namespace LMS.Models;

public sealed record StaffStudentResponse(string Id, string FullName, string Email,
    string ProgramName, int CurrentSemester, bool IsActive);
public sealed record StaffStudentsResponse(string DepartmentName, IReadOnlyList<StaffStudentResponse> Items,
    long Total, int Page, int PageSize);
public sealed record StaffCourseResponse(string ClassName, string SubjectName, int AssignmentCount,
    int SubmittedCount, int GradedCount, double? AverageGrade);
public sealed record StaffProgressResponse(StaffStudentResponse Student, IReadOnlyList<StaffCourseResponse> Courses);
