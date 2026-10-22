using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class QuestionBankService(MongoContext db, UserService users, NotificationService notifications,
    LearningEventService events)
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
            manager && !question.IsArchived, question.AiAssisted, question.AiModel, question.AiGeneratedAt,
            question.CurrentVersion, question.Versions.OrderByDescending(x => x.Number)
                .Select(x => new QuestionVersionResponse(x.Number, x.EditorName, x.CreatedAt)).ToArray());
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
        AddVersion(question);
        await db.Questions.InsertOneAsync(question);
        return question;
    }

    public async Task<bool> EditAsync(User user, QuestionBankItem question, QuestionRequest request)
    {
        if (!await CanManageAsync(user, question)) throw new UnauthorizedAccessException();
        if (question.IsArchived || question.State is not (QuestionStates.Draft or QuestionStates.Rejected))
            throw new ArgumentException("Chỉ có thể sửa câu hỏi nháp hoặc câu hỏi bị từ chối.");
        await ApplyRequestAsync(user, question, request);
        if (question.Versions.Count >= 50) throw new ArgumentException("Câu hỏi đã đạt giới hạn 50 phiên bản.");
        question.CurrentVersion = Math.Max(question.CurrentVersion, question.Versions.LastOrDefault()?.Number ?? 0) + 1;
        AddVersion(question);
        return await SaveAsync(question, request.Revision);
    }

    private static void AddVersion(QuestionBankItem question) => question.Versions.Add(new QuestionVersion
    {
        Number = question.CurrentVersion, Content = question.Content, Topic = question.Topic,
        Difficulty = question.Difficulty, Options = question.Options.Select(x => new QuestionOption { Id = x.Id, Text = x.Text }).ToList(),
        CorrectOption = question.CorrectOption, Explanation = question.Explanation,
        EditorId = question.EditorId, EditorName = question.EditorName, CreatedAt = question.UpdatedAt
    });

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
        var saved = await SaveAsync(question, request.Revision);
        if (saved)
        {
            var auditSubject = await db.Subjects.Find(x => x.Id == question.SubjectId).FirstOrDefaultAsync();
            await events.RecordAsync($"question.{action}", user, "Question", question.Id,
                $"Câu hỏi đã thực hiện thao tác {action}", departmentId: auditSubject?.DepartmentId);
        }
        if (saved && action == "submit")
        {
            var subject = await db.Subjects.Find(x => x.Id == question.SubjectId).FirstOrDefaultAsync();
            if (subject is not null) await notifications.NotifyDepartmentAsync(subject.DepartmentId,
                "question.review", "Câu hỏi chờ kiểm duyệt", question.Content,
                $"/app/questions/{question.Id}", user.Id);
        }
        else if (saved && action == "review")
            await notifications.AddAsync(question.OwnerId, "question.reviewed",
                request.Approve ? "Câu hỏi đã được duyệt" : "Câu hỏi cần chỉnh sửa",
                request.Approve ? question.Content : question.ReviewComment, $"/app/questions/{question.Id}");
        return saved;
    }

    private async Task<bool> SaveAsync(QuestionBankItem question, int expectedRevision)
    {
        if (question.Revision != expectedRevision) return false;
        question.Revision++;
        var result = await db.Questions.ReplaceOneAsync(x => x.Id == question.Id && x.Revision == expectedRevision, question);
        return result.MatchedCount == 1;
    }
}
