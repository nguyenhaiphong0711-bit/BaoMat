using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;

namespace LMS.Services;

public class ActivityLogService
{
    private readonly MongoContext _db;
    public ActivityLogService(MongoContext db) => _db = db;
    public Task LogAsync(HttpContext context, ObjectId? userId, string email, string action, bool success, string message)
        => _db.ActivityLogs.InsertOneAsync(new ActivityLog {
            UserId = userId, Email = email, Action = action, Success = success,
            Message = message, IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = context.Request.Headers.UserAgent.ToString()
        });
    public Task<List<ActivityLog>> GetAllAsync() => _db.ActivityLogs.Find(_ => true).SortByDescending(x => x.CreatedAt).ToListAsync();
}

public class AuthService
{
    private readonly MongoContext _db;
    private readonly SecuritySettings _security;
    private readonly ActivityLogService _logs;
    public AuthService(MongoContext db, IOptions<SecuritySettings> security, ActivityLogService logs) { _db = db; _security = security.Value; _logs = logs; }

    public async Task<(bool Success, string Message, User? User)> LoginAsync(LoginViewModel vm, HttpContext context)
    {
        var email = vm.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Find(x => x.Email == email).FirstOrDefaultAsync();
        if (user == null) { await _logs.LogAsync(context, null, email, "LOGIN", false, "User not found"); return (false, "Email hoặc mật khẩu không đúng.", null); }
        if (!user.IsActive) { await _logs.LogAsync(context, user.Id, email, "LOGIN", false, "Account disabled"); return (false, "Email hoặc mật khẩu không đúng.", null); }
        if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
        { await _logs.LogAsync(context, user.Id, email, "LOGIN", false, "Account temporarily locked"); return (false, "Email hoặc mật khẩu không đúng.", null); }

        var valid = BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash);
        if (!valid)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= _security.MaxLoginAttempts)
            { user.LockoutUntil = DateTime.UtcNow.AddMinutes(_security.LockoutMinutes); user.FailedLoginAttempts = 0; }
            await _db.Users.ReplaceOneAsync(x => x.Id == user.Id, user);
            await _logs.LogAsync(context, user.Id, email, "LOGIN", false, "Invalid password");
            return (false, "Email hoặc mật khẩu không đúng.", null);
        }
        user.FailedLoginAttempts = 0; user.LockoutUntil = null;
        await _db.Users.ReplaceOneAsync(x => x.Id == user.Id, user);
        await _logs.LogAsync(context, user.Id, email, "LOGIN", true, "Login successful");
        return (true, "Đăng nhập thành công.", user);
    }
}

