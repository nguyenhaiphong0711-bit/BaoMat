using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class CourseOpeningService(MongoContext db, ClassService classes, NotificationService notifications)
{
    public async Task<CourseOpeningRequest> CreateAsync(User teacher, CourseOpeningCreateRequest request)
    {
        if (!ObjectId.TryParse(request.SubjectId, out var subjectId) ||
            !ObjectId.TryParse(request.AcademicTermId, out var termId) ||
            !teacher.TeachingSubjectIds.Contains(subjectId))
            throw new ArgumentException("Chỉ được đề xuất lớp cho môn đã được phân công.");
        var subject = await db.Subjects.Find(x => x.Id == subjectId && !x.IsArchived).FirstOrDefaultAsync();
        var term = await db.AcademicTerms.Find(x => x.Id == termId && x.EndsAt > DateTime.UtcNow).FirstOrDefaultAsync();
        if (subject is null || term is null) throw new ArgumentException("Môn học hoặc học kỳ không còn hợp lệ.");
        if (await db.CourseOpeningRequests.Find(x => x.TeacherId == teacher.Id && x.SubjectId == subjectId &&
            x.AcademicTermId == termId && x.Status == CourseOpeningStatuses.Pending).AnyAsync())
            throw new ArgumentException("Bạn đã có một đề xuất đang chờ duyệt cho môn và học kỳ này.");
        var item = new CourseOpeningRequest { TeacherId = teacher.Id, SubjectId = subjectId, AcademicTermId = termId,
            Name = request.Name.Trim(), Description = request.Description.Trim(), EnrollmentCapacity = request.EnrollmentCapacity };
        await db.CourseOpeningRequests.InsertOneAsync(item);
        await notifications.NotifyAdminsAsync("course-opening.created", "Đề xuất mở lớp mới",
            $"{teacher.FullName} đề xuất lớp {item.Name}.", "/app/course-openings");
        return item;
    }

    public async Task<IReadOnlyList<CourseOpeningResponse>> ListAsync(User user)
    {
        var filter = user.Role == Roles.Admin ? Builders<CourseOpeningRequest>.Filter.Empty :
            Builders<CourseOpeningRequest>.Filter.Eq(x => x.TeacherId, user.Id);
        var rows = await db.CourseOpeningRequests.Find(filter).SortByDescending(x => x.CreatedAt).Limit(100).ToListAsync();
        var users = (await db.Users.Find(x => rows.Select(r => r.TeacherId).Contains(x.Id)).ToListAsync()).ToDictionary(x => x.Id);
        var subjects = (await db.Subjects.Find(x => rows.Select(r => r.SubjectId).Contains(x.Id)).ToListAsync()).ToDictionary(x => x.Id);
        var terms = (await db.AcademicTerms.Find(x => rows.Select(r => r.AcademicTermId).Contains(x.Id)).ToListAsync()).ToDictionary(x => x.Id);
        return rows.Select(x => new CourseOpeningResponse(x.Id.ToString(), users.GetValueOrDefault(x.TeacherId)?.FullName ?? "—",
            subjects.GetValueOrDefault(x.SubjectId)?.Name ?? "—", terms.GetValueOrDefault(x.AcademicTermId)?.Name ?? "—",
            x.Name, x.Description, x.EnrollmentCapacity, x.Status, x.DecisionComment,
            x.CreatedClassId?.ToString(), x.CreatedAt, x.ReviewedAt)).ToArray();
    }

    public async Task DecideAsync(ObjectId requestId, User admin, CourseOpeningDecisionRequest decision)
    {
        if (!decision.Approve && string.IsNullOrWhiteSpace(decision.Comment))
            throw new ArgumentException("Cần ghi lý do khi từ chối đề xuất.");
        var row = await db.CourseOpeningRequests.FindOneAndUpdateAsync(
            x => x.Id == requestId && x.Status == CourseOpeningStatuses.Pending,
            Builders<CourseOpeningRequest>.Update.Set(x => x.Status, CourseOpeningStatuses.Processing),
            new FindOneAndUpdateOptions<CourseOpeningRequest> { ReturnDocument = ReturnDocument.After });
        if (row is null) throw new InvalidOperationException("Đề xuất đã được xử lý hoặc không tồn tại.");
        ObjectId? classId = null;
        try
        {
            if (decision.Approve)
            {
                var teacherValid = await db.Users.Find(x => x.Id == row.TeacherId && x.Role == Roles.Teacher &&
                    x.IsActive && x.TeachingSubjectIds.Contains(row.SubjectId)).AnyAsync();
                var subjectValid = await db.Subjects.Find(x => x.Id == row.SubjectId && !x.IsArchived).AnyAsync();
                var termValid = await db.AcademicTerms.Find(x => x.Id == row.AcademicTermId && x.EndsAt > DateTime.UtcNow).AnyAsync();
                if (!teacherValid || !subjectValid || !termValid)
                    throw new ArgumentException("Giảng viên, môn học hoặc học kỳ không còn hợp lệ để mở lớp.");
                var classroom = await db.Classes.Find(x => !x.IsArchived && x.Name == row.Name &&
                    x.SubjectId == row.SubjectId && x.TeacherId == row.TeacherId && x.AcademicTermId == row.AcademicTermId)
                    .FirstOrDefaultAsync() ?? await classes.CreateAsync(new CreateClassViewModel { Name = row.Name,
                        Description = row.Description, SubjectId = row.SubjectId.ToString(), TeacherId = row.TeacherId.ToString(),
                        AcademicTermId = row.AcademicTermId.ToString(), EnrollmentCapacity = row.EnrollmentCapacity });
                classId = classroom.Id;
            }
            await db.CourseOpeningRequests.UpdateOneAsync(x => x.Id == row.Id,
                Builders<CourseOpeningRequest>.Update
                    .Set(x => x.Status, decision.Approve ? CourseOpeningStatuses.Approved : CourseOpeningStatuses.Rejected)
                    .Set(x => x.DecisionComment, decision.Comment.Trim()).Set(x => x.ReviewedBy, admin.Id)
                    .Set(x => x.ReviewedAt, DateTime.UtcNow).Set(x => x.CreatedClassId, classId));
        }
        catch
        {
            await db.CourseOpeningRequests.UpdateOneAsync(x => x.Id == row.Id,
                Builders<CourseOpeningRequest>.Update.Set(x => x.Status, CourseOpeningStatuses.Pending));
            throw;
        }
        await notifications.AddAsync(row.TeacherId, "course-opening.decided",
            decision.Approve ? "Đề xuất mở lớp đã được duyệt" : "Đề xuất mở lớp bị từ chối",
            decision.Approve ? $"Lớp {row.Name} đã được tạo." : decision.Comment.Trim(),
            classId.HasValue ? $"/app/classes/{classId}" : "/app/course-openings");
    }
}
