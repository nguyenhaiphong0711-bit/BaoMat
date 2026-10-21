using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class QuestionBankService(MongoContext db, UserService users)
{
    public async Task<QuestionBankItem?> GetAsync(string id) => ObjectId.TryParse(id, out var key)
        ? await db.Questions.Find(x => x.Id == key).FirstOrDefaultAsync() : null;

    public async Task<List<Subject>> AuthorSubjectsAsync(User user) => await db.Subjects.Find(x => !x.IsArchived &&
        (user.Role == Roles.Admin || user.TeachingSubjectIds.Contains(x.Id))).SortBy(x => x.Name).ToListAsync();

    private async Task<bool> CanManageAsync(User user, QuestionBankItem question) => user.Role == Roles.Admin ||
        (user.Role == Roles.Teacher && question.OwnerId == user.Id &&
         await users.HasPermissionAsync(user.Id, PermissionCodes.QuestionsManage));

    private async Task<bool> CanReviewAsync(User user, QuestionBankItem question)
    {
        if (user.Role == Roles.Admin) return true;
        if (user.Role != Roles.Staff || user.DepartmentId is not { } department ||
            !await users.HasPermissionAsync(user.Id, PermissionCodes.QuestionsReview) ||
            !await db.Departments.Find(x => x.Id == department && !x.IsArchived).AnyAsync()) return false;
        return await db.Subjects.Find(x => x.Id == question.SubjectId && x.DepartmentId == department && !x.IsArchived).AnyAsync();
    }

    private async Task<bool> CanReadAsync(User user, QuestionBankItem question)
    {
        if (await CanManageAsync(user, question)) return true;
        if (question.IsArchived) return false;
        if (user.Role == Roles.Admin) return true;
        if (await CanReviewAsync(user, question)) return question.State != QuestionStates.Draft;
        return user.Role == Roles.Teacher && question.State == QuestionStates.Published &&
            user.TeachingSubjectIds.Contains(question.SubjectId) &&
            await users.HasPermissionAsync(user.Id, PermissionCodes.QuestionsView);
    }

    public async Task<QuestionResponse?> ResponseAsync(User user, QuestionBankItem question)
    {
        if (!await CanReadAsync(user, question)) return null;
        var subject = await db.Subjects.Find(x => x.Id == question.SubjectId).FirstOrDefaultAsync();
        if (subject is null) return null;
        var manager = await CanManageAsync(user, question);
        var reviewer = await CanReviewAsync(user, question);
        return new(question.Id.ToString(), question.Revision, question.SubjectId.ToString(), subject.Name,
            question.Topic, question.Difficulty, question.Content, question.Options, question.CorrectOption,
            question.Explanation, question.State, question.IsArchived, question.EditorName,
            question.ReviewerName, manager || reviewer ? question.ReviewComment : "",
            manager && !question.IsArchived && question.State is QuestionStates.Draft or QuestionStates.Rejected,
            manager && !question.IsArchived && question.State == QuestionStates.Draft,
            reviewer && !question.IsArchived && question.State == QuestionStates.Pending &&
                question.OwnerId != user.Id && question.EditorId != user.Id,
            manager && !question.IsArchived && question.State == QuestionStates.Approved,
            manager && !question.IsArchived);
    }

    public async Task<object> ListAsync(User user, string? query, string? subjectId, string? state, int page)
    {
        var items = new List<QuestionResponse>();
        foreach (var question in await db.Questions.Find(_ => true).SortByDescending(x => x.UpdatedAt).ToListAsync())
        {
            var response = await ResponseAsync(user, question);
            if (response is null || (!string.IsNullOrWhiteSpace(query) &&
                !(response.Content + " " + response.Topic).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(subjectId) && response.SubjectId != subjectId) ||
                (!string.IsNullOrWhiteSpace(state) && (response.IsArchived ? "Archived" : response.State) != state)) continue;
            items.Add(response);
        }
        return new { items = items.Skip((page - 1) * 15).Take(15), total = items.Count, page, pageSize = 15 };
    }

    private async Task ApplyRequestAsync(User user, QuestionBankItem question, QuestionRequest request)
    {
        var subjects = await AuthorSubjectsAsync(user);
        if (!ObjectId.TryParse(request.SubjectId, out var subjectId) || subjects.All(x => x.Id != subjectId))
            throw new ArgumentException("Chỉ được soạn câu hỏi cho môn đang được phân công.");
        var options = request.Options?.Select(x => x?.Trim() ?? "").ToArray() ?? Array.Empty<string>();
        if (options.Length is < 2 or > 6 || options.Any(string.IsNullOrWhiteSpace) || options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Length)
            throw new ArgumentException("Cần từ 2 đến 6 lựa chọn khác nhau và không để trống.");
        if (request.CorrectOption < 0 || request.CorrectOption >= options.Length)
            throw new ArgumentException("Đáp án đúng không thuộc danh sách lựa chọn.");
        question.SubjectId = subjectId;
        question.Topic = request.Topic.Trim(); question.Difficulty = request.Difficulty;
        question.Content = request.Content.Trim(); question.CorrectOption = request.CorrectOption;
        question.Explanation = request.Explanation.Trim(); question.EditorId = user.Id;
        question.EditorName = user.FullName; question.Options = options.Select((text, index) =>
            new QuestionOption { Id = ((char)('A' + index)).ToString(), Text = text }).ToList();
        question.State = QuestionStates.Draft; question.ReviewerId = null; question.ReviewerName = "";
        question.ReviewComment = ""; question.ReviewedAt = null; question.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<QuestionBankItem> CreateAsync(User user, QuestionRequest request)
    {
        var question = new QuestionBankItem { OwnerId = user.Id };
        await ApplyRequestAsync(user, question, request);
        await db.Questions.InsertOneAsync(question);
        return question;
    }

    public async Task<bool> EditAsync(User user, QuestionBankItem question, QuestionRequest request)
    {
        if (!await CanManageAsync(user, question)) throw new UnauthorizedAccessException();
        if (question.IsArchived || question.State is not (QuestionStates.Draft or QuestionStates.Rejected))
            throw new ArgumentException("Chỉ có thể sửa câu hỏi nháp hoặc câu hỏi bị từ chối.");
        await ApplyRequestAsync(user, question, request);
        return await SaveAsync(question, request.Revision);
    }

    public async Task<bool> TransitionAsync(User user, QuestionBankItem question, string action, QuestionActionRequest request)
    {
        if (question.IsArchived) throw new ArgumentException("Câu hỏi đã được lưu trữ.");
        if (action == "review")
        {
            if (!await CanReviewAsync(user, question) || question.OwnerId == user.Id || question.EditorId == user.Id)
                throw new UnauthorizedAccessException();
            if (question.State != QuestionStates.Pending) throw new ArgumentException("Câu hỏi không ở trạng thái chờ duyệt.");
            if (!request.Approve && string.IsNullOrWhiteSpace(request.Comment)) throw new ArgumentException("Cần nhận xét khi từ chối.");
            question.State = request.Approve ? QuestionStates.Approved : QuestionStates.Rejected;
            question.ReviewerId = user.Id; question.ReviewerName = user.FullName;
            question.ReviewComment = request.Comment.Trim(); question.ReviewedAt = DateTime.UtcNow;
        }
        else
        {
            if (!await CanManageAsync(user, question)) throw new UnauthorizedAccessException();
            if (action != "archive" && (await AuthorSubjectsAsync(user)).All(x => x.Id != question.SubjectId))
                throw new UnauthorizedAccessException();
            switch (action)
            {
                case "submit" when question.State == QuestionStates.Draft: question.State = QuestionStates.Pending; break;
                case "publish" when question.State == QuestionStates.Approved: question.State = QuestionStates.Published; break;
                case "archive": question.IsArchived = true; break;
                default: throw new ArgumentException("Trạng thái không hợp lệ cho thao tác này.");
            }
        }
        question.UpdatedAt = DateTime.UtcNow;
        return await SaveAsync(question, request.Revision);
    }

    private async Task<bool> SaveAsync(QuestionBankItem question, int expectedRevision)
    {
        if (question.Revision != expectedRevision) return false;
        question.Revision++;
        var result = await db.Questions.ReplaceOneAsync(x => x.Id == question.Id && x.Revision == expectedRevision, question);
        return result.MatchedCount == 1;
    }
}