public class UserService
{
    private readonly MongoContext _db;
    public UserService(MongoContext db) => _db = db;
    public Task<List<User>> GetAllAsync() => _db.Users.Find(_ => true).SortBy(x => x.Role).ToListAsync();
    public async Task<bool> CreateAsync(CreateUserViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.FullName) ||
            !new[] { Roles.Admin, Roles.Teacher, Roles.Student }.Contains(vm.Role, StringComparer.Ordinal))
            return false;
        var email = vm.Email.Trim().ToLowerInvariant();
        if (await _db.Users.Find(x => x.Email == email).AnyAsync()) return false;
        var user = new User
        {
            FullName = vm.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
            Role = vm.Role,
            PermissionCodes = PermissionCodes.DefaultsForRole(vm.Role).ToList(),
            PermissionsInitialized = true,
            StudentPortalPermissionsInitialized = vm.Role == Roles.Student,
            AcademicProfileInitialized = vm.Role == Roles.Admin
        };
        if (vm.Role == Roles.Teacher)
        {
            var defaultDepartment = await _db.Departments.Find(x => x.Code == "GEN" && !x.IsArchived)
                .FirstOrDefaultAsync();
            if (defaultDepartment is not null)
            {
                user.DepartmentId = defaultDepartment.Id;
                user.AcademicProfileInitialized = true;
            }
        }
        else if (vm.Role == Roles.Student)
        {
            var defaultProgram = await _db.AcademicPrograms.Find(x => x.Code == "GEN-DEFAULT" && x.IsActive)
                .FirstOrDefaultAsync();
            if (defaultProgram is not null)
            {
                user.ProgramId = defaultProgram.Id;
                user.DepartmentId = defaultProgram.DepartmentId;
                user.CurrentSemester = 1;
                user.AcademicProfileInitialized = true;
            }
        }

        await _db.Users.InsertOneAsync(user);
        return true;
    }
    public async Task<User?> GetAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var oid))
            return null;

        return await _db.Users.Find(x => x.Id == oid).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        User? user = await _db.Users.Find(x => x.Email == normalizedEmail).FirstOrDefaultAsync();
        return user;
    }

    public async Task ToggleAsync(string id) { if (!ObjectId.TryParse(id, out var oid)) return; var u = await GetAsync(id); if (u != null) { u.IsActive = !u.IsActive; await _db.Users.ReplaceOneAsync(x => x.Id == oid, u); } }
    public Task<List<User>> GetTeachersAsync() => _db.Users.Find(x => x.Role == Roles.Teacher && x.IsActive).ToListAsync();
    public Task<List<User>> GetStudentsAsync() => _db.Users.Find(x => x.Role == Roles.Student && x.IsActive).ToListAsync();
    public Task<List<User>> GetByIdsAsync(ObjectId[] ids) =>
        _db.Users.Find(Builders<User>.Filter.In(x => x.Id, ids)).ToListAsync();

    public async Task<bool> HasPermissionAsync(ObjectId userId, string permissionCode)
    {
        var user = await _db.Users.Find(x => x.Id == userId && x.IsActive).FirstOrDefaultAsync();
        return user is { Role: Roles.Admin } ||
            user?.PermissionCodes.Contains(permissionCode, StringComparer.Ordinal) == true;
    }

    public async Task<bool> AssignTeachingSubjectsAsync(string teacherId, IReadOnlyCollection<string> subjectIds)
    {
        if (!ObjectId.TryParse(teacherId, out var teacherObjectId) ||
            subjectIds.Any(id => !ObjectId.TryParse(id, out _)))
            return false;

        var teacher = await _db.Users.Find(x =>
            x.Id == teacherObjectId && x.Role == Roles.Teacher && x.IsActive).FirstOrDefaultAsync();
        if (teacher is null)
            return false;

        var parsedIds = subjectIds.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(ObjectId.Parse).ToArray();
        var subjects = parsedIds.Length == 0
            ? new List<Subject>()
            : await _db.Subjects.Find(Builders<Subject>.Filter.And(
                Builders<Subject>.Filter.In(x => x.Id, parsedIds),
                Builders<Subject>.Filter.Eq(x => x.IsArchived, false))).ToListAsync();
        if (subjects.Count != parsedIds.Length ||
            subjects.Select(x => x.DepartmentId).Distinct().Count() > 1)
            return false;
        var assignedClasses = await _db.Classes.Find(x => x.TeacherId == teacherObjectId).ToListAsync();
        if (assignedClasses.Any(classroom => !parsedIds.Contains(classroom.SubjectId)))
            return false;

        await _db.Users.UpdateOneAsync(
            x => x.Id == teacherObjectId,
            Builders<User>.Update
                .Set(x => x.TeachingSubjectIds, parsedIds.ToList())
                .Set(x => x.DepartmentId, subjects.FirstOrDefault()?.DepartmentId ?? teacher.DepartmentId));
        return true;
    }

    public async Task<bool> UpdateAcademicProfileAsync(UserPermissionViewModel model)
    {
        if (!ObjectId.TryParse(model.UserId, out var userId))
            return false;

        var user = await _db.Users.Find(x => x.Id == userId && x.IsActive).FirstOrDefaultAsync();
        if (user is null || user.Role == Roles.Admin)
            return false;

        if (user.Role == Roles.Teacher)
        {
            if (!ObjectId.TryParse(model.DepartmentId, out var departmentId) ||
                !await _db.Departments.Find(x => x.Id == departmentId && !x.IsArchived).AnyAsync() ||
                model.TeachingSubjectIds.Any(id => !ObjectId.TryParse(id, out _)))
                return false;

            var subjectIds = model.TeachingSubjectIds.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(ObjectId.Parse).ToArray();
            var subjects = subjectIds.Length == 0
                ? new List<Subject>()
                : await _db.Subjects.Find(x =>
                    !x.IsArchived &&
                    x.DepartmentId == departmentId &&
                    subjectIds.Contains(x.Id)).ToListAsync();
            if (subjects.Count != subjectIds.Length)
                return false;

            var assignedClasses = await _db.Classes.Find(x => x.TeacherId == userId && !x.IsArchived).ToListAsync();
            if (assignedClasses.Any(classroom => !subjectIds.Contains(classroom.SubjectId)))
                return false;

            await _db.Users.UpdateOneAsync(x => x.Id == userId,
                Builders<User>.Update
                    .Set(x => x.DepartmentId, departmentId)
                    .Set(x => x.TeachingSubjectIds, subjectIds.ToList())
                    .Set(x => x.ProgramId, (ObjectId?)null)
                    .Set(x => x.AcademicProfileInitialized, true));
            return true;
        }

        if (user.Role == Roles.Student)
        {
            if (!ObjectId.TryParse(model.ProgramId, out var programId) ||
                model.CurrentSemester is < 1 or > 20 ||
                await _db.AcademicPrograms.Find(x => x.Id == programId && x.IsActive)
                    .FirstOrDefaultAsync() is not { } program)
                return false;

            await _db.Users.UpdateOneAsync(x => x.Id == userId,
                Builders<User>.Update
                    .Set(x => x.ProgramId, programId)
                    .Set(x => x.DepartmentId, program.DepartmentId)
                    .Set(x => x.CurrentSemester, model.CurrentSemester)
                    .Set(x => x.TeachingSubjectIds, new List<ObjectId>())
                    .Set(x => x.AcademicProfileInitialized, true));
            return true;
        }

        return false;
    }
}

