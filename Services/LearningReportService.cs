using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class LearningEventService(MongoContext db)
{
    public Task RecordAsync(string type, User actor, string entityType, ObjectId entityId, string summary,
        ObjectId? studentId = null, ObjectId? departmentId = null, ObjectId? classId = null) =>
        db.LearningEvents.InsertOneAsync(new LearningEvent { Type = type, ActorId = actor.Id,
            ActorRole = actor.Role, EntityType = entityType, EntityId = entityId, Summary = summary,
            StudentId = studentId, DepartmentId = departmentId, ClassId = classId });

    public async Task RecordDailyAsync(string type, User actor, string entityType, ObjectId entityId, string summary,
        ObjectId? studentId = null, ObjectId? departmentId = null, ObjectId? classId = null)
    {
        var now = DateTime.UtcNow;
        var key = $"{type}:{actor.Id}:{entityId}:{now:yyyyMMdd}";
        try
        {
            await db.LearningEvents.UpdateOneAsync(x => x.EventKey == key,
                Builders<LearningEvent>.Update
                    .SetOnInsert(x => x.EventKey, key).SetOnInsert(x => x.Type, type)
                    .SetOnInsert(x => x.ActorId, actor.Id).SetOnInsert(x => x.ActorRole, actor.Role)
                    .SetOnInsert(x => x.EntityType, entityType).SetOnInsert(x => x.EntityId, entityId)
                    .SetOnInsert(x => x.Summary, summary).SetOnInsert(x => x.StudentId, studentId)
                    .SetOnInsert(x => x.DepartmentId, departmentId).SetOnInsert(x => x.ClassId, classId)
                    .SetOnInsert(x => x.OccurredAt, now), new UpdateOptions { IsUpsert = true });
        }
        catch (MongoWriteException error) when (error.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // A simultaneous view/download already inserted the same daily event.
        }
    }
}

