using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class ScheduleService(MongoContext db)
{
    public async Task<ClassSession?> GetSessionAsync(string id) =>
        ObjectId.TryParse(id, out var sessionId)
            ? await db.ClassSessions.Find(x => x.Id == sessionId).FirstOrDefaultAsync()
            : null;

    public async Task<(TeacherAvailability? Availability, string? Error)> AddAvailabilityAsync(ObjectId teacherId, TeacherAvailabilityViewModel request)
    {
        var startAt = request.StartAt.UtcDateTime;
        var endAt = request.EndAt.UtcDateTime;
        if (startAt <= DateTime.UtcNow || endAt <= startAt || endAt - startAt > TimeSpan.FromHours(8))
            return (null, "Chọn thời gian bắt đầu trong tương lai, thời lượng ít nhất 1 phút và không quá 8 tiếng.");

        var existing = await db.TeacherAvailabilities.Find(x =>
            x.TeacherId == teacherId &&
            x.EndAt > DateTime.UtcNow &&
            x.Status != ScheduleStatuses.Rejected &&
            x.Status != ScheduleStatuses.Cancelled).ToListAsync();
        if (existing.Any(x => x.StartAt < endAt && x.EndAt > startAt))
            return (null, "Khung giờ này bị trùng với một lịch rảnh đã gửi hoặc đã phân công.");

        var availability = new TeacherAvailability
        {
            TeacherId = teacherId,
            StartAt = startAt,
            EndAt = endAt,
            Note = request.Note.Trim()
        };
        await db.TeacherAvailabilities.InsertOneAsync(availability);
        return (availability, null);
    }

    public Task<List<TeacherAvailability>> GetAvailabilitiesAsync(ObjectId? teacherId = null) =>
        db.TeacherAvailabilities
            .Find(teacherId.HasValue ? x => x.TeacherId == teacherId.Value : _ => true)
            .SortBy(x => x.StartAt)
            .ToListAsync();

    public async Task<SessionDetailsViewModel?> GetSessionDetailsAsync(string id, User viewer)
    {
        if (!ObjectId.TryParse(id, out var sessionId))
            return null;
        var session = await db.ClassSessions.Find(x => x.Id == sessionId).FirstOrDefaultAsync();
        if (session is null)
            return null;
        var classroom = await db.Classes.Find(x => x.Id == session.ClassId).FirstOrDefaultAsync();
        if (classroom is null)
            return null;

        var canManage = viewer.Role == Roles.Admin || (viewer.Role == Roles.Teacher && viewer.Id == session.TeacherId);
        var hasEnrollment = viewer.Role == Roles.Student &&
            await db.SessionEnrollments.Find(x =>
                x.SessionId == sessionId &&
                x.StudentId == viewer.Id).AnyAsync();
        if (!canManage && !hasEnrollment &&
            (viewer.Role != Roles.Student || !classroom.StudentIds.Contains(viewer.Id)))
            return null;

        var enrollments = await db.SessionEnrollments.Find(x =>
            x.SessionId == sessionId &&
            (canManage || x.StudentId == viewer.Id)).ToListAsync();
        var enrolledStudentIds = enrollments.Select(x => x.StudentId).Distinct().ToArray();
        var students = canManage && enrolledStudentIds.Length > 0
            ? await db.Users.Find(Builders<User>.Filter.In(x => x.Id, enrolledStudentIds)).ToListAsync()
            : new List<User>();
        var studentById = students.ToDictionary(x => x.Id);
        var teacher = await db.Users.Find(x => x.Id == session.TeacherId).FirstOrDefaultAsync();
        var activeCount = session.EnrolledCount;
        var sessionSubjectId = session.SubjectId == ObjectId.Empty ? classroom.SubjectId : session.SubjectId;
        var sessionSubject = await db.Subjects.Find(x => x.Id == sessionSubjectId).FirstOrDefaultAsync();

        return new SessionDetailsViewModel
        {
            Session = new SessionListItem
            {
                Id = session.Id.ToString(),
                ClassId = classroom.Id.ToString(),
                SubjectId = sessionSubjectId.ToString(),
                SubjectName = sessionSubject?.Name ?? "Môn học",
                ClassName = classroom.Name,
                TeacherName = teacher?.FullName ?? "Teacher",
                StartAt = AsUtcOffset(session.StartAt),
                EndAt = AsUtcOffset(session.EndAt),
                Capacity = session.Capacity,
                EnrolledCount = activeCount,
                IsEnrolled = enrollments.Any(x => x.Status == ScheduleStatuses.Active && x.StudentId == viewer.Id),
                Status = session.Status,
                CancellationReason = session.CancellationReason
            },
            Students = canManage
                ? enrollments.Select(enrollment => new SessionStudentViewModel
                {
                    FullName = studentById.GetValueOrDefault(enrollment.StudentId)?.FullName ?? "Unknown",
                    Email = studentById.GetValueOrDefault(enrollment.StudentId)?.Email ?? "",
                    Status = enrollment.Status,
                    EnrolledAt = AsUtcOffset(enrollment.EnrolledAt)
                }).ToList()
                : Array.Empty<SessionStudentViewModel>()
        };
    }

    public async Task<bool> ReviewAvailabilityAsync(string id, ObjectId reviewerId, bool approve)
    {
        if (!ObjectId.TryParse(id, out var availabilityId))
            return false;

        var availability = await db.TeacherAvailabilities
            .Find(x => x.Id == availabilityId && x.Status == ScheduleStatuses.Pending)
            .FirstOrDefaultAsync();
        if (availability is null)
            return false;
        if (approve && availability.StartAt <= DateTime.UtcNow)
            return false;

        var result = await db.TeacherAvailabilities.UpdateOneAsync(
            x => x.Id == availabilityId && x.Status == ScheduleStatuses.Pending,
            Builders<TeacherAvailability>.Update
                .Set(x => x.Status, approve ? ScheduleStatuses.Approved : ScheduleStatuses.Rejected)
                .Set(x => x.ReviewedBy, reviewerId)
                .Set(x => x.ReviewedAt, DateTime.UtcNow));
        return result.ModifiedCount == 1;
    }

    public async Task<bool> CancelAvailabilityAsync(string id, ObjectId teacherId)
    {
        if (!ObjectId.TryParse(id, out var availabilityId))
            return false;
        var result = await db.TeacherAvailabilities.UpdateOneAsync(
            x => x.Id == availabilityId &&
                x.TeacherId == teacherId &&
                x.Status == ScheduleStatuses.Pending,
            Builders<TeacherAvailability>.Update
                .Set(x => x.Status, ScheduleStatuses.Cancelled)
                .Set(x => x.ReviewedAt, DateTime.UtcNow));
        return result.ModifiedCount == 1;
    }

    public async Task<(ClassSession? Session, string? Error)> CreateSessionAsync(
        string classId,
        string subjectId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        int capacity,
        ObjectId adminId)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId) ||
            !ObjectId.TryParse(subjectId, out var subjectObjectId))
            return (null, "Mã lớp hoặc môn học không hợp lệ.");

        var classroom = await db.Classes.Find(x => x.Id == classObjectId && !x.IsArchived).FirstOrDefaultAsync();
        if (classroom is null)
            return (null, "Không tìm thấy lớp học.");
        if (classroom.SubjectId != subjectObjectId)
            return (null, "Môn học không khớp với môn đã gán cho lớp.");
        var subjectExists = await db.Subjects.Find(x => x.Id == subjectObjectId && !x.IsArchived).AnyAsync();
        if (!subjectExists)
            return (null, "Môn học không tồn tại hoặc đã bị lưu trữ.");
        var teacherCanTeach = await db.Users.Find(Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(x => x.Id, classroom.TeacherId),
            Builders<User>.Filter.Eq(x => x.Role, Roles.Teacher),
            Builders<User>.Filter.Eq(x => x.IsActive, true),
            Builders<User>.Filter.AnyEq(x => x.TeachingSubjectIds, subjectObjectId))).AnyAsync();
        if (!teacherCanTeach)
            return (null, "Giảng viên chưa được phân công dạy môn học này.");
        var startUtc = startAt.UtcDateTime;
        var endUtc = endAt.UtcDateTime;
        if (startUtc <= DateTime.UtcNow || endUtc - startUtc < TimeSpan.FromMinutes(1) ||
            endUtc - startUtc > TimeSpan.FromHours(8))
            return (null, "Buổi học phải ở tương lai, dài ít nhất 1 phút và không quá 8 tiếng.");
        if (capacity is < 1 or > 500)
            return (null, "Sức chứa phải nằm trong khoảng từ 1 đến 500.");

        var overlapsTeacher = await db.ClassSessions.Find(x =>
            x.TeacherId == classroom.TeacherId &&
            x.Status == ScheduleStatuses.Scheduled &&
            x.StartAt < endUtc &&
            x.EndAt > startUtc).AnyAsync();
        if (overlapsTeacher)
            return (null, "Giảng viên đã có buổi học khác trùng thời gian.");

        var overlapsClass = await db.ClassSessions.Find(x =>
            x.ClassId == classObjectId &&
            x.Status == ScheduleStatuses.Scheduled &&
            x.StartAt < endUtc &&
            x.EndAt > startUtc).AnyAsync();
        if (overlapsClass)
            return (null, "Lớp đã có buổi học khác trùng thời gian.");

        var session = new ClassSession
        {
            ClassId = classObjectId,
            TeacherId = classroom.TeacherId,
            SubjectId = subjectObjectId,
            StartAt = startUtc,
            EndAt = endUtc,
            Capacity = capacity,
            CreatedBy = adminId
        };
        await db.ClassSessions.InsertOneAsync(session);
        return (session, null);
    }

    public async Task<List<SessionListItem>> GetSessionsAsync(User user)
    {
        var studentEnrollments = user.Role == Roles.Student
            ? await db.SessionEnrollments.Find(x => x.StudentId == user.Id).ToListAsync()
            : new List<SessionEnrollment>();
        var enrolledSessionIds = studentEnrollments.Select(x => x.SessionId).ToHashSet();
        var activeEnrollmentIds = studentEnrollments
            .Where(x => x.Status == ScheduleStatuses.Active)
            .Select(x => x.SessionId)
            .ToHashSet();
        var classes = await new ClassService(db).GetForUserAsync(user);
        var classIds = classes.Select(x => x.Id).ToHashSet();

        FilterDefinition<ClassSession> sessionFilter = user.Role switch
        {
            Roles.Admin => Builders<ClassSession>.Filter.Empty,
            Roles.Teacher => Builders<ClassSession>.Filter.Eq(x => x.TeacherId, user.Id),
            Roles.Student => Builders<ClassSession>.Filter.Or(
                Builders<ClassSession>.Filter.And(
                    Builders<ClassSession>.Filter.In(x => x.ClassId, classIds),
                    Builders<ClassSession>.Filter.Eq(x => x.Status, ScheduleStatuses.Scheduled)),
                Builders<ClassSession>.Filter.In(x => x.Id, enrolledSessionIds)),
            _ => Builders<ClassSession>.Filter.Eq(x => x.Id, ObjectId.Empty)
        };
        var sessions = await db.ClassSessions.Find(sessionFilter)
            .SortBy(x => x.StartAt)
            .ToListAsync();

        var sessionClassIds = sessions.Select(x => x.ClassId).Distinct().ToArray();
        var sessionClasses = await db.Classes.Find(
            Builders<ClassRoom>.Filter.In(x => x.Id, sessionClassIds)).ToListAsync();
        var classNames = sessionClasses.ToDictionary(x => x.Id, x => x.Name);
        var classById = sessionClasses.ToDictionary(x => x.Id);
        var sessionSubjectIds = sessions.ToDictionary(
            session => session.Id,
            session => session.SubjectId == ObjectId.Empty && classById.TryGetValue(session.ClassId, out var classroom)
                ? classroom.SubjectId
                : session.SubjectId);
        var distinctSubjectIds = sessionSubjectIds.Values.Distinct().ToArray();
        var subjectList = distinctSubjectIds.Length == 0
            ? new List<Subject>()
            : await db.Subjects.Find(Builders<Subject>.Filter.In(x => x.Id, distinctSubjectIds)).ToListAsync();
        var subjectNames = subjectList.ToDictionary(x => x.Id, x => x.Name);
        var teacherIds = sessions.Select(x => x.TeacherId).Distinct().ToArray();
        var teachers = await db.Users.Find(
            Builders<User>.Filter.In(x => x.Id, teacherIds)).ToListAsync();
        var teacherNames = teachers.ToDictionary(x => x.Id, x => x.FullName);
        return sessions.Select(session => new SessionListItem
        {
            Id = session.Id.ToString(),
            ClassId = session.ClassId.ToString(),
            SubjectId = sessionSubjectIds[session.Id].ToString(),
            SubjectName = subjectNames.GetValueOrDefault(sessionSubjectIds[session.Id], "Môn học"),
            ClassName = classNames.GetValueOrDefault(session.ClassId, "Class"),
            TeacherName = teacherNames.GetValueOrDefault(session.TeacherId, "Teacher"),
            StartAt = new DateTimeOffset(DateTime.SpecifyKind(session.StartAt, DateTimeKind.Utc)),
            EndAt = new DateTimeOffset(DateTime.SpecifyKind(session.EndAt, DateTimeKind.Utc)),
            Capacity = session.Capacity,
            EnrolledCount = session.EnrolledCount,
            IsEnrolled = activeEnrollmentIds.Contains(session.Id),
            Status = session.Status,
            CancellationReason = session.CancellationReason
        }).ToList();
    }

    public async Task<(bool Success, string? Error)> EnrollAsync(string sessionId, User student)
    {
        if (!ObjectId.TryParse(sessionId, out var sessionObjectId))
            return (false, "Không tìm thấy buổi học.");
        var session = await db.ClassSessions.Find(x =>
            x.Id == sessionObjectId &&
            x.Status == ScheduleStatuses.Scheduled &&
            x.StartAt > DateTime.UtcNow).FirstOrDefaultAsync();
        if (session is null)
            return (false, "Buổi học không còn mở đăng ký.");

        var classroom = await db.Classes.Find(x => x.Id == session.ClassId).FirstOrDefaultAsync();
        if (classroom is null || !classroom.StudentIds.Contains(student.Id))
            return (false, "Bạn chưa được ghi danh vào lớp này.");

        var existing = await db.SessionEnrollments.Find(x =>
            x.SessionId == sessionObjectId &&
            x.StudentId == student.Id).FirstOrDefaultAsync();
        if (existing is { Status: ScheduleStatuses.Active })
            return (false, "Bạn đã đăng ký buổi học này.");

        var currentEnrollments = await db.SessionEnrollments.Find(x =>
            x.StudentId == student.Id &&
            x.Status == ScheduleStatuses.Active).ToListAsync();
        var otherSessionIds = currentEnrollments
            .Where(x => x.SessionId != sessionObjectId)
            .Select(x => x.SessionId)
            .Distinct()
            .ToArray();
        var conflictingSessions = await db.ClassSessions.Find(
            Builders<ClassSession>.Filter.And(
                Builders<ClassSession>.Filter.In(x => x.Id, otherSessionIds),
                Builders<ClassSession>.Filter.Eq(x => x.Status, ScheduleStatuses.Scheduled),
                Builders<ClassSession>.Filter.Lt(x => x.StartAt, session.EndAt),
                Builders<ClassSession>.Filter.Gt(x => x.EndAt, session.StartAt))).AnyAsync();
        if (conflictingSessions)
            return (false, "Buổi học bị trùng với lịch bạn đã đăng ký.");

        var seatReserved = await db.ClassSessions.UpdateOneAsync(
            x => x.Id == sessionObjectId &&
                x.Status == ScheduleStatuses.Scheduled &&
                x.StartAt > DateTime.UtcNow &&
                x.EnrolledCount < x.Capacity,
            Builders<ClassSession>.Update.Inc(x => x.EnrolledCount, 1));
        if (seatReserved.ModifiedCount != 1)
            return (false, "Buổi học đã đủ số lượng học viên.");

        try
        {
            if (existing is not null)
            {
                var reactivated = await db.SessionEnrollments.UpdateOneAsync(
                    x => x.Id == existing.Id && x.Status == ScheduleStatuses.Cancelled,
                    Builders<SessionEnrollment>.Update
                        .Set(x => x.Status, ScheduleStatuses.Active)
                        .Set(x => x.EnrolledAt, DateTime.UtcNow)
                        .Unset(x => x.CancelledAt));
                if (reactivated.ModifiedCount != 1)
                {
                    await db.ClassSessions.UpdateOneAsync(
                        x => x.Id == sessionObjectId,
                        Builders<ClassSession>.Update.Inc(x => x.EnrolledCount, -1));
                    return (false, "Đăng ký đã được thay đổi trong một thao tác khác. Hãy tải lại.");
                }
            }
            else
            {
                await db.SessionEnrollments.InsertOneAsync(new SessionEnrollment
                {
                    SessionId = sessionObjectId,
                    StudentId = student.Id
                });
            }
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await db.ClassSessions.UpdateOneAsync(
                x => x.Id == sessionObjectId,
                Builders<ClassSession>.Update.Inc(x => x.EnrolledCount, -1));
            return (false, "Bạn đã đăng ký buổi học này.");
        }

        var stillOpen = await db.ClassSessions.Find(x =>
            x.Id == sessionObjectId &&
            x.Status == ScheduleStatuses.Scheduled).AnyAsync();
        if (!stillOpen)
        {
            await db.SessionEnrollments.UpdateOneAsync(
                x => x.SessionId == sessionObjectId &&
                    x.StudentId == student.Id &&
                    x.Status == ScheduleStatuses.Active,
                Builders<SessionEnrollment>.Update
                    .Set(x => x.Status, ScheduleStatuses.Cancelled)
                    .Set(x => x.CancelledAt, DateTime.UtcNow));
            return (false, "Buổi học vừa bị hủy. Hãy tải lại lịch học.");
        }
        return (true, null);
    }

    public async Task<bool> CancelEnrollmentAsync(string sessionId, ObjectId studentId)
    {
        if (!ObjectId.TryParse(sessionId, out var sessionObjectId))
            return false;
        var result = await db.SessionEnrollments.UpdateOneAsync(
            x => x.SessionId == sessionObjectId &&
                x.StudentId == studentId &&
                x.Status == ScheduleStatuses.Active,
            Builders<SessionEnrollment>.Update
                .Set(x => x.Status, ScheduleStatuses.Cancelled)
                .Set(x => x.CancelledAt, DateTime.UtcNow));
        if (result.ModifiedCount == 1)
            await db.ClassSessions.UpdateOneAsync(
                x => x.Id == sessionObjectId && x.EnrolledCount > 0,
                Builders<ClassSession>.Update.Inc(x => x.EnrolledCount, -1));
        return result.ModifiedCount == 1;
    }

    public async Task<(bool Success, string? Error)> CancelEnrollmentAsync(string sessionId, User student, bool allowPastSession = false)
    {
        if (!ObjectId.TryParse(sessionId, out var sessionObjectId))
            return (false, "Không tìm thấy buổi học.");
        var session = await db.ClassSessions.Find(x => x.Id == sessionObjectId).FirstOrDefaultAsync();
        if (session is null)
            return (false, "Không tìm thấy buổi học.");
        if (!allowPastSession && (session.Status != ScheduleStatuses.Scheduled || session.StartAt <= DateTime.UtcNow))
            return (false, "Buổi học đã bắt đầu hoặc đã bị hủy.");
        var cancelled = await CancelEnrollmentAsync(sessionId, student.Id);
        return cancelled ? (true, null) : (false, "Bạn chưa đăng ký buổi học này.");
    }

    public async Task<bool> CancelSessionAsync(string id, string reason)
    {
        if (!ObjectId.TryParse(id, out var sessionId))
            return false;
        var result = await db.ClassSessions.UpdateOneAsync(
            x => x.Id == sessionId && x.Status == ScheduleStatuses.Scheduled,
            Builders<ClassSession>.Update
                .Set(x => x.Status, ScheduleStatuses.Cancelled)
                .Set(x => x.CancelledAt, DateTime.UtcNow)
                .Set(x => x.CancellationReason, reason.Trim())
                .Set(x => x.EnrolledCount, 0));
        if (result.ModifiedCount != 1)
            return false;

        var enrollments = await db.SessionEnrollments.Find(x =>
            x.SessionId == sessionId &&
            x.Status == ScheduleStatuses.Active).ToListAsync();
        foreach (var enrollment in enrollments)
        {
            await db.SessionEnrollments.UpdateOneAsync(
                x => x.Id == enrollment.Id && x.Status == ScheduleStatuses.Active,
                Builders<SessionEnrollment>.Update
                    .Set(x => x.Status, ScheduleStatuses.Cancelled)
                    .Set(x => x.CancelledAt, DateTime.UtcNow));
        }
        return true;
    }

    public async Task<int> GetEnrollmentCountAsync(ObjectId sessionId) =>
        (int)await db.SessionEnrollments.CountDocumentsAsync(x =>
            x.SessionId == sessionId &&
            x.Status == ScheduleStatuses.Active);

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