public class SubjectService
{
    private readonly MongoContext _db;
    public SubjectService(MongoContext db) => _db = db;

    public Task<List<Subject>> GetAllAsync() =>
        _db.Subjects.Find(x => !x.IsArchived).SortBy(x => x.Name).ToListAsync();

    public async Task<List<Subject>> SearchAsync(CatalogFilterViewModel filter)
    {
        var subjects = await GetAllAsync();
        var query = filter.Query.Trim();
        if (!string.IsNullOrEmpty(query))
            subjects = subjects.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (ObjectId.TryParse(filter.DepartmentId, out var departmentId))
            subjects = subjects.Where(x => x.DepartmentId == departmentId).ToList();
        if (filter.CreatedFrom.HasValue)
            subjects = subjects.Where(x => x.CreatedAt.Date >= filter.CreatedFrom.Value.Date).ToList();
        if (filter.CreatedTo.HasValue)
            subjects = subjects.Where(x => x.CreatedAt.Date <= filter.CreatedTo.Value.Date).ToList();
        return subjects;
    }

    public async Task<Department?> GetDepartmentAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var departmentId))
            return null;
        return await _db.Departments.Find(x => x.Id == departmentId && !x.IsArchived).FirstOrDefaultAsync();
    }

    public async Task<Subject?> GetAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var subjectId))
            return null;
        return await _db.Subjects.Find(x => x.Id == subjectId && !x.IsArchived).FirstOrDefaultAsync();
    }

    public async Task<Subject?> CreateAsync(SubjectViewModel model)
    {
        if (!ObjectId.TryParse(model.DepartmentId, out var departmentId) ||
            !await _db.Departments.Find(x => x.Id == departmentId && !x.IsArchived).AnyAsync())
            return null;
        var code = model.Code.Trim().ToUpperInvariant();
        if ((await GetAllAsync()).Any(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            return null;
        var subject = new Subject { DepartmentId = departmentId, Code = code, Name = model.Name.Trim(), Description = model.Description.Trim() };
        try
        {
            await _db.Subjects.InsertOneAsync(subject);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return null;
        }
        return subject;
    }

    public async Task<bool> UpdateAsync(string id, SubjectViewModel model)
    {
        if (!ObjectId.TryParse(id, out var subjectId) ||
            !ObjectId.TryParse(model.DepartmentId, out var departmentId) ||
            !await _db.Departments.Find(x => x.Id == departmentId && !x.IsArchived).AnyAsync())
            return false;
        var code = model.Code.Trim().ToUpperInvariant();
        if ((await GetAllAsync()).Any(x => x.Id != subjectId && x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            return false;

        try
        {
            var result = await _db.Subjects.UpdateOneAsync(
                x => x.Id == subjectId && !x.IsArchived,
                Builders<Subject>.Update
                    .Set(x => x.Code, code)
                    .Set(x => x.DepartmentId, departmentId)
                    .Set(x => x.Name, model.Name.Trim())
                    .Set(x => x.Description, model.Description.Trim()));
            return result.ModifiedCount == 1 || result.MatchedCount == 1;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var subjectId))
            return (false, "Mã môn học không hợp lệ.");
        var inUseByClass = await _db.Classes.Find(x => x.SubjectId == subjectId).AnyAsync();
        var inUseByTeacher = await _db.Users.Find(
            Builders<User>.Filter.AnyEq(x => x.TeachingSubjectIds, subjectId)).AnyAsync();
        if (inUseByClass || inUseByTeacher)
            return (false, "Không thể xóa môn đang được dùng bởi lớp hoặc giảng viên. Hãy chuyển các liên kết trước.");
        var deleted = await _db.Subjects.DeleteOneAsync(x => x.Id == subjectId);
        return deleted.DeletedCount == 1
            ? (true, null)
            : (false, "Không tìm thấy môn học.");
    }
}

public class DepartmentService
{
    private readonly MongoContext _db;
    public DepartmentService(MongoContext db) => _db = db;

    public Task<List<Department>> GetAllAsync() =>
        _db.Departments.Find(x => !x.IsArchived).SortBy(x => x.Name).ToListAsync();

    public async Task<Department?> GetAsync(string id) =>
        ObjectId.TryParse(id, out var departmentId)
            ? await _db.Departments.Find(x => x.Id == departmentId && !x.IsArchived).FirstOrDefaultAsync()
            : null;

    public async Task<Department?> CreateAsync(DepartmentViewModel model)
    {
        var code = model.Code.Trim().ToUpperInvariant();
        if ((await GetAllAsync()).Any(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            return null;
        var department = new Department
        {
            Code = code,
            Name = model.Name.Trim(),
            Description = model.Description.Trim()
        };
        try
        {
            await _db.Departments.InsertOneAsync(department);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return null;
        }
        return department;
    }

    public async Task<bool> UpdateAsync(string id, DepartmentViewModel model)
    {
        if (!ObjectId.TryParse(id, out var departmentId))
            return false;
        var code = model.Code.Trim().ToUpperInvariant();
        if ((await GetAllAsync()).Any(x => x.Id != departmentId && x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            return false;
        try
        {
            var result = await _db.Departments.UpdateOneAsync(
                x => x.Id == departmentId && !x.IsArchived,
                Builders<Department>.Update
                    .Set(x => x.Code, code)
                    .Set(x => x.Name, model.Name.Trim())
                    .Set(x => x.Description, model.Description.Trim()));
            return result.MatchedCount == 1;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var departmentId))
            return (false, "Mã khoa không hợp lệ.");
        if (await _db.Subjects.Find(x => x.DepartmentId == departmentId).AnyAsync())
            return (false, "Không thể xóa khoa đang có môn học. Hãy chuyển hoặc xóa các môn trước.");
        var deleted = await _db.Departments.DeleteOneAsync(x => x.Id == departmentId);
        return deleted.DeletedCount == 1
            ? (true, null)
            : (false, "Không tìm thấy khoa.");
    }
}

public class ClassService
{
    private readonly MongoContext _db;
    public ClassService(MongoContext db) => _db = db;
    public Task<List<ClassRoom>> GetAllAsync() => _db.Classes.Find(x => !x.IsArchived).SortBy(x => x.Name).ToListAsync();
    public async Task<ClassRoom?> GetAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var oid))
            return null;

        return await _db.Classes.Find(x => x.Id == oid && !x.IsArchived).FirstOrDefaultAsync();
    }

    public Task<bool> CanTeachSubjectAsync(string teacherId, string subjectId) =>
        ObjectId.TryParse(teacherId, out var teacherObjectId) &&
        ObjectId.TryParse(subjectId, out var subjectObjectId)
            ? _db.Users.Find(Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(x => x.Id, teacherObjectId),
                Builders<User>.Filter.Eq(x => x.Role, Roles.Teacher),
                Builders<User>.Filter.Eq(x => x.IsActive, true),
                Builders<User>.Filter.AnyEq(x => x.TeachingSubjectIds, subjectObjectId))).AnyAsync()
            : Task.FromResult(false);

    public async Task<ClassRoom> CreateAsync(CreateClassViewModel vm)
    {
        ObjectId.TryParse(vm.TeacherId, out var teacherId);
        ObjectId.TryParse(vm.SubjectId, out var subjectId);
        ObjectId.TryParse(vm.AcademicTermId, out var academicTermId);
        var studentIds = vm.StudentIds
            .Where(id => ObjectId.TryParse(id, out _))
            .Select(ObjectId.Parse)
            .ToList();
        var termExists = academicTermId != ObjectId.Empty &&
            await _db.AcademicTerms.Find(x => x.Id == academicTermId).AnyAsync();
        if (!termExists || vm.EnrollmentCapacity < studentIds.Count)
            throw new InvalidOperationException("Lớp phải thuộc học kỳ hợp lệ và sĩ số không được vượt quá sức chứa.");

        var classroom = new ClassRoom
        {
            Name = vm.Name.Trim(),
            Description = vm.Description.Trim(),
            SubjectId = subjectId,
            AcademicTermId = academicTermId == ObjectId.Empty ? null : academicTermId,
            EnrollmentCapacity = Math.Clamp(vm.EnrollmentCapacity, 1, 500),
            TeacherId = teacherId,
            StudentIds = studentIds
        };
        await _db.Classes.InsertOneAsync(classroom);
        foreach (var studentId in studentIds)
        {
            await _db.StudentRegistrations.UpdateOneAsync(
                x => x.StudentId == studentId && x.ClassId == classroom.Id,
                Builders<StudentRegistration>.Update
                    .Set(x => x.AcademicTermId, academicTermId)
                    .Set(x => x.Status, "Active")
                    .Set(x => x.RegisteredAt, DateTime.UtcNow)
                    .Set(x => x.WithdrawnAt, (DateTime?)null)
                    .SetOnInsert(x => x.StudentId, studentId)
                    .SetOnInsert(x => x.ClassId, classroom.Id),
                new UpdateOptions { IsUpsert = true });
        }
        return classroom;
    }

    public async Task<bool> AssignTeacherAsync(string classId, string teacherId)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId) ||
            !ObjectId.TryParse(teacherId, out var teacherObjectId))
            return false;

        var teacher = await _db.Users.Find(x => x.Id == teacherObjectId && x.Role == Roles.Teacher && x.IsActive).FirstOrDefaultAsync();
        if (teacher is null)
            return false;
        var classroom = await GetAsync(classId);
        if (classroom is null)
            return false;
        if (!teacher.TeachingSubjectIds.Contains(classroom.SubjectId))
            return false;
        if (classroom.TeacherId != teacherObjectId &&
            await _db.ClassSessions.Find(x =>
                x.ClassId == classObjectId &&
                x.Status == ScheduleStatuses.Scheduled &&
                x.EndAt > DateTime.UtcNow).AnyAsync())
            return false;

        var result = await _db.Classes.UpdateOneAsync(
            x => x.Id == classObjectId,
            Builders<ClassRoom>.Update.Set(x => x.TeacherId, teacherObjectId));
        return result.MatchedCount > 0;
    }

    public async Task<bool> UpdateAsync(string id, UpdateClassViewModel vm)
    {
        if (!ObjectId.TryParse(id, out var classObjectId) ||
            !ObjectId.TryParse(vm.SubjectId, out var subjectId) ||
            !ObjectId.TryParse(vm.TeacherId, out var teacherObjectId) ||
            !ObjectId.TryParse(vm.AcademicTermId, out var academicTermId) ||
            vm.EnrollmentCapacity is < 1 or > 500 ||
            vm.StudentIds is null ||
            vm.StudentIds.Any(studentId => !ObjectId.TryParse(studentId, out _)))
            return false;

        var teacher = await _db.Users.Find(Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(x => x.Id, teacherObjectId),
            Builders<User>.Filter.Eq(x => x.Role, Roles.Teacher),
            Builders<User>.Filter.Eq(x => x.IsActive, true),
            Builders<User>.Filter.AnyEq(x => x.TeachingSubjectIds, subjectId))).FirstOrDefaultAsync();
        var subject = await _db.Subjects.Find(x => x.Id == subjectId && !x.IsArchived).AnyAsync();
        var studentIds = vm.StudentIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(ObjectId.Parse)
            .ToList();
        var students = await _db.Users.Find(
            Builders<User>.Filter.And(
                Builders<User>.Filter.In(x => x.Id, studentIds),
                Builders<User>.Filter.Eq(x => x.Role, Roles.Student),
                Builders<User>.Filter.Eq(x => x.IsActive, true))).ToListAsync();

        if (teacher is null || !subject || students.Count != studentIds.Count)
            return false;

        var classroom = await GetAsync(id);
        if (classroom is null)
            return false;
        if (!await _db.AcademicTerms.Find(x => x.Id == academicTermId).AnyAsync() ||
            vm.EnrollmentCapacity < studentIds.Count)
            return false;
        if ((classroom.TeacherId != teacherObjectId || classroom.SubjectId != subjectId) &&
            await _db.ClassSessions.Find(x =>
                x.ClassId == classObjectId &&
                x.Status == ScheduleStatuses.Scheduled &&
                x.EndAt > DateTime.UtcNow).AnyAsync())
            return false;

        var previousStudentIds = classroom.StudentIds.ToHashSet();
        classroom.Name = vm.Name.Trim();
        classroom.Description = vm.Description.Trim();
        classroom.SubjectId = subjectId;
        classroom.AcademicTermId = academicTermId;
        classroom.EnrollmentCapacity = vm.EnrollmentCapacity;
        classroom.TeacherId = teacherObjectId;
        classroom.StudentIds = studentIds;
        await _db.Classes.ReplaceOneAsync(x => x.Id == classObjectId, classroom);
        await _db.StudentRegistrations.UpdateManyAsync(
            x => x.ClassId == classObjectId && x.Status == "Active",
            Builders<StudentRegistration>.Update.Set(x => x.AcademicTermId, academicTermId));
        var withdrawnIds = previousStudentIds.Except(studentIds).ToArray();
        if (withdrawnIds.Length > 0)
        {
            await _db.StudentRegistrations.UpdateManyAsync(x =>
                    x.ClassId == classObjectId && withdrawnIds.Contains(x.StudentId) && x.Status == "Active",
                Builders<StudentRegistration>.Update
                    .Set(x => x.Status, "Withdrawn")
                    .Set(x => x.WithdrawnAt, DateTime.UtcNow));
        }
        foreach (var studentId in studentIds.Except(previousStudentIds))
        {
            await _db.StudentRegistrations.UpdateOneAsync(
                x => x.StudentId == studentId && x.ClassId == classObjectId,
                Builders<StudentRegistration>.Update
                    .Set(x => x.AcademicTermId, academicTermId)
                    .Set(x => x.Status, "Active")
                    .Set(x => x.RegisteredAt, DateTime.UtcNow)
                    .Set(x => x.WithdrawnAt, (DateTime?)null)
                    .SetOnInsert(x => x.StudentId, studentId)
                    .SetOnInsert(x => x.ClassId, classObjectId),
                new UpdateOptions { IsUpsert = true });
        }
        return true;
    }

    public async Task<(bool Success, string? Error)> ArchiveAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var classId))
            return (false, "Mã lớp không hợp lệ.");
        var hasUpcomingSession = await _db.ClassSessions.Find(x =>
            x.ClassId == classId &&
            x.Status == ScheduleStatuses.Scheduled &&
            x.EndAt > DateTime.UtcNow).AnyAsync();
        if (hasUpcomingSession)
            return (false, "Hãy hủy các buổi học sắp tới trước khi lưu trữ lớp.");

        var result = await _db.Classes.UpdateOneAsync(
            x => x.Id == classId && !x.IsArchived,
            Builders<ClassRoom>.Update.Set(x => x.IsArchived, true));
        return result.ModifiedCount == 1
            ? (true, null)
            : (false, "Không tìm thấy lớp hoặc lớp đã được lưu trữ.");
    }

    public async Task<List<ClassRoom>> GetForUserAsync(User user)
    {
        if (user.Role == Roles.Admin) return await GetAllAsync();
        if (user.Role == Roles.Teacher) return await _db.Classes.Find(x => !x.IsArchived && x.TeacherId == user.Id).ToListAsync();
        return await _db.Classes.Find(
            Builders<ClassRoom>.Filter.And(
                Builders<ClassRoom>.Filter.Eq(x => x.IsArchived, false),
                Builders<ClassRoom>.Filter.AnyEq(x => x.StudentIds, user.Id))).ToListAsync();
    }
    public async Task<bool> HasAccessAsync(string classId, User user)
    {
        var c = await GetAsync(classId); if (c == null) return false;
        return user.Role == Roles.Admin || c.TeacherId == user.Id || c.StudentIds.Contains(user.Id);
    }
}

