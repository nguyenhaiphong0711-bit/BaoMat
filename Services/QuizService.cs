using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class QuizService(MongoContext db, UserService users, LearningEventService events)
{
    public async Task<Quiz?> GetAsync(string id) => ObjectId.TryParse(id, out var key)
        ? await db.Quizzes.Find(x => x.Id == key).FirstOrDefaultAsync() : null;

    private async Task<ClassRoom?> ClassAsync(ObjectId id) =>
        await db.Classes.Find(x => x.Id == id && !x.IsArchived).FirstOrDefaultAsync();

    private async Task<bool> CanManageAsync(User user, Quiz quiz)
    {
        if (user.Role == Roles.Admin) return true;
        if (user.Role != Roles.Teacher || !await users.HasPermissionAsync(user.Id, PermissionCodes.QuizzesManage)) return false;
        return await db.Classes.Find(x => x.Id == quiz.ClassId && !x.IsArchived && x.TeacherId == user.Id).AnyAsync();
    }

    private async Task<bool> CanAttemptAsync(User user, Quiz quiz) => user.Role == Roles.Student &&
        await users.HasPermissionAsync(user.Id, PermissionCodes.QuizzesAttempt) &&
        await db.Classes.Find(x => x.Id == quiz.ClassId && !x.IsArchived && x.StudentIds.Contains(user.Id)).AnyAsync();

    public async Task<object> ListAsync(User user)
    {
        var filter = user.Role switch
        {
            Roles.Admin => Builders<Quiz>.Filter.Empty,
            Roles.Teacher => Builders<Quiz>.Filter.In(x => x.ClassId,
                (await db.Classes.Find(x => !x.IsArchived && x.TeacherId == user.Id).ToListAsync()).Select(x => x.Id)),
            Roles.Student => Builders<Quiz>.Filter.And(
                Builders<Quiz>.Filter.In(x => x.ClassId,
                    (await db.Classes.Find(x => !x.IsArchived && x.StudentIds.Contains(user.Id)).ToListAsync()).Select(x => x.Id)),
                Builders<Quiz>.Filter.Eq(x => x.IsPublished, true), Builders<Quiz>.Filter.Eq(x => x.IsArchived, false)),
            _ => Builders<Quiz>.Filter.Where(_ => false)
        };
        var quizzes = await db.Quizzes.Find(filter).SortByDescending(x => x.OpensAt).ToListAsync();
        var classIds = quizzes.Select(x => x.ClassId).Distinct().ToArray();
        var classes = (await db.Classes.Find(Builders<ClassRoom>.Filter.In(x => x.Id, classIds)).ToListAsync()).ToDictionary(x => x.Id);
        var attempts = user.Role == Roles.Student
            ? (await db.QuizAttempts.Find(x => x.StudentId == user.Id && quizzes.Select(q => q.Id).Contains(x.QuizId)).ToListAsync()).ToDictionary(x => x.QuizId)
            : new Dictionary<ObjectId, QuizAttempt>();
        return quizzes.Select(q => Summary(q, classes.GetValueOrDefault(q.ClassId)?.Name ?? "", attempts.GetValueOrDefault(q.Id))).ToArray();
    }

    private static QuizSummaryResponse Summary(Quiz quiz, string className, QuizAttempt? attempt = null) =>
        new(quiz.Id.ToString(), quiz.Revision, quiz.ClassId.ToString(), className, quiz.Title,
            quiz.DurationMinutes, quiz.OpensAt, quiz.ClosesAt, quiz.QuestionIds.Count, quiz.IsPublished,
            quiz.IsArchived, attempt is null ? "NotStarted" : attempt.SubmittedAt.HasValue ? "Submitted" : "InProgress", attempt?.Score);

    public async Task<object?> DetailsAsync(User user, Quiz quiz)
    {
        var classroom = await ClassAsync(quiz.ClassId); if (classroom is null) return null;
        if (await CanManageAsync(user, quiz))
        {
            var questionDocs = await db.Questions.Find(x => quiz.QuestionIds.Contains(x.Id)).ToListAsync();
            var byId = questionDocs.ToDictionary(x => x.Id);
            var questionResponses = quiz.QuestionIds.Where(byId.ContainsKey).Select(id =>
            {
                var q = byId[id];
                return new QuestionResponse(q.Id.ToString(), q.Revision, q.SubjectId.ToString(), "", q.Topic,
                    q.Difficulty, q.Content, q.Options, q.CorrectOption, q.Explanation, q.State, q.IsArchived,
                    q.EditorName, q.ReviewerName, q.ReviewComment, false, false, false, false, false);
            }).ToArray();
            var attemptDocs = await db.QuizAttempts.Find(x => x.QuizId == quiz.Id).SortBy(x => x.StartedAt).ToListAsync();
            var studentIds = attemptDocs.Select(x => x.StudentId).Distinct().ToArray();
            var names = (await db.Users.Find(Builders<User>.Filter.In(x => x.Id, studentIds)).ToListAsync()).ToDictionary(x => x.Id, x => x.FullName);
            var results = attemptDocs.Select(x => new QuizAttemptSummary(x.StudentId.ToString(), names.GetValueOrDefault(x.StudentId) ?? "", x.StartedAt, x.SubmittedAt, x.Score)).ToArray();
            return new QuizManageResponse(Summary(quiz, classroom.Name), quiz.Instructions, questionResponses,
                !quiz.IsPublished && !quiz.IsArchived, !quiz.IsPublished && !quiz.IsArchived,
                !quiz.IsArchived, results);
        }
        if (!quiz.IsPublished || quiz.IsArchived || !await CanAttemptAsync(user, quiz)) return null;
        var attempt = await db.QuizAttempts.Find(x => x.QuizId == quiz.Id && x.StudentId == user.Id).FirstOrDefaultAsync();
        return new { quiz = Summary(quiz, classroom.Name, attempt), quiz.Instructions,
            canStart = attempt is null && DateTime.UtcNow >= quiz.OpensAt && DateTime.UtcNow <= quiz.ClosesAt,
            attemptId = attempt?.Id.ToString() };
    }

    public async Task<object> EditorOptionsAsync(User user, string? classId)
    {
        var classes = user.Role == Roles.Admin
            ? await db.Classes.Find(x => !x.IsArchived).SortBy(x => x.Name).ToListAsync()
            : await db.Classes.Find(x => !x.IsArchived && x.TeacherId == user.Id).SortBy(x => x.Name).ToListAsync();
        var selected = ObjectId.TryParse(classId, out var parsed) ? classes.FirstOrDefault(x => x.Id == parsed) : null;
        var questions = selected is null ? new List<QuestionBankItem>() : await db.Questions.Find(x =>
            x.SubjectId == selected.SubjectId && x.State == QuestionStates.Published && !x.IsArchived).SortBy(x => x.Topic).ToListAsync();
        return new
        {
            classes = classes.Select(x => new { id = x.Id.ToString(), x.Name, subjectId = x.SubjectId.ToString() }),
            questions = questions.Select(x => new { id = x.Id.ToString(), x.Content, x.Topic, x.Difficulty })
        };
    }

    private async Task ApplyAsync(User user, Quiz quiz, QuizRequest request)
    {
        if (!ObjectId.TryParse(request.ClassId, out var classId)) throw new ArgumentException("Lớp học không hợp lệ.");
        var classroom = await ClassAsync(classId);
        if (classroom is null || (user.Role != Roles.Admin && classroom.TeacherId != user.Id)) throw new UnauthorizedAccessException();
        var opens = request.OpensAt.ToUniversalTime(); var closes = request.ClosesAt.ToUniversalTime();
        if (closes <= opens || closes <= DateTime.UtcNow) throw new ArgumentException("Thời gian đóng phải sau thời gian mở và ở tương lai.");
        if (request.QuestionIds is null || request.QuestionIds.Length is < 1 or > 50 ||
            request.QuestionIds.Any(x => !ObjectId.TryParse(x, out _))) throw new ArgumentException("Danh sách câu hỏi không hợp lệ.");
        var ids = request.QuestionIds.Distinct().Select(ObjectId.Parse).ToArray();
        if (ids.Length != request.QuestionIds.Length) throw new ArgumentException("Danh sách câu hỏi bị trùng.");
        var count = await db.Questions.CountDocumentsAsync(x => ids.Contains(x.Id) && x.SubjectId == classroom.SubjectId &&
            x.State == QuestionStates.Published && !x.IsArchived);
        if (count != ids.Length) throw new ArgumentException("Chỉ được chọn câu hỏi đã công bố thuộc môn của lớp.");
        quiz.ClassId = classId; quiz.Title = request.Title.Trim(); quiz.Instructions = request.Instructions.Trim();
        quiz.DurationMinutes = request.DurationMinutes; quiz.OpensAt = opens; quiz.ClosesAt = closes;
        quiz.QuestionIds = ids.ToList(); quiz.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<Quiz> CreateAsync(User user, QuizRequest request)
    {
        var quiz = new Quiz { OwnerId = user.Id };
        await ApplyAsync(user, quiz, request); await db.Quizzes.InsertOneAsync(quiz); return quiz;
    }

    public async Task<bool> EditAsync(User user, Quiz quiz, QuizRequest request)
    {
        if (!await CanManageAsync(user, quiz)) throw new UnauthorizedAccessException();
        if (quiz.IsPublished || quiz.IsArchived) throw new ArgumentException("Chỉ có thể sửa bài kiểm tra nháp.");
        await ApplyAsync(user, quiz, request); return await SaveAsync(quiz, request.Revision);
    }

    public async Task<bool> TransitionAsync(User user, Quiz quiz, string action, int revision)
    {
        if (!await CanManageAsync(user, quiz)) throw new UnauthorizedAccessException();
        if (quiz.IsArchived) throw new ArgumentException("Bài kiểm tra đã lưu trữ.");
        switch (action)
        {
            case "publish" when !quiz.IsPublished:
                var classroom = await ClassAsync(quiz.ClassId) ?? throw new UnauthorizedAccessException();
                if (await db.Questions.CountDocumentsAsync(x => quiz.QuestionIds.Contains(x.Id) &&
                    x.SubjectId == classroom.SubjectId && x.State == QuestionStates.Published && !x.IsArchived) != quiz.QuestionIds.Count)
                    throw new ArgumentException("Có câu hỏi không còn ở trạng thái công bố.");
                quiz.IsPublished = true; break;
            case "archive": quiz.IsArchived = true; break;
            default: throw new ArgumentException("Trạng thái không hợp lệ cho thao tác này.");
        }
        quiz.UpdatedAt = DateTime.UtcNow; return await SaveAsync(quiz, revision);
    }

    private async Task<bool> SaveAsync(Quiz quiz, int expected)
    {
        if (quiz.Revision != expected) return false; quiz.Revision++;
        return (await db.Quizzes.ReplaceOneAsync(x => x.Id == quiz.Id && x.Revision == expected, quiz)).MatchedCount == 1;
    }

    public async Task<QuizAttemptResponse> StartAsync(User user, Quiz quiz)
    {
        if (!quiz.IsPublished || quiz.IsArchived || !await CanAttemptAsync(user, quiz)) throw new UnauthorizedAccessException();
        var now = DateTime.UtcNow;
        if (now < quiz.OpensAt || now > quiz.ClosesAt) throw new ArgumentException("Bài kiểm tra chưa mở hoặc đã đóng.");
        var existing = await db.QuizAttempts.Find(x => x.QuizId == quiz.Id && x.StudentId == user.Id).FirstOrDefaultAsync();
        if (existing is not null) return await AttemptResponseAsync(user, quiz, existing);
        var questions = await db.Questions.Find(x => quiz.QuestionIds.Contains(x.Id) && x.State == QuestionStates.Published && !x.IsArchived).ToListAsync();
        var byId = questions.ToDictionary(x => x.Id);
        if (quiz.QuestionIds.Any(id => !byId.ContainsKey(id))) throw new ArgumentException("Bài kiểm tra có câu hỏi không còn khả dụng.");
        var attempt = new QuizAttempt { QuizId = quiz.Id, StudentId = user.Id, StartedAt = now,
            ExpiresAt = new[] { quiz.ClosesAt, now.AddMinutes(quiz.DurationMinutes) }.Min(),
            Questions = quiz.QuestionIds.Select(id => { var q = byId[id]; return new QuizQuestionSnapshot
                { SourceQuestionId = id, Content = q.Content, Options = q.Options, CorrectOption = q.CorrectOption, Explanation = q.Explanation }; }).ToList(),
            Answers = Enumerable.Repeat(-1, quiz.QuestionIds.Count).ToList() };
        var created = false;
        try { await db.QuizAttempts.InsertOneAsync(attempt); created = true; }
        catch (MongoWriteException error) when (error.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        { attempt = await db.QuizAttempts.Find(x => x.QuizId == quiz.Id && x.StudentId == user.Id).FirstAsync(); }
        if (created)
        {
            var classroom = await ClassAsync(quiz.ClassId);
            var subject = classroom is null ? null : await db.Subjects.Find(x => x.Id == classroom.SubjectId).FirstOrDefaultAsync();
            await events.RecordAsync("quiz.started", user, "Quiz", quiz.Id, "Đã bắt đầu bài kiểm tra " + quiz.Title,
                user.Id, subject?.DepartmentId, quiz.ClassId);
        }
        return await AttemptResponseAsync(user, quiz, attempt);
    }

    public async Task<QuizAttemptResponse?> GetAttemptAsync(User user, string id)
    {
        if (!ObjectId.TryParse(id, out var key)) return null;
        var attempt = await db.QuizAttempts.Find(x => x.Id == key).FirstOrDefaultAsync(); if (attempt is null) return null;
        var quiz = await db.Quizzes.Find(x => x.Id == attempt.QuizId).FirstOrDefaultAsync(); if (quiz is null) return null;
        if (user.Role == Roles.Student && attempt.StudentId != user.Id) return null;
        if (user.Role != Roles.Student && !await CanManageAsync(user, quiz)) return null;
        return await AttemptResponseAsync(user, quiz, attempt);
    }

    public async Task<QuizAttemptResponse> SubmitAsync(User user, string id, int[]? answers)
    {
        if (!ObjectId.TryParse(id, out var key)) throw new KeyNotFoundException();
        var attempt = await db.QuizAttempts.Find(x => x.Id == key && x.StudentId == user.Id).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException();
        var quiz = await db.Quizzes.Find(x => x.Id == attempt.QuizId).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException();
        if (!await CanAttemptAsync(user, quiz)) throw new UnauthorizedAccessException();
        attempt = await FinalizeExpiredAsync(attempt);
        if (attempt.SubmittedAt.HasValue) return await AttemptResponseAsync(user, quiz, attempt);
        var submitted = answers;
        if (submitted is null || submitted.Length != attempt.Questions.Count || submitted.Select((answer, index) =>
            answer < -1 || answer >= attempt.Questions[index].Options.Count).Any(x => x))
            throw new ArgumentException("Danh sách câu trả lời không hợp lệ.");
        attempt.Answers = submitted.ToList(); attempt.SubmittedAt = DateTime.UtcNow;
        attempt.Score = Math.Round(attempt.Questions.Select((q, i) => q.CorrectOption == attempt.Answers[i]).Count(x => x) * 10d / attempt.Questions.Count, 2);
        var result = await db.QuizAttempts.ReplaceOneAsync(x => x.Id == attempt.Id && x.SubmittedAt == null, attempt);
        if (result.MatchedCount == 0) attempt = await db.QuizAttempts.Find(x => x.Id == attempt.Id).FirstAsync();
        else
        {
            var classroom = await ClassAsync(quiz.ClassId);
            var subject = classroom is null ? null : await db.Subjects.Find(x => x.Id == classroom.SubjectId).FirstOrDefaultAsync();
            await events.RecordAsync("quiz.submitted", user, "Quiz", quiz.Id, "Đã nộp bài kiểm tra " + quiz.Title,
                user.Id, subject?.DepartmentId, quiz.ClassId);
        }
        return await AttemptResponseAsync(user, quiz, attempt);
    }

    private async Task<QuizAttemptResponse> AttemptResponseAsync(User user, Quiz quiz, QuizAttempt attempt)
    {
        attempt = await FinalizeExpiredAsync(attempt);
        var reveal = attempt.SubmittedAt.HasValue || user.Role is Roles.Admin or Roles.Teacher;
        return new(attempt.Id.ToString(), quiz.Id.ToString(), quiz.Title, quiz.Instructions, attempt.StartedAt,
            attempt.ExpiresAt, attempt.SubmittedAt, attempt.Score, attempt.Questions.Select(q =>
                new QuizAttemptQuestionResponse(q.Content, q.Options, reveal ? q.CorrectOption : null,
                    reveal ? q.Explanation : "")).ToArray(), attempt.Answers);
    }

    private async Task<QuizAttempt> FinalizeExpiredAsync(QuizAttempt attempt)
    {
        if (attempt.SubmittedAt.HasValue || DateTime.UtcNow <= attempt.ExpiresAt) return attempt;
        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.Score = Math.Round(attempt.Questions.Select((q, i) => q.CorrectOption == attempt.Answers[i])
            .Count(x => x) * 10d / attempt.Questions.Count, 2);
        var result = await db.QuizAttempts.ReplaceOneAsync(x => x.Id == attempt.Id && x.SubmittedAt == null, attempt);
        return result.MatchedCount == 1 ? attempt : await db.QuizAttempts.Find(x => x.Id == attempt.Id).FirstAsync();
    }
}