public sealed class LearningReportService(MongoContext db)
{
    public async Task<LearningReportResponse> BuildAsync(User user, DateTime? from = null, DateTime? toExclusive = null)
    {
        var metrics = new List<ReportMetric>();
        FilterDefinition<LearningEvent> eventFilter;
        FilterDefinition<SupportTicket> supportFilter;
        string scope;

        if (user.Role == Roles.Admin)
        {
            scope = "Toàn hệ thống";
            metrics.Add(new("students", "Sinh viên đang hoạt động", await db.Users.CountDocumentsAsync(x => x.Role == Roles.Student && x.IsActive), "tài khoản"));
            metrics.Add(new("teachers", "Giảng viên đang hoạt động", await db.Users.CountDocumentsAsync(x => x.Role == Roles.Teacher && x.IsActive), "tài khoản"));
            metrics.Add(new("classes", "Lớp đang hoạt động", await db.Classes.CountDocumentsAsync(x => !x.IsArchived), "lớp"));
            metrics.Add(new("resources", "Học liệu đã công bố", await db.Resources.CountDocumentsAsync(x => !x.IsArchived && x.PublishedVersion != null), "tài liệu"));
            var attempts = await db.QuizAttempts.Find(x => x.SubmittedAt != null).ToListAsync();
            metrics.Add(new("quizAverage", "Điểm kiểm tra trung bình", Average(attempts), "điểm"));
            eventFilter = Builders<LearningEvent>.Filter.Empty; supportFilter = Builders<SupportTicket>.Filter.Empty;
        }
        else if (user.Role == Roles.Teacher)
        {
            var classes = await db.Classes.Find(x => !x.IsArchived && x.TeacherId == user.Id).ToListAsync();
            var classIds = classes.Select(x => x.Id).ToArray();
            var assignmentIds = await db.Assignments.Find(x => classIds.Contains(x.ClassId) && !x.IsArchived).Project(x => x.Id).ToListAsync();
            var quizIds = await db.Quizzes.Find(x => classIds.Contains(x.ClassId) && !x.IsArchived).Project(x => x.Id).ToListAsync();
            var attempts = await db.QuizAttempts.Find(x => quizIds.Contains(x.QuizId) && x.SubmittedAt != null).ToListAsync();
            scope = "Các lớp đang phụ trách";
            metrics.Add(new("classes", "Lớp phụ trách", classes.Count, "lớp"));
            metrics.Add(new("assignments", "Bài tập", assignmentIds.Count, "bài"));
            metrics.Add(new("submissions", "Bài đã nộp", await db.Submissions.CountDocumentsAsync(x => assignmentIds.Contains(x.AssignmentId)), "lượt"));
            metrics.Add(new("quizAverage", "Điểm kiểm tra trung bình", Average(attempts), "điểm"));
            eventFilter = Builders<LearningEvent>.Filter.In(x => x.ClassId, classIds.Select(x => (ObjectId?)x));
            supportFilter = Builders<SupportTicket>.Filter.Where(_ => false);
        }
        else if (user.Role == Roles.Student)
        {
            var classes = await db.Classes.Find(x => !x.IsArchived && x.StudentIds.Contains(user.Id)).ToListAsync();
            var classIds = classes.Select(x => x.Id).ToArray();
            var assignmentIds = await db.Assignments.Find(x => classIds.Contains(x.ClassId) && !x.IsArchived && x.IsPublished).Project(x => x.Id).ToListAsync();
            var attempts = await db.QuizAttempts.Find(x => x.StudentId == user.Id && x.SubmittedAt != null).ToListAsync();
            scope = "Tiến độ của tôi";
            metrics.Add(new("classes", "Lớp đang tham gia", classes.Count, "lớp"));
            metrics.Add(new("assignments", "Bài tập đã công bố", assignmentIds.Count, "bài"));
            metrics.Add(new("submissions", "Bài đã nộp", await db.Submissions.CountDocumentsAsync(x => x.StudentId == user.Id && assignmentIds.Contains(x.AssignmentId)), "bài"));
            metrics.Add(new("lessonsCompleted", "Bài học đã hoàn thành", await db.LessonCompletions.CountDocumentsAsync(x => x.StudentId == user.Id), "bài"));
            metrics.Add(new("resourceActivity", "Học liệu đã xem hoặc tải", await db.LearningEvents.CountDocumentsAsync(x =>
                x.StudentId == user.Id && (x.Type == "resource.viewed" || x.Type == "resource.downloaded")), "lượt/ngày"));
            metrics.Add(new("quizAverage", "Điểm kiểm tra trung bình", Average(attempts), "điểm"));
            eventFilter = Builders<LearningEvent>.Filter.Or(
                Builders<LearningEvent>.Filter.Eq(x => x.StudentId, user.Id),
                Builders<LearningEvent>.Filter.Eq(x => x.ActorId, user.Id));
            supportFilter = Builders<SupportTicket>.Filter.Eq(x => x.StudentId, user.Id);
        }
        else if (user.Role == Roles.Staff && user.DepartmentId is { } departmentId &&
                 await db.Departments.Find(x => x.Id == departmentId && !x.IsArchived).FirstOrDefaultAsync() is { } department)
        {
            var subjectIds = await db.Subjects.Find(x => x.DepartmentId == departmentId && !x.IsArchived).Project(x => x.Id).ToListAsync();
            var classIds = await db.Classes.Find(x => subjectIds.Contains(x.SubjectId) && !x.IsArchived).Project(x => x.Id).ToListAsync();
            var quizIds = await db.Quizzes.Find(x => classIds.Contains(x.ClassId)).Project(x => x.Id).ToListAsync();
            var attempts = await db.QuizAttempts.Find(x => quizIds.Contains(x.QuizId) && x.SubmittedAt != null).ToListAsync();
            scope = "Khoa " + department.Name;
            metrics.Add(new("students", "Sinh viên trong khoa", await db.Users.CountDocumentsAsync(x => x.Role == Roles.Student && x.IsActive && x.DepartmentId == departmentId), "sinh viên"));
            metrics.Add(new("classes", "Lớp trong khoa", classIds.Count, "lớp"));
            metrics.Add(new("quizAttempts", "Lượt kiểm tra đã nộp", attempts.Count, "lượt"));
            metrics.Add(new("quizAverage", "Điểm kiểm tra trung bình", Average(attempts), "điểm"));
            eventFilter = Builders<LearningEvent>.Filter.Eq(x => x.DepartmentId, departmentId);
            supportFilter = Builders<SupportTicket>.Filter.Eq(x => x.DepartmentId, departmentId);
        }
        else throw new UnauthorizedAccessException();

        if (from.HasValue) eventFilter &= Builders<LearningEvent>.Filter.Gte(x => x.OccurredAt, from.Value);
        if (toExclusive.HasValue) eventFilter &= Builders<LearningEvent>.Filter.Lt(x => x.OccurredAt, toExclusive.Value);
        var tickets = await db.SupportTickets.Find(supportFilter).ToListAsync();
        var groups = new[] { TicketStatuses.Open, TicketStatuses.InProgress, TicketStatuses.Resolved, TicketStatuses.Closed }
            .Select(status => new ReportGroup(status, status switch { TicketStatuses.Open => "Mới", TicketStatuses.InProgress => "Đang xử lý", TicketStatuses.Resolved => "Đã giải quyết", _ => "Đã đóng" }, tickets.Count(x => x.Status == status))).ToArray();
        metrics.Add(new("openSupport", "Yêu cầu chưa hoàn tất", tickets.Count(x => x.Status is TicketStatuses.Open or TicketStatuses.InProgress), "yêu cầu"));
        var recent = (await db.LearningEvents.Find(eventFilter).SortByDescending(x => x.OccurredAt).Limit(20).ToListAsync())
            .Select(x => new ReportEvent(x.Type, x.Summary, x.OccurredAt)).ToArray();
        return new(scope, metrics, groups, recent);
    }

    private static double Average(IEnumerable<QuizAttempt> attempts)
    {
        var scores = attempts.Where(x => x.Score.HasValue).Select(x => x.Score!.Value).ToArray();
        return scores.Length == 0 ? 0 : Math.Round(scores.Average(), 2);
    }
}