public class LessonService
{
    private readonly MongoContext _db; public LessonService(MongoContext db) => _db = db;
    public Task<List<Lesson>> GetByClassAsync(ObjectId classId, bool publishedOnly) =>
        _db.Lessons.Find(publishedOnly
            ? (x => x.ClassId == classId && x.IsPublished && !x.IsArchived)
            : (x => x.ClassId == classId && !x.IsArchived)).SortBy(x => x.CreatedAt).ToListAsync();
    public async Task<Lesson?> GetAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var oid))
            return null;

        return await _db.Lessons.Find(x => x.Id == oid).FirstOrDefaultAsync();
    }

    public async Task<Lesson> CreateAsync(ObjectId classId, LessonViewModel vm)
    {
        var lesson = new Lesson { ClassId = classId, Title = vm.Title, Content = vm.Content, IsPublished = vm.IsPublished };
        await _db.Lessons.InsertOneAsync(lesson);
        return lesson;
    }

    public async Task UpdateAsync(string id, LessonViewModel vm)
    {
        var lesson = await GetAsync(id);
        if (lesson is null) return;
        lesson.Title = vm.Title;
        lesson.Content = vm.Content;
        lesson.IsPublished = vm.IsPublished;
        await _db.Lessons.ReplaceOneAsync(x => x.Id == lesson.Id, lesson);
    }

    public async Task<bool> ArchiveAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var lessonId)) return false;
        var result = await _db.Lessons.UpdateOneAsync(
            x => x.Id == lessonId && !x.IsArchived,
            Builders<Lesson>.Update.Set(x => x.IsArchived, true));
        return result.ModifiedCount == 1;
    }
}

