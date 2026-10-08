using LMS.Data;
using LMS.Models;
using MongoDB.Driver;

namespace LMS.Services;
public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoContext>();
        var defaultDepartment = await db.Departments.Find(x => x.Code == "GEN").FirstOrDefaultAsync();
        if (defaultDepartment is null)
        {
            defaultDepartment = new Department
            {
                Code = "GEN",
                Name = "Khoa đại cương",
                Description = "Khoa mặc định để giữ liên kết hợp lệ cho dữ liệu môn học cũ."
            };
            await db.Departments.InsertOneAsync(defaultDepartment);
        }
        if (!await db.Users.Find(_ => true).AnyAsync())
        {
            await db.Users.InsertManyAsync(new[] {
                new User { FullName="Administrator", Email="admin@lms.com", PasswordHash=BCrypt.Net.BCrypt.HashPassword("Admin@123"), Role=Roles.Admin },
                new User { FullName="Teacher Demo", Email="teacher@lms.com", PasswordHash=BCrypt.Net.BCrypt.HashPassword("Teacher@123"), Role=Roles.Teacher },
                new User { FullName="Student Demo", Email="student@lms.com", PasswordHash=BCrypt.Net.BCrypt.HashPassword("Student@123"), Role=Roles.Student }
            });
        }

        var generalSubject = await db.Subjects.Find(x => x.Code == "GEN101").FirstOrDefaultAsync();
        generalSubject ??= await db.Subjects.Find(x => !x.IsArchived).SortBy(x => x.CreatedAt).FirstOrDefaultAsync();
        if (generalSubject is null)
        {
            generalSubject = new Subject
            {
                DepartmentId = defaultDepartment.Id,
                Code = "GEN101",
                Name = "Môn học mẫu",
                Description = "Môn mặc định dùng cho môi trường phát triển; có thể chỉnh sửa hoặc xóa khi không còn liên kết."
            };
            await db.Subjects.InsertOneAsync(generalSubject);
        }

        var activeDepartmentIds = await db.Departments.Find(x => !x.IsArchived)
            .Project(x => x.Id).ToListAsync();
        await db.Subjects.UpdateManyAsync(
            Builders<Subject>.Filter.Nin(x => x.DepartmentId, activeDepartmentIds),
            Builders<Subject>.Update.Set(x => x.DepartmentId, defaultDepartment.Id));
        if (generalSubject is not null && generalSubject.DepartmentId == MongoDB.Bson.ObjectId.Empty)
        {
            generalSubject.DepartmentId = defaultDepartment.Id;
            await db.Subjects.UpdateOneAsync(x => x.Id == generalSubject.Id,
                Builders<Subject>.Update.Set(x => x.DepartmentId, defaultDepartment.Id));
        }

        if (generalSubject is not null)
        {
            var emptySubjectFilter = Builders<ClassRoom>.Filter.Or(
                Builders<ClassRoom>.Filter.Eq(x => x.SubjectId, MongoDB.Bson.ObjectId.Empty),
                Builders<ClassRoom>.Filter.Exists(x => x.SubjectId, false));
            var legacyClasses = await db.Classes.Find(emptySubjectFilter).ToListAsync();
            var teacherIds = legacyClasses.Select(x => x.TeacherId).Distinct().ToArray();
            if (teacherIds.Length > 0)
            {
                await db.Users.UpdateManyAsync(
                    Builders<User>.Filter.And(
                        Builders<User>.Filter.In(x => x.Id, teacherIds),
                        Builders<User>.Filter.Eq(x => x.Role, Roles.Teacher)),
                    Builders<User>.Update.AddToSet(x => x.TeachingSubjectIds, generalSubject.Id));
                await db.Classes.UpdateManyAsync(
                    emptySubjectFilter,
                    Builders<ClassRoom>.Update.Set(x => x.SubjectId, generalSubject.Id));
            }

            var demoTeacher = await db.Users.Find(x =>
                x.Email == "teacher@lms.com" && x.Role == Roles.Teacher).FirstOrDefaultAsync();
            if (demoTeacher is not null && demoTeacher.TeachingSubjectIds.Count == 0)
                await db.Users.UpdateOneAsync(
                    x => x.Id == demoTeacher.Id,
                    Builders<User>.Update.AddToSet(x => x.TeachingSubjectIds, generalSubject.Id));
        }

        var permissionService = scope.ServiceProvider.GetRequiredService<PermissionService>();
        await permissionService.InitializeAsync();

        var defaultProgram = await db.AcademicPrograms.Find(x => x.Code == "GEN-DEFAULT")
            .FirstOrDefaultAsync();
        if (defaultProgram is null)
        {
            defaultProgram = new AcademicProgram
            {
                DepartmentId = defaultDepartment.Id,
                Code = "GEN-DEFAULT",
                Name = "Chương trình đại cương",
                Description = "Chương trình mặc định để cấu hình hồ sơ sinh viên và lịch đăng ký."
            };
            await db.AcademicPrograms.InsertOneAsync(defaultProgram);
        }

        var now = DateTime.UtcNow;
        var activeTerm = await db.AcademicTerms.Find(x => x.IsActive).FirstOrDefaultAsync();
        var fallbackTerm = activeTerm ??
            await db.AcademicTerms.Find(_ => true).SortByDescending(x => x.StartsAt).FirstOrDefaultAsync();
        if (fallbackTerm is null)
        {
            fallbackTerm = new AcademicTerm
            {
                Code = $"DEFAULT-{now.Year}",
                Name = $"Học kỳ 1 năm {now.Year}",
                AcademicYear = now.Year.ToString(),
                SemesterNumber = 1,
                StartsAt = now.AddMonths(-1),
                EndsAt = now.AddMonths(5),
                RegistrationOpensAt = now.AddDays(-1),
                RegistrationClosesAt = now.AddDays(14),
                IsActive = true
            };
            await db.AcademicTerms.InsertOneAsync(fallbackTerm);
            activeTerm = fallbackTerm;
        }

        var programSubjects = await db.ProgramCourses.Find(x => x.ProgramId == defaultProgram.Id)
            .Project(x => x.SubjectId).ToListAsync();
        var defaultSubjects = await db.Subjects.Find(x =>
            x.DepartmentId == defaultDepartment.Id && !x.IsArchived).ToListAsync();
        var unassignedSubjectIds = defaultSubjects.Select(x => x.Id)
            .Except(programSubjects).ToArray();
        if (unassignedSubjectIds.Length > 0)
        {
            await db.ProgramCourses.InsertManyAsync(unassignedSubjectIds.Select(subjectId => new ProgramCourse
            {
                ProgramId = defaultProgram.Id,
                SubjectId = subjectId,
                SemesterNumber = 1,
                Credits = 3,
                IsRequired = true
            }));
        }

        var teachersNeedingProfile = await db.Users.Find(x =>
            x.Role == Roles.Teacher &&
            (!x.AcademicProfileInitialized || x.DepartmentId == null)).ToListAsync();
        foreach (var teacher in teachersNeedingProfile)
        {
            var assignedSubjects = teacher.TeachingSubjectIds.Count == 0
                ? new List<Subject>()
                : await db.Subjects.Find(x => teacher.TeachingSubjectIds.Contains(x.Id)).ToListAsync();
            var departmentIds = assignedSubjects.Select(x => x.DepartmentId).Distinct().ToArray();
            if (teacher.AcademicProfileInitialized && departmentIds.Length > 1)
                continue;
            var departmentId = departmentIds.Length switch
            {
                0 => defaultDepartment.Id,
                1 => departmentIds[0],
                _ => (MongoDB.Bson.ObjectId?)null
            };
            await db.Users.UpdateOneAsync(x => x.Id == teacher.Id,
                Builders<User>.Update
                    .Set(x => x.DepartmentId, departmentId)
                    .Set(x => x.AcademicProfileInitialized, true));
        }

        var unassignedStudentFilter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(x => x.Role, Roles.Student),
            Builders<User>.Filter.Or(
                Builders<User>.Filter.Eq(x => x.AcademicProfileInitialized, false),
                Builders<User>.Filter.Eq(x => x.ProgramId, null)));
        await db.Users.UpdateManyAsync(unassignedStudentFilter,
            Builders<User>.Update
                .Set(x => x.DepartmentId, defaultDepartment.Id)
                .Set(x => x.ProgramId, defaultProgram.Id)
                .Set(x => x.CurrentSemester, 1)
                .Set(x => x.AcademicProfileInitialized, true));

        var unassignedClassFilter = Builders<ClassRoom>.Filter.Or(
            Builders<ClassRoom>.Filter.Exists(x => x.AcademicTermId, false),
            Builders<ClassRoom>.Filter.Eq(x => x.AcademicTermId, null));
        await db.Classes.UpdateManyAsync(unassignedClassFilter,
            Builders<ClassRoom>.Update
                .Set(x => x.AcademicTermId, fallbackTerm.Id)
                .Set(x => x.EnrollmentCapacity, 40));

        var allLegacyClasses = await db.Classes.Find(x => !x.IsArchived).ToListAsync();
        foreach (var classroom in allLegacyClasses)
        {
            foreach (var studentId in classroom.StudentIds)
            {
                await db.StudentRegistrations.UpdateOneAsync(
                    x => x.StudentId == studentId && x.ClassId == classroom.Id,
                    Builders<StudentRegistration>.Update
                        .SetOnInsert(x => x.StudentId, studentId)
                        .SetOnInsert(x => x.ClassId, classroom.Id)
                        .SetOnInsert(x => x.AcademicTermId, classroom.AcademicTermId ?? fallbackTerm.Id)
                        .SetOnInsert(x => x.Status, "Active")
                        .SetOnInsert(x => x.RegisteredAt, classroom.CreatedAt),
                    new UpdateOptions { IsUpsert = true });
            }
        }
    }
}
