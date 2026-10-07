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
    }
}