public class AssignmentService
{
    private readonly MongoContext _db; public AssignmentService(MongoContext db) => _db = db;
    public Task<List<Assignment>> GetByClassAsync(ObjectId classId, bool publishedOnly) =>
        _db.Assignments.Find(publishedOnly
            ? (x => x.ClassId == classId && x.IsPublished && !x.IsArchived)
            : (x => x.ClassId == classId && !x.IsArchived)).SortBy(x => x.DueDate).ToListAsync();
    public async Task<Assignment?> GetAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var oid))
            return null;

        return await _db.Assignments.Find(x => x.Id == oid).FirstOrDefaultAsync();
    }

    public async Task<Assignment> CreateAsync(
        ObjectId classId,
        AssignmentViewModel vm,
        IReadOnlyList<StoredFileReference>? attachments = null)
    {
        var assignment = new Assignment
        {
            ClassId = classId,
            Title = vm.Title,
            Description = vm.Description,
            DueDate = vm.DueDate.ToUniversalTime(),
            IsPublished = vm.IsPublished,
            Attachments = attachments?.ToList() ?? new List<StoredFileReference>()
        };
        await _db.Assignments.InsertOneAsync(assignment);
        return assignment;
    }

    public async Task UpdateAsync(string id, AssignmentViewModel vm, IReadOnlyList<StoredFileReference>? attachments = null)
    {
        var assignment = await GetAsync(id);
        if (assignment is null) return;
        assignment.Title = vm.Title;
        assignment.Description = vm.Description;
        assignment.DueDate = vm.DueDate.ToUniversalTime();
        assignment.IsPublished = vm.IsPublished;
        if (attachments is not null)
            assignment.Attachments = attachments.ToList();
        await _db.Assignments.ReplaceOneAsync(x => x.Id == assignment.Id, assignment);
    }

    public async Task<Assignment?> GetByAttachmentAsync(ObjectId fileId) =>
        await _db.Assignments.Find(x => x.Attachments.Any(file => file.Id == fileId)).FirstOrDefaultAsync();

    public async Task<bool> ArchiveAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var assignmentId)) return false;
        var result = await _db.Assignments.UpdateOneAsync(
            x => x.Id == assignmentId && !x.IsArchived,
            Builders<Assignment>.Update.Set(x => x.IsArchived, true));
        return result.ModifiedCount == 1;
    }
}

