using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace LMS.Services;

// Read-only reporting. It never creates invoices or returns submissions/files/password fields.
public sealed class StaffAcademicService(MongoContext db)
{
    public async Task<Department?> GetScopeAsync(User staff) =>
        staff is { IsActive: true, Role: Roles.Staff, DepartmentId: { } departmentId }
            ? await db.Departments.Find(x => x.Id == departmentId && !x.IsArchived).FirstOrDefaultAsync()
            : null;

    public async Task<StaffStudentsResponse> GetStudentsAsync(Department scope, string? query, int page)
    {
        const int pageSize = 20;
        var filter = Builders<User>.Filter.Eq(x => x.Role, Roles.Student) &
            Builders<User>.Filter.Eq(x => x.DepartmentId, scope.Id);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = new BsonRegularExpression(Regex.Escape(query.Trim()), "i");
            filter &= Builders<User>.Filter.Regex(x => x.FullName, term) | Builders<User>.Filter.Regex(x => x.Email, term);
        }
        var total = await db.Users.CountDocumentsAsync(filter);
        var students = await db.Users.Find(filter).SortBy(x => x.FullName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync();
        var programs = await db.AcademicPrograms.Find(x => x.DepartmentId == scope.Id && x.IsActive).ToListAsync();
        return new StaffStudentsResponse(scope.Name, students.Select(s => ToStudent(s, programs)).ToArray(), total, page, pageSize);
    }

    public async Task<StaffProgressResponse?> GetProgressAsync(Department scope, string studentId)
    {
        if (!ObjectId.TryParse(studentId, out var id)) return null;
        var student = await db.Users.Find(x => x.Id == id && x.Role == Roles.Student && x.DepartmentId == scope.Id)
            .FirstOrDefaultAsync();
        if (student is null) return null;
        var programs = await db.AcademicPrograms.Find(x => x.DepartmentId == scope.Id && x.IsActive).ToListAsync();
        var subjects = await db.Subjects.Find(x => x.DepartmentId == scope.Id && !x.IsArchived).ToListAsync();
        var subjectIds = subjects.Select(x => x.Id).ToArray();
        var classes = await db.Classes.Find(x => !x.IsArchived && x.StudentIds.Contains(id) && subjectIds.Contains(x.SubjectId)).ToListAsync();
        var classIds = classes.Select(x => x.Id).ToArray();
        var assignments = await db.Assignments.Find(x => classIds.Contains(x.ClassId) && x.IsPublished && !x.IsArchived).ToListAsync();
        var assignmentIds = assignments.Select(x => x.Id).ToArray();
        var submissions = await db.Submissions.Find(x => x.StudentId == id && assignmentIds.Contains(x.AssignmentId)).ToListAsync();
        var courses = classes.Select(c =>
        {
            var tasks = assignments.Where(x => x.ClassId == c.Id).Select(x => x.Id).ToHashSet();
            var work = submissions.Where(x => tasks.Contains(x.AssignmentId)).GroupBy(x => x.AssignmentId)
                .Select(group => group.OrderByDescending(x => x.SubmittedAt).First()).ToArray();
            var scores = work.Where(x => x.Grade.HasValue).Select(x => x.Grade!.Value).ToArray();
            return new StaffCourseResponse(c.Name, subjects.First(x => x.Id == c.SubjectId).Name,
                tasks.Count, work.Length, scores.Length, scores.Length == 0 ? null : scores.Average());
        }).OrderBy(x => x.ClassName).ToArray();
        return new StaffProgressResponse(ToStudent(student, programs), courses);
    }

    private static StaffStudentResponse ToStudent(User student, IReadOnlyList<AcademicProgram> programs) =>
        new(student.Id.ToString(), student.FullName, student.Email,
            programs.FirstOrDefault(p => p.Id == student.ProgramId)?.Name ?? "Chưa gán ngành hợp lệ",
            student.CurrentSemester, student.IsActive);
}
