using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public static class DemoDataSeeder
{
    private const int SampleCount = 20;
    private const string DemoPassword = "Demo@123";

    public static async Task InitializeAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DemoData:Enabled"))
            return;

        var db = scope.ServiceProvider.GetRequiredService<MongoContext>();
        var now = DateTime.UtcNow;
        var departments = new List<Department>();
        var programs = new List<AcademicProgram>();
        var subjects = new List<Subject>();

        for (var index = 1; index <= SampleCount; index++)
        {
            var suffix = index.ToString("D2");
            var department = await GetOrCreateAsync(
                db.Departments,
                x => x.Code == $"DEMO-D{suffix}",
                new Department
                {
                    Code = $"DEMO-D{suffix}",
                    Name = $"DEMO - Khoa mau {suffix}",
                    Description = "Du lieu minh hoa, khong phai danh muc chinh thuc cua NEU."
                });
            departments.Add(department);
        }

        var sampleDepartment = departments[0];
        for (var index = 1; index <= SampleCount; index++)
        {
            var suffix = index.ToString("D2");
            var programDepartment = departments[index - 1];
            var program = await GetOrCreateAsync(
                db.AcademicPrograms,
                x => x.Code == $"DEMO-P{suffix}",
                new AcademicProgram
                {
                    DepartmentId = programDepartment.Id,
                    Code = $"DEMO-P{suffix}",
                    Name = $"DEMO - Chuong trinh mau {suffix}",
                    Description = "Chuong trinh du lieu minh hoa de thu nghiem danh muc va phan trang."
                });
            programs.Add(program);

            var subject = await GetOrCreateAsync(
                db.Subjects,
                x => x.Code == $"DEMO-S{suffix}",
                new Subject
                {
                    DepartmentId = departments[index - 1].Id,
                    Code = $"DEMO-S{suffix}",
                    Name = $"DEMO - Hoc phan mau {suffix}",
                    Description = "Hoc phan mau 3 tin chi, chi dung de thu nghiem LMS."
                });
            subjects.Add(subject);

            await db.ProgramCourses.UpdateOneAsync(
                x => x.ProgramId == program.Id && x.SubjectId == subject.Id,
                Builders<ProgramCourse>.Update
                    .SetOnInsert(x => x.ProgramId, program.Id)
                    .SetOnInsert(x => x.SubjectId, subject.Id)
                    .SetOnInsert(x => x.SemesterNumber, 1)
                    .SetOnInsert(x => x.Credits, 3)
                    .SetOnInsert(x => x.IsRequired, true),
                new UpdateOptions { IsUpsert = true });
        }

        var primaryProgram = programs[0];

        var term = await GetOrCreateAsync(
            db.AcademicTerms,
            x => x.Code == "DEMO-2026-1",
            new AcademicTerm
            {
                Code = "DEMO-2026-1",
                Name = "DEMO - Hoc ky mau 1",
                AcademicYear = "2026-2027",
                SemesterNumber = 1,
                StartsAt = now.AddDays(-14),
                EndsAt = now.AddMonths(5),
                RegistrationOpensAt = now.AddDays(-1),
                RegistrationClosesAt = now.AddDays(45),
                TuitionPerCredit = 400_000m,
                IsActive = true
            });
        await db.AcademicTerms.UpdateManyAsync(
            x => x.Id != term.Id && x.IsActive,
            Builders<AcademicTerm>.Update.Set(x => x.IsActive, false));
        await db.AcademicTerms.UpdateOneAsync(
            x => x.Id == term.Id,
            Builders<AcademicTerm>.Update
                .Set(x => x.IsActive, true)
                .Set(x => x.RegistrationOpensAt, now.AddDays(-1))
                .Set(x => x.RegistrationClosesAt, now.AddDays(45)));

        var admin = await db.Users.Find(x => x.Role == Roles.Admin).FirstOrDefaultAsync();
        var adminId = admin?.Id ?? ObjectId.Empty;
        var teachers = new List<User>();
        var students = new List<User>();
        for (var index = 1; index <= SampleCount; index++)
        {
            var suffix = index.ToString("D2");
            var teacherEmail = $"demo.teacher{suffix}@example.test";
            var teacher = await GetOrCreateDemoUserAsync(
                db,
                teacherEmail,
                $"DEMO Teacher {suffix}",
                Roles.Teacher,
                sampleDepartment.Id,
                null,
                new[] { subjects[0].Id });
            teachers.Add(teacher);

            var studentEmail = $"demo.student{suffix}@example.test";
            var student = await GetOrCreateDemoUserAsync(
                db,
                studentEmail,
                $"DEMO Student {suffix}",
                Roles.Student,
                sampleDepartment.Id,
                primaryProgram.Id,
                Array.Empty<ObjectId>());
            students.Add(student);
        }
        var sessionCreatorId = adminId != ObjectId.Empty ? adminId : teachers[0].Id;

        var existingDemoStudent = await db.Users.Find(x =>
            x.Email == "student@lms.com" && x.Role == Roles.Student).FirstOrDefaultAsync();
        if (existingDemoStudent is not null)
        {
            await db.Users.UpdateOneAsync(
                x => x.Id == existingDemoStudent.Id,
                Builders<User>.Update
                    .Set(x => x.DepartmentId, sampleDepartment.Id)
                    .Set(x => x.ProgramId, primaryProgram.Id)
                    .Set(x => x.CurrentSemester, 1)
                    .Set(x => x.AcademicProfileInitialized, true));
        }

        var classes = new List<ClassRoom>();
        for (var index = 1; index <= SampleCount; index++)
        {
            var suffix = index.ToString("D2");
            var classroom = await GetOrCreateAsync(
                db.Classes,
                x => x.Name == $"DEMO - Lop {suffix}",
                new ClassRoom
                {
                    Name = $"DEMO - Lop {suffix}",
                    Description = "Lop minh hoa, du lieu demo.",
                    SubjectId = subjects[0].Id,
                    AcademicTermId = term.Id,
                    EnrollmentCapacity = 40,
                    TeacherId = teachers[index - 1].Id,
                    CreatedAt = now.AddDays(-index)
                });
            classes.Add(classroom);

            await db.Users.UpdateOneAsync(
                x => x.Id == teachers[index - 1].Id,
                Builders<User>.Update.AddToSet(x => x.TeachingSubjectIds, subjects[0].Id));

            var lesson = await GetOrCreateAsync(
                db.Lessons,
                x => x.ClassId == classroom.Id && x.Title == $"DEMO - Bai hoc {suffix}",
                new Lesson
                {
                    ClassId = classroom.Id,
                    Title = $"DEMO - Bai hoc {suffix}",
                    Content = "Noi dung minh hoa de kiem tra trang bai hoc.",
                    IsPublished = true,
                    CreatedAt = now.AddDays(-index)
                });
            _ = lesson;

            var assignment = await GetOrCreateAsync(
                db.Assignments,
                x => x.ClassId == classroom.Id && x.Title == $"DEMO - Bai tap {suffix}",
                new Assignment
                {
                    ClassId = classroom.Id,
                    Title = $"DEMO - Bai tap {suffix}",
                    Description = "Bai tap minh hoa de kiem tra nop bai va cham diem.",
                    DueDate = now.AddDays(30),
                    IsPublished = true,
                    CreatedAt = now.AddDays(-index)
                });
            await db.Classes.UpdateOneAsync(
                x => x.Id == classroom.Id,
                Builders<ClassRoom>.Update.AddToSet(x => x.StudentIds, students[index - 1].Id));
            await db.StudentRegistrations.UpdateOneAsync(
                x => x.StudentId == students[index - 1].Id && x.ClassId == classroom.Id,
                Builders<StudentRegistration>.Update
                    .SetOnInsert(x => x.StudentId, students[index - 1].Id)
                    .SetOnInsert(x => x.ClassId, classroom.Id)
                    .SetOnInsert(x => x.AcademicTermId, term.Id)
                    .SetOnInsert(x => x.Status, "Active")
                    .SetOnInsert(x => x.RegisteredAt, now.AddDays(-index)),
                new UpdateOptions { IsUpsert = true });

            var invoiceAmount = checked((long)(term.TuitionPerCredit * 3));
            await db.TuitionInvoices.UpdateOneAsync(
                x => x.StudentId == students[index - 1].Id && x.ClassId == classroom.Id,
                Builders<TuitionInvoice>.Update
                    .SetOnInsert(x => x.StudentId, students[index - 1].Id)
                    .SetOnInsert(x => x.ClassId, classroom.Id)
                    .SetOnInsert(x => x.AcademicTermId, term.Id)
                    .SetOnInsert(x => x.Credits, 3)
                    .SetOnInsert(x => x.TuitionPerCredit, term.TuitionPerCredit)
                    .SetOnInsert(x => x.AmountVnd, invoiceAmount)
                    .SetOnInsert(x => x.Status, TuitionInvoiceStatuses.Pending)
                    .SetOnInsert(x => x.CreatedAt, now.AddDays(-index))
                    .SetOnInsert(x => x.UpdatedAt, now.AddDays(-index)),
                new UpdateOptions { IsUpsert = true });

            var availability = await GetOrCreateAsync(
                db.TeacherAvailabilities,
                x => x.TeacherId == teachers[index - 1].Id && x.Note == $"DEMO availability {suffix}",
                new TeacherAvailability
                {
                    TeacherId = teachers[index - 1].Id,
                    StartAt = now.AddDays(index + 1),
                    EndAt = now.AddDays(index + 1).AddHours(2),
                    Note = $"DEMO availability {suffix}",
                    Status = ScheduleStatuses.Pending,
                    CreatedAt = now.AddDays(-index)
                });

            var session = await GetOrCreateAsync(
                db.ClassSessions,
                x => x.ClassId == classroom.Id && x.StartAt == now.Date.AddDays(-index).AddHours(9),
                new ClassSession
                {
                    ClassId = classroom.Id,
                    TeacherId = teachers[index - 1].Id,
                    AvailabilityId = availability.Id,
                    SubjectId = subjects[0].Id,
                    StartAt = now.Date.AddDays(-index).AddHours(9),
                    EndAt = now.Date.AddDays(-index).AddHours(11),
                    Capacity = 40,
                    EnrolledCount = 1,
                    Status = ScheduleStatuses.Scheduled,
                    CreatedBy = sessionCreatorId,
                    CreatedAt = now.AddDays(-index)
                });

            await db.SessionEnrollments.UpdateOneAsync(
                x => x.SessionId == session.Id && x.StudentId == students[index - 1].Id,
                Builders<SessionEnrollment>.Update
                    .SetOnInsert(x => x.SessionId, session.Id)
                    .SetOnInsert(x => x.StudentId, students[index - 1].Id)
                    .SetOnInsert(x => x.Status, ScheduleStatuses.Active)
                    .SetOnInsert(x => x.EnrolledAt, now.AddDays(-index)),
                new UpdateOptions { IsUpsert = true });

            await db.Submissions.UpdateOneAsync(
                x => x.AssignmentId == assignment.Id && x.StudentId == students[index - 1].Id,
                Builders<Submission>.Update
                    .SetOnInsert(x => x.AssignmentId, assignment.Id)
                    .SetOnInsert(x => x.StudentId, students[index - 1].Id)
                    .SetOnInsert(x => x.Content, "DEMO submission")
                    .SetOnInsert(x => x.Grade, 7 + index % 4)
                    .SetOnInsert(x => x.TeacherComment, "DEMO feedback")
                    .SetOnInsert(x => x.SubmittedAt, now.AddDays(-index))
                    .SetOnInsert(x => x.GradedAt, now.AddDays(-index).AddHours(1)),
                new UpdateOptions { IsUpsert = true });

            await db.ActivityLogs.UpdateOneAsync(
                x => x.Action == "DEMO-SEED" && x.Message == $"DEMO activity {suffix}",
                Builders<ActivityLog>.Update
                    .SetOnInsert(x => x.UserId, students[index - 1].Id)
                    .SetOnInsert(x => x.Email, students[index - 1].Email)
                    .SetOnInsert(x => x.Action, "DEMO-SEED")
                    .SetOnInsert(x => x.Success, true)
                    .SetOnInsert(x => x.IpAddress, "127.0.0.1")
                    .SetOnInsert(x => x.UserAgent, "LMS demo data")
                    .SetOnInsert(x => x.Message, $"DEMO activity {suffix}")
                    .SetOnInsert(x => x.CreatedAt, now.AddDays(-index)),
                new UpdateOptions { IsUpsert = true });
        }
    }

    private static async Task<T> GetOrCreateAsync<T>(
        IMongoCollection<T> collection,
        System.Linq.Expressions.Expression<Func<T, bool>> filter,
        T value)
    {
        var existing = await collection.Find(filter).FirstOrDefaultAsync();
        if (existing is not null)
            return existing;
        await collection.InsertOneAsync(value);
        return value;
    }

    private static async Task<User> GetOrCreateDemoUserAsync(
        MongoContext db,
        string email,
        string fullName,
        string role,
        ObjectId departmentId,
        ObjectId? programId,
        IEnumerable<ObjectId> teachingSubjects)
    {
        var existing = await db.Users.Find(x => x.Email == email).FirstOrDefaultAsync();
        if (existing is not null)
            return existing;

        var permissions = PermissionCodes.DefaultsForRole(role).ToList();
        var user = new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
            Role = role,
            DepartmentId = departmentId,
            ProgramId = programId,
            CurrentSemester = 1,
            TeachingSubjectIds = teachingSubjects.ToList(),
            PermissionCodes = permissions,
            PermissionsInitialized = true,
            StudentPortalPermissionsInitialized = role == Roles.Student,
            AcademicProfileInitialized = true,
            IsActive = true
        };
        await db.Users.InsertOneAsync(user);
        return user;
    }
}