public class SubmissionService
{
    private readonly MongoContext _db; public SubmissionService(MongoContext db) => _db = db;
    public Task<List<Submission>> GetByAssignmentAsync(ObjectId assignmentId) => _db.Submissions.Find(x => x.AssignmentId == assignmentId).ToListAsync();
    public async Task<Submission?> GetAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var oid))
            return null;

        return await _db.Submissions.Find(x => x.Id == oid).FirstOrDefaultAsync();
    }

    public async Task<Submission> SubmitAsync(
        ObjectId assignmentId,
        ObjectId studentId,
        SubmissionViewModel vm,
        IReadOnlyList<StoredFileReference>? attachments = null)
    {
        var old = await _db.Submissions.Find(x => x.AssignmentId == assignmentId && x.StudentId == studentId).FirstOrDefaultAsync();
        if (old is not null)
        {
            old.Content = vm.Content;
            if (attachments is not null)
                old.Attachments = attachments.ToList();
            old.SubmittedAt = DateTime.UtcNow;
            old.Grade = null;
            old.TeacherComment = "";
            old.GradedAt = null;
            await _db.Submissions.ReplaceOneAsync(x => x.Id == old.Id, old);
            return old;
        }

        var submission = new Submission
        {
            AssignmentId = assignmentId,
            StudentId = studentId,
            Content = vm.Content,
            Attachments = attachments?.ToList() ?? new List<StoredFileReference>()
        };
        await _db.Submissions.InsertOneAsync(submission);
        return submission;
    }
    public async Task<Submission?> GradeAsync(string id, GradeViewModel vm)
    {
        var submission = await GetAsync(id);
        if (submission is null) return null;
        submission.Grade = vm.Grade;
        submission.TeacherComment = vm.TeacherComment;
        submission.GradedAt = DateTime.UtcNow;
        await _db.Submissions.ReplaceOneAsync(x => x.Id == submission.Id, submission);
        return submission;
    }
    public async Task<Submission?> GetMineAsync(ObjectId assignmentId, ObjectId studentId)
    {
        return await _db.Submissions.Find(x => x.AssignmentId == assignmentId && x.StudentId == studentId).FirstOrDefaultAsync();
    }

    public async Task<List<Submission>> GetMineForClassAsync(ObjectId classId, ObjectId studentId)
    {
        var assignmentIds = await _db.Assignments.Find(x => x.ClassId == classId && !x.IsArchived)
            .Project(x => x.Id).ToListAsync();
        if (assignmentIds.Count == 0)
            return new List<Submission>();

        return await _db.Submissions.Find(x =>
            x.StudentId == studentId && assignmentIds.Contains(x.AssignmentId)).ToListAsync();
    }

    public async Task<Submission?> GetByAttachmentAsync(ObjectId fileId) =>
        await _db.Submissions.Find(x => x.Attachments.Any(file => file.Id == fileId)).FirstOrDefaultAsync();
}
