using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public class AcademicService(MongoContext db)
{
    public Task<List<AcademicProgram>> GetProgramsAsync() =>
        db.AcademicPrograms.Find(x => x.IsActive).SortBy(x => x.Name).ToListAsync();

    public Task<List<AcademicTerm>> GetTermsAsync() =>
        db.AcademicTerms.Find(_ => true).SortByDescending(x => x.StartsAt).ToListAsync();

    public async Task<AcademicTerm?> GetActiveTermAsync() =>
        await db.AcademicTerms.Find(x => x.IsActive).FirstOrDefaultAsync();

    public async Task<List<ProgramCourseViewModel>> GetProgramCurriculumAsync(string programId)
    {
        if (!ObjectId.TryParse(programId, out var objectId))
            return new();
        return (await db.ProgramCourses.Find(x => x.ProgramId == objectId).ToListAsync())
            .Select(x => new ProgramCourseViewModel
            {
                SubjectId = x.SubjectId.ToString(),
                Selected = true,
                SemesterNumber = x.SemesterNumber,
                Credits = x.Credits,
                IsRequired = x.IsRequired
            }).ToList();
    }

    public async Task<List<AcademicProgram>> GetAllProgramsAsync() =>
        await db.AcademicPrograms.Find(_ => true).SortBy(x => x.Name).ToListAsync();

    public async Task<Dictionary<string, int>> GetProgramCourseCountsAsync()
    {
        var courses = await db.ProgramCourses.Find(_ => true).ToListAsync();
        return courses.GroupBy(x => x.ProgramId.ToString())
            .ToDictionary(group => group.Key, group => group.Count());
    }

    public async Task<AcademicProgram?> GetProgramAsync(string? id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return null;
        return await db.AcademicPrograms.Find(x => x.Id == objectId && x.IsActive).FirstOrDefaultAsync();
    }

    public async Task<bool> SaveProgramAsync(AcademicProgram model, IReadOnlyCollection<ProgramCourseViewModel> curriculum)
    {
        model.Code = model.Code.Trim().ToUpperInvariant();
        model.Name = model.Name.Trim();
        model.Description = model.Description.Trim();
        if (model.Code.Length is < 2 or > 20 ||
            model.Name.Length is < 2 or > 120 ||
            model.Description.Length > 1000 ||
            await db.Departments.Find(x => x.Id == model.DepartmentId && !x.IsArchived).FirstOrDefaultAsync() is null)
        {
            return false;
        }

        var duplicate = await db.AcademicPrograms.Find(x => x.Code == model.Code && x.Id != model.Id).AnyAsync();
        if (duplicate)
            return false;

        if (model.Id != ObjectId.Empty)
        {
            var existing = await db.AcademicPrograms.Find(x => x.Id == model.Id).FirstOrDefaultAsync();
            if (existing is null || !existing.IsActive)
                return false;
            if (existing.DepartmentId != model.DepartmentId &&
                await db.Users.Find(x => x.ProgramId == model.Id).AnyAsync())
                return false;
        }

        var subjects = curriculum.Where(x => x.Selected)
            .GroupBy(x => x.SubjectId).Select(x => x.First()).ToList();
        if (subjects.Any(x =>
                !ObjectId.TryParse(x.SubjectId, out _) ||
                x.SemesterNumber is < 1 or > 20 ||
                x.Credits is < 1 or > 30))
        {
            return false;
        }

        var subjectIds = subjects.Select(x => ObjectId.Parse(x.SubjectId)).ToArray();
        var validSubjects = subjectIds.Length == 0
            ? new List<Subject>()
            : await db.Subjects.Find(Builders<Subject>.Filter.And(
                Builders<Subject>.Filter.Eq(x => x.DepartmentId, model.DepartmentId),
                Builders<Subject>.Filter.Eq(x => x.IsArchived, false),
                Builders<Subject>.Filter.In(x => x.Id, subjectIds))).ToListAsync();
        if (validSubjects.Count != subjectIds.Length)
            return false;

        if (model.Id == ObjectId.Empty)
        {
            model.Id = ObjectId.GenerateNewId();
            await db.AcademicPrograms.InsertOneAsync(model);
        }
        else
        {
            await db.AcademicPrograms.ReplaceOneAsync(x => x.Id == model.Id, model, new ReplaceOptions { IsUpsert = true });
        }

        await db.ProgramCourses.DeleteManyAsync(x => x.ProgramId == model.Id);
        if (subjects.Count > 0)
        {
            await db.ProgramCourses.InsertManyAsync(subjects.Select(x => new ProgramCourse
            {
                ProgramId = model.Id,
                SubjectId = ObjectId.Parse(x.SubjectId),
                SemesterNumber = x.SemesterNumber,
                Credits = x.Credits,
                IsRequired = x.IsRequired
            }));
        }

        return true;
    }

    public async Task<(bool Success, string Message)> SetProgramActiveAsync(string id, bool isActive)
    {
        if (!ObjectId.TryParse(id, out var programId))
            return (false, "Mã ngành học không hợp lệ.");

        var program = await db.AcademicPrograms.Find(x => x.Id == programId).FirstOrDefaultAsync();
        if (program is null)
            return (false, "Không tìm thấy ngành học.");
        if (program.IsActive == isActive)
            return (true, isActive ? "Ngành học đã được mở." : "Ngành học đã được lưu trữ.");

        if (!isActive && await db.Users.Find(x => x.ProgramId == programId).AnyAsync())
            return (false, "Không thể lưu trữ ngành đang được gán cho sinh viên. Hãy chuyển hồ sơ sinh viên sang ngành khác trước.");

        var update = await db.AcademicPrograms.UpdateOneAsync(
            x => x.Id == programId,
            Builders<AcademicProgram>.Update.Set(x => x.IsActive, isActive));
        return update.MatchedCount == 1
            ? (true, isActive ? "Đã mở lại ngành học." : "Đã lưu trữ ngành học.")
            : (false, "Không tìm thấy ngành học.");
    }

    public async Task<bool> SaveTermAsync(AcademicTerm model)
    {
        model.Code = model.Code.Trim().ToUpperInvariant();
        model.Name = model.Name.Trim();
        model.AcademicYear = model.AcademicYear.Trim();
        if (model.Code.Length is < 2 or > 30 ||
            model.Name.Length is < 2 or > 120 ||
            model.AcademicYear.Length is < 4 or > 12 ||
            model.SemesterNumber is < 1 or > 3 ||
            model.EndsAt <= model.StartsAt ||
            model.RegistrationOpensAt >= model.RegistrationClosesAt ||
            model.RegistrationClosesAt > model.EndsAt ||
            model.TuitionPerCredit is < 0 or > 1_000_000_000 ||
            await db.AcademicTerms.Find(x => x.Code == model.Code && x.Id != model.Id).AnyAsync())
        {
            return false;
        }

        if (model.IsActive)
            await db.AcademicTerms.UpdateManyAsync(x => x.Id != model.Id && x.IsActive,
                Builders<AcademicTerm>.Update.Set(x => x.IsActive, false));

        if (model.Id == ObjectId.Empty)
            await db.AcademicTerms.InsertOneAsync(model);
        else
            await db.AcademicTerms.ReplaceOneAsync(x => x.Id == model.Id, model, new ReplaceOptions { IsUpsert = true });
        return true;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(User student, string classId)
    {
        if (student.Role != Roles.Student || student.ProgramId is null ||
            student.CurrentSemester < 1 || !ObjectId.TryParse(classId, out var classObjectId))
            return (false, "Hồ sơ học viên chưa có chương trình hoặc học kỳ hợp lệ.");

        var term = await GetActiveTermAsync();
        var now = DateTime.UtcNow;
        if (term is null || now < term.RegistrationOpensAt || now > term.RegistrationClosesAt)
            return (false, "Hiện không nằm trong thời gian đăng ký của học kỳ.");

        var classroom = await db.Classes.Find(x =>
            x.Id == classObjectId && !x.IsArchived && x.AcademicTermId == term.Id).FirstOrDefaultAsync();
        if (classroom is null)
            return (false, "Lớp không được mở trong học kỳ hiện tại.");
        if (classroom.StudentIds.Contains(student.Id))
            return (false, "Bạn đã đăng ký lớp này rồi.");

        var existingRegistration = await db.StudentRegistrations.Find(x =>
            x.StudentId == student.Id && x.ClassId == classObjectId).FirstOrDefaultAsync();
        if (existingRegistration?.Status == StudentRegistrationStatuses.Active)
            return (false, "Bạn đã đăng ký lớp này rồi.");
        if (existingRegistration?.Status == StudentRegistrationStatuses.Pending)
            return (false, "Yêu cầu đăng ký lớp này đang chờ quản trị viên duyệt.");

        var curriculum = await db.ProgramCourses.Find(x =>
            x.ProgramId == student.ProgramId.Value &&
            x.SubjectId == classroom.SubjectId &&
            x.SemesterNumber == student.CurrentSemester).FirstOrDefaultAsync();
        if (curriculum is null)
            return (false, "Môn học không thuộc chương trình hoặc học kỳ hiện tại của bạn.");

        if (classroom.StudentIds.Count >= classroom.EnrollmentCapacity)
            return (false, "Lớp đã đủ sĩ số; hãy chọn lớp khác.");

        var overlap = await HasScheduleConflictAsync(student, classroom);
        if (overlap)
            return (false, "Lịch các buổi học của lớp bị trùng với một lớp bạn đã đăng ký.");

        await db.StudentRegistrations.UpdateOneAsync(
            x => x.StudentId == student.Id && x.ClassId == classObjectId,
            Builders<StudentRegistration>.Update
                .Set(x => x.AcademicTermId, term.Id)
                .Set(x => x.Status, StudentRegistrationStatuses.Pending)
                .Set(x => x.RegisteredAt, now)
                .Set(x => x.WithdrawnAt, (DateTime?)null)
                .SetOnInsert(x => x.StudentId, student.Id)
                .SetOnInsert(x => x.ClassId, classObjectId),
            new UpdateOptions { IsUpsert = true });

        return (true, "Đã gửi yêu cầu đăng ký. Lớp học sẽ hiển thị sau khi quản trị viên duyệt.");
    }

    public async Task<List<CourseRegistrationReviewViewModel>> GetPendingRegistrationReviewsAsync()
    {
        var registrations = await db.StudentRegistrations.Find(
            x => x.Status == StudentRegistrationStatuses.Pending).SortBy(x => x.RegisteredAt).ToListAsync();
        if (registrations.Count == 0)
            return new();

        var studentIds = registrations.Select(x => x.StudentId).Distinct().ToArray();
        var classIds = registrations.Select(x => x.ClassId).Distinct().ToArray();
        var students = await db.Users.Find(Builders<User>.Filter.In(x => x.Id, studentIds)).ToListAsync();
        var classes = await db.Classes.Find(Builders<ClassRoom>.Filter.In(x => x.Id, classIds)).ToListAsync();
        var subjectIds = classes.Select(x => x.SubjectId).Distinct().ToArray();
        var termIds = registrations.Select(x => x.AcademicTermId).Distinct().ToArray();
        var subjects = subjectIds.Length == 0
            ? new List<Subject>()
            : await db.Subjects.Find(Builders<Subject>.Filter.In(x => x.Id, subjectIds)).ToListAsync();
        var terms = termIds.Length == 0
            ? new List<AcademicTerm>()
            : await db.AcademicTerms.Find(Builders<AcademicTerm>.Filter.In(x => x.Id, termIds)).ToListAsync();
        var studentsById = students.ToDictionary(x => x.Id);
        var classesById = classes.ToDictionary(x => x.Id);
        var subjectsById = subjects.ToDictionary(x => x.Id);
        var termsById = terms.ToDictionary(x => x.Id);

        return registrations
            .Where(registration => studentsById.ContainsKey(registration.StudentId) &&
                classesById.ContainsKey(registration.ClassId))
            .Select(registration =>
            {
                var student = studentsById[registration.StudentId];
                var classroom = classesById[registration.ClassId];
                subjectsById.TryGetValue(classroom.SubjectId, out var subject);
                termsById.TryGetValue(registration.AcademicTermId, out var term);
                return new CourseRegistrationReviewViewModel
                {
                    RegistrationId = registration.Id.ToString(),
                    StudentName = student.FullName,
                    StudentEmail = student.Email,
                    ClassId = classroom.Id.ToString(),
                    ClassName = classroom.Name,
                    SubjectName = subject?.Name ?? "Môn học không xác định",
                    TermName = term?.Name ?? "Học kỳ không xác định",
                    RequestedAt = registration.RegisteredAt
                };
            }).ToList();
    }

    public async Task<Dictionary<ObjectId, string>> GetRegistrationStatusesAsync(
        ObjectId studentId,
        IReadOnlyCollection<ObjectId> classIds)
    {
        if (classIds.Count == 0)
            return new();
        var registrations = await db.StudentRegistrations.Find(Builders<StudentRegistration>.Filter.And(
            Builders<StudentRegistration>.Filter.Eq(x => x.StudentId, studentId),
            Builders<StudentRegistration>.Filter.In(x => x.ClassId, classIds))).ToListAsync();
        return registrations
            .GroupBy(x => x.ClassId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.RegisteredAt).First().Status);
    }

    public async Task<(bool Success, string Message)> ReviewRegistrationAsync(string registrationId, bool approve)
    {
        if (!ObjectId.TryParse(registrationId, out var registrationObjectId))
            return (false, "Mã yêu cầu đăng ký không hợp lệ.");

        var registration = await db.StudentRegistrations.Find(x =>
            x.Id == registrationObjectId &&
            x.Status == StudentRegistrationStatuses.Pending).FirstOrDefaultAsync();
        if (registration is null)
            return (false, "Yêu cầu không còn chờ duyệt.");

        if (!approve)
        {
            var rejected = await db.StudentRegistrations.UpdateOneAsync(
                x => x.Id == registrationObjectId && x.Status == StudentRegistrationStatuses.Pending,
                Builders<StudentRegistration>.Update.Set(x => x.Status, StudentRegistrationStatuses.Rejected));
            return rejected.ModifiedCount == 1
                ? (true, "Đã từ chối yêu cầu đăng ký.")
                : (false, "Yêu cầu vừa được người khác xử lý. Hãy tải lại trang.");
        }

        var student = await db.Users.Find(x =>
            x.Id == registration.StudentId && x.Role == Roles.Student && x.IsActive).FirstOrDefaultAsync();
        var classroom = await db.Classes.Find(x =>
            x.Id == registration.ClassId && !x.IsArchived).FirstOrDefaultAsync();
        var term = await db.AcademicTerms.Find(x => x.Id == registration.AcademicTermId).FirstOrDefaultAsync();
        if (student?.ProgramId is not { } programId || classroom is null || term is null ||
            classroom.AcademicTermId != term.Id)
            return (false, "Không thể duyệt: hồ sơ sinh viên, lớp hoặc học kỳ không còn hợp lệ.");

        var curriculum = await db.ProgramCourses.Find(x =>
            x.ProgramId == programId &&
            x.SubjectId == classroom.SubjectId &&
            x.SemesterNumber == student.CurrentSemester).FirstOrDefaultAsync();
        if (curriculum is null)
            return (false, "Không thể duyệt: môn học không còn thuộc chương trình hiện tại của sinh viên.");
        if (classroom.StudentIds.Contains(student.Id))
            return (false, "Sinh viên đã được ghi danh trực tiếp vào lớp; hãy tải lại danh sách.");
        if (await HasScheduleConflictAsync(student, classroom))
            return (false, "Không thể duyệt do lịch học bị trùng với lớp sinh viên đang theo học.");

        var classFilter = Builders<ClassRoom>.Filter.And(
            Builders<ClassRoom>.Filter.Eq(x => x.Id, classroom.Id),
            Builders<ClassRoom>.Filter.Eq(x => x.AcademicTermId, term.Id),
            Builders<ClassRoom>.Filter.Eq(x => x.IsArchived, false),
            Builders<ClassRoom>.Filter.Not(Builders<ClassRoom>.Filter.AnyEq(x => x.StudentIds, student.Id)),
            new BsonDocumentFilterDefinition<ClassRoom>(new BsonDocument("$expr", new BsonDocument("$lt", new BsonArray
            {
                new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray { "$StudentIds", new BsonArray() })),
                new BsonDocument("$ifNull", new BsonArray { "$EnrollmentCapacity", 40 })
            }))));
        var addStudent = await db.Classes.UpdateOneAsync(classFilter,
            Builders<ClassRoom>.Update.AddToSet(x => x.StudentIds, student.Id));
        if (addStudent.ModifiedCount != 1)
            return (false, "Lớp đã đủ sĩ số hoặc vừa thay đổi; yêu cầu vẫn đang chờ duyệt.");

        var activate = await db.StudentRegistrations.UpdateOneAsync(
            x => x.Id == registrationObjectId && x.Status == StudentRegistrationStatuses.Pending,
            Builders<StudentRegistration>.Update.Set(x => x.Status, StudentRegistrationStatuses.Active));
        if (activate.ModifiedCount != 1)
        {
            await db.Classes.UpdateOneAsync(x => x.Id == classroom.Id,
                Builders<ClassRoom>.Update.Pull(x => x.StudentIds, student.Id));
            return (false, "Yêu cầu vừa được người khác xử lý; thay đổi ghi danh đã được hoàn tác.");
        }

        await CreateTuitionInvoiceAsync(student.Id, classroom, term, curriculum.Credits);
        return (true, "Đã duyệt yêu cầu và ghi danh sinh viên vào lớp.");
    }

    private async Task CreateTuitionInvoiceAsync(ObjectId studentId, ClassRoom classroom, AcademicTerm term, int credits)
    {
        var now = DateTime.UtcNow;
        var amount = term.TuitionPerCredit > 0 && credits > 0
            ? decimal.ToInt64(decimal.Round(term.TuitionPerCredit * credits, 0, MidpointRounding.AwayFromZero))
            : 0;
        await db.TuitionInvoices.UpdateOneAsync(
            x => x.StudentId == studentId && x.ClassId == classroom.Id,
            Builders<TuitionInvoice>.Update
                .SetOnInsert(x => x.StudentId, studentId)
                .SetOnInsert(x => x.ClassId, classroom.Id)
                .SetOnInsert(x => x.AcademicTermId, term.Id)
                .SetOnInsert(x => x.Credits, credits)
                .SetOnInsert(x => x.TuitionPerCredit, term.TuitionPerCredit)
                .SetOnInsert(x => x.AmountVnd, amount)
                .SetOnInsert(x => x.Status, TuitionInvoiceStatuses.Pending)
                .SetOnInsert(x => x.CreatedAt, now)
                .SetOnInsert(x => x.UpdatedAt, now),
            new UpdateOptions { IsUpsert = true });

        await db.TuitionInvoices.UpdateOneAsync(
            x => x.StudentId == studentId &&
                x.ClassId == classroom.Id &&
                x.Status == TuitionInvoiceStatuses.Cancelled,
            Builders<TuitionInvoice>.Update
                .Set(x => x.AcademicTermId, term.Id)
                .Set(x => x.Credits, credits)
                .Set(x => x.TuitionPerCredit, term.TuitionPerCredit)
                .Set(x => x.AmountVnd, amount)
                .Set(x => x.Status, TuitionInvoiceStatuses.Pending)
                .Set(x => x.VnpayTransactionReference, "")
                .Set(x => x.VnpayTransactionNumber, "")
                .Set(x => x.PaymentInitiatedAt, (DateTime?)null)
                .Set(x => x.PaidAt, (DateTime?)null)
                .Set(x => x.UpdatedAt, now));
    }

    public async Task<(bool Success, string Message)> WithdrawAsync(User student, string classId)
    {
        if (student.Role != Roles.Student || !ObjectId.TryParse(classId, out var classObjectId))
            return (false, "Thông tin đăng ký không hợp lệ.");

        var registrationRequest = await db.StudentRegistrations.Find(x =>
            x.StudentId == student.Id && x.ClassId == classObjectId &&
            x.Status == StudentRegistrationStatuses.Pending).FirstOrDefaultAsync();
        if (registrationRequest is not null)
        {
            await db.StudentRegistrations.UpdateOneAsync(
                x => x.Id == registrationRequest.Id && x.Status == StudentRegistrationStatuses.Pending,
                Builders<StudentRegistration>.Update
                    .Set(x => x.Status, StudentRegistrationStatuses.Withdrawn)
                    .Set(x => x.WithdrawnAt, DateTime.UtcNow));
            return (true, "Đã hủy yêu cầu đăng ký đang chờ duyệt.");
        }

        var classroom = await db.Classes.Find(Builders<ClassRoom>.Filter.And(
            Builders<ClassRoom>.Filter.Eq(x => x.Id, classObjectId),
            Builders<ClassRoom>.Filter.Eq(x => x.IsArchived, false),
            Builders<ClassRoom>.Filter.AnyEq(x => x.StudentIds, student.Id))).FirstOrDefaultAsync();
        if (classroom?.AcademicTermId is not { } termId)
            return (false, "Không tìm thấy đăng ký đang hoạt động.");

        var term = await db.AcademicTerms.Find(x => x.Id == termId).FirstOrDefaultAsync();
        if (term is null || DateTime.UtcNow > term.RegistrationClosesAt)
            return (false, "Không thể rút lớp sau khi hết thời gian đăng ký.");

        var activeSession = await db.ClassSessions.Find(x =>
            x.ClassId == classObjectId &&
            x.Status == ScheduleStatuses.Scheduled &&
            x.EndAt > DateTime.UtcNow).AnyAsync();
        if (activeSession)
            return (false, "Không thể rút lớp khi lớp còn buổi học đã lên lịch; liên hệ quản trị viên.");

        var paidInvoice = await db.TuitionInvoices.Find(x =>
            x.StudentId == student.Id &&
            x.ClassId == classObjectId &&
            x.Status == TuitionInvoiceStatuses.Paid).AnyAsync();
        if (paidInvoice)
            return (false, "Học phí môn này đã thanh toán. Vui lòng liên hệ quản trị viên để xử lý hoàn phí trước khi rút lớp.");

        await db.Classes.UpdateOneAsync(x => x.Id == classObjectId,
            Builders<ClassRoom>.Update.Pull(x => x.StudentIds, student.Id));
        await db.StudentRegistrations.UpdateOneAsync(
            x => x.StudentId == student.Id && x.ClassId == classObjectId && x.Status == "Active",
            Builders<StudentRegistration>.Update
                .Set(x => x.Status, StudentRegistrationStatuses.Withdrawn)
                .Set(x => x.WithdrawnAt, DateTime.UtcNow));
        await db.TuitionInvoices.UpdateManyAsync(x =>
                x.StudentId == student.Id &&
                x.ClassId == classObjectId &&
                x.Status == TuitionInvoiceStatuses.Pending,
            Builders<TuitionInvoice>.Update
                .Set(x => x.Status, TuitionInvoiceStatuses.Cancelled)
                .Set(x => x.UpdatedAt, DateTime.UtcNow));
        return (true, "Đã rút khỏi lớp.");
    }

    public async Task<List<(ClassRoom Class, Subject Subject, AcademicTerm Term)>> GetEligibleClassesAsync(User student)
    {
        if (student.ProgramId is null)
            return new();
        var term = await GetActiveTermAsync();
        var now = DateTime.UtcNow;
        if (term is null || now < term.RegistrationOpensAt || now > term.RegistrationClosesAt)
            return new();

        var curriculum = await db.ProgramCourses.Find(x =>
            x.ProgramId == student.ProgramId.Value &&
            x.SemesterNumber == student.CurrentSemester).ToListAsync();
        var subjectIds = curriculum.Select(x => x.SubjectId).ToArray();
        if (subjectIds.Length == 0)
            return new();

        var classFilter = Builders<ClassRoom>.Filter.And(
            Builders<ClassRoom>.Filter.Eq(x => x.IsArchived, false),
            Builders<ClassRoom>.Filter.Eq(x => x.AcademicTermId, (ObjectId?)term.Id),
            Builders<ClassRoom>.Filter.In(x => x.SubjectId, subjectIds));
        var classes = await db.Classes.Find(classFilter).ToListAsync();
        var subjects = await db.Subjects.Find(
            Builders<Subject>.Filter.In(x => x.Id, subjectIds)).ToListAsync();
        var subjectById = subjects.ToDictionary(x => x.Id);
        return classes.Where(x => subjectById.ContainsKey(x.SubjectId))
            .Select(x => (x, subjectById[x.SubjectId], term))
            .ToList();
    }

    private async Task<bool> HasScheduleConflictAsync(User student, ClassRoom candidate)
    {
        var enrolledClasses = await db.Classes.Find(Builders<ClassRoom>.Filter.And(
            Builders<ClassRoom>.Filter.AnyEq(x => x.StudentIds, student.Id),
            Builders<ClassRoom>.Filter.Eq(x => x.AcademicTermId, candidate.AcademicTermId),
            Builders<ClassRoom>.Filter.Eq(x => x.IsArchived, false))).ToListAsync();
        var enrolledClassIds = enrolledClasses.Select(x => x.Id).ToArray();
        if (enrolledClassIds.Length == 0)
            return false;

        var now = DateTime.UtcNow;
        var registeredSessions = await db.ClassSessions.Find(Builders<ClassSession>.Filter.And(
            Builders<ClassSession>.Filter.In(x => x.ClassId, enrolledClassIds),
            Builders<ClassSession>.Filter.Eq(x => x.Status, ScheduleStatuses.Scheduled),
            Builders<ClassSession>.Filter.Gt(x => x.EndAt, now))).ToListAsync();
        if (registeredSessions.Count == 0)
            return false;

        var candidateSessions = await db.ClassSessions.Find(Builders<ClassSession>.Filter.And(
            Builders<ClassSession>.Filter.Eq(x => x.ClassId, candidate.Id),
            Builders<ClassSession>.Filter.Eq(x => x.Status, ScheduleStatuses.Scheduled),
            Builders<ClassSession>.Filter.Gt(x => x.EndAt, now))).ToListAsync();
        return candidateSessions.Any(candidateSession =>
            registeredSessions.Any(existing =>
                candidateSession.StartAt < existing.EndAt &&
                candidateSession.EndAt > existing.StartAt));
    }
}
