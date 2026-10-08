using LMS.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace LMS.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
}

public class ResetPasswordViewModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã OTP phải gồm đúng 6 chữ số.")]
    public string Code { get; set; } = "";

    [Required, MinLength(8), StringLength(128), DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = "";
}

public class CreateUserViewModel
{
    [Required, StringLength(120, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, MinLength(8), StringLength(128), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, RegularExpression("^(Admin|Teacher|Student)$")] public string Role { get; set; } = Roles.Student;
}

public class CreateClassViewModel
{
    [Required, StringLength(160, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(2000)]
    public string Description { get; set; } = "";
    [Required] public string SubjectId { get; set; } = "";
    [Required] public string TeacherId { get; set; } = "";
    [Required] public string AcademicTermId { get; set; } = "";
    [Range(1, 500)] public int EnrollmentCapacity { get; set; } = 40;
    public List<string> StudentIds { get; set; } = new();
}

public class TeachingAssignmentViewModel
{
    [Required] public string ClassId { get; set; } = "";
    [Required] public string TeacherId { get; set; } = "";
    [Required] public string SubjectId { get; set; } = "";
}

public class TeachingAssignmentListItem
{
    public string ClassId { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string TeacherId { get; init; } = "";
    public string TeacherName { get; init; } = "";
    public string SubjectName { get; init; } = "";
    public string SubjectId { get; init; } = "";
    public string DepartmentId { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}

public class SubjectViewModel
{
    [Required] public string DepartmentId { get; set; } = "";
    [Required, StringLength(20, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Mã môn chỉ được gồm chữ, số và dấu gạch ngang.")]
    public string Code { get; set; } = "";

    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [StringLength(1000)]
    public string Description { get; set; } = "";
}

public class DepartmentViewModel
{
    [Required, StringLength(20, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Mã khoa chỉ được gồm chữ, số và dấu gạch ngang.")]
    public string Code { get; set; } = "";

    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [StringLength(1000)]
    public string Description { get; set; } = "";
}

public class CatalogFilterViewModel
{
    public string Query { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Role { get; set; }
    public string? Status { get; set; }
    public string? DepartmentId { get; set; }
    public string? SubjectId { get; set; }
    public string? ClassId { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public DateTime? StartFrom { get; set; }
    public DateTime? StartTo { get; set; }
}

public sealed class PaginationViewModel
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public int FirstItem { get; init; }
    public int LastItem { get; init; }

    public static List<T> Apply<T>(
        IReadOnlyList<T> source,
        int requestedPage,
        int requestedPageSize,
        out PaginationViewModel pagination)
    {
        var pageSize = requestedPageSize is 10 or 20 or 50 ? requestedPageSize : 20;
        var totalPages = (int)Math.Ceiling(source.Count / (double)pageSize);
        var page = Math.Clamp(requestedPage, 1, Math.Max(totalPages, 1));
        var skip = (page - 1) * pageSize;
        pagination = new PaginationViewModel
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = source.Count,
            TotalPages = totalPages,
            FirstItem = source.Count == 0 ? 0 : skip + 1,
            LastItem = Math.Min(skip + pageSize, source.Count)
        };
        return source.Skip(skip).Take(pageSize).ToList();
    }
}

public class SubjectCatalogViewModel
{
    public CatalogFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();
    public IReadOnlyList<Department> Departments { get; init; } = Array.Empty<Department>();
    public IReadOnlyDictionary<string, string> DepartmentNames { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, int> ClassCounts { get; init; } = new Dictionary<string, int>();
    public PaginationViewModel? Pagination { get; init; }
}
public class TeacherSubjectsViewModel
{
    [Required] public string TeacherId { get; set; } = "";
    public List<string> SubjectIds { get; set; } = new();
}

public class LessonViewModel
{
    [Required] public string Title { get; set; } = "";
    [Required] public string Content { get; set; } = "";
    public bool IsPublished { get; set; }
}

public class UpdateClassViewModel
{
    [Required, StringLength(160, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(2000)]
    public string Description { get; set; } = "";
    [Required] public string SubjectId { get; set; } = "";
    [Required] public string TeacherId { get; set; } = "";
    [Required] public string AcademicTermId { get; set; } = "";
    [Range(1, 500)] public int EnrollmentCapacity { get; set; } = 40;
    public List<string> StudentIds { get; set; } = new();
}

public class UserPermissionViewModel
{
    [Required] public string UserId { get; set; } = "";
    public List<string> PermissionCodes { get; set; } = new();
    public string DepartmentId { get; set; } = "";
    public string ProgramId { get; set; } = "";
    [Range(1, 20)] public int CurrentSemester { get; set; } = 1;
    public List<string> TeachingSubjectIds { get; set; } = new();
}

public class PermissionDefinitionViewModel
{
    [Required, RegularExpression("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*){1,4}$")]
    public string Code { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, StringLength(60, MinimumLength = 2)] public string Category { get; set; } = "";
    [StringLength(500)] public string Description { get; set; } = "";
    [RegularExpression("^[A-Za-z][A-Za-z0-9]{0,79}$")] public string ControllerName { get; set; } = "";
    [RegularExpression("^[A-Za-z][A-Za-z0-9]{0,79}$")] public string ActionName { get; set; } = "";
}

public class PermissionManagementViewModel
{
    public UserPermissionViewModel Assignment { get; set; } = new();
    public PermissionDefinitionViewModel NewPermission { get; set; } = new();
    public User SelectedUser { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public List<Permission> Permissions { get; set; } = new();
    public List<AcademicProgram> Programs { get; set; } = new();
    public List<Department> Departments { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
}

public class ProgramCourseViewModel
{
    public string SubjectId { get; set; } = "";
    public bool Selected { get; set; }
    [Range(1, 20)] public int SemesterNumber { get; set; } = 1;
    [Range(1, 30)] public int Credits { get; set; } = 3;
    public bool IsRequired { get; set; } = true;
}

public class AcademicProgramViewModel
{
    public string Id { get; set; } = "";
    [Required] public string DepartmentId { get; set; } = "";
    [Required, StringLength(20, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$")]
    public string Code { get; set; } = "";
    [Required, StringLength(120, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(1000)] public string Description { get; set; } = "";
    public List<ProgramCourseViewModel> Curriculum { get; set; } = new();
}

public class AcademicTermViewModel
{
    [ValidateNever]
    public string Id { get; set; } = "";
    [Required, StringLength(30, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$")]
    public string Code { get; set; } = "";
    [Required, StringLength(120, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, StringLength(12, MinimumLength = 4)] public string AcademicYear { get; set; } = "";
    [Range(1, 3)] public int SemesterNumber { get; set; } = 1;
    [Required] public DateTime StartsAt { get; set; } = DateTime.Today;
    [Required] public DateTime EndsAt { get; set; } = DateTime.Today.AddMonths(4);
    [Required] public DateTime RegistrationOpensAt { get; set; } = DateTime.Today;
    [Required] public DateTime RegistrationClosesAt { get; set; } = DateTime.Today.AddDays(14);
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal TuitionPerCredit { get; set; }
    public bool IsActive { get; set; }
}

public sealed class StudentCourseProgressViewModel
{
    public string ClassId { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string SubjectCode { get; init; } = "";
    public string SubjectName { get; init; } = "";
    public string TermName { get; init; } = "";
    public string AcademicYear { get; init; } = "";
    public int Credits { get; init; }
    public int GradedAssignmentCount { get; init; }
    public int AssignmentCount { get; init; }
    public double? AverageAssignmentGrade { get; init; }
}

public sealed class StudentTuitionItemViewModel
{
    public string ClassId { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string SubjectCode { get; init; } = "";
    public string SubjectName { get; init; } = "";
    public string TermName { get; init; } = "";
    public int Credits { get; init; }
    public decimal TuitionPerCredit { get; init; }
    public long AmountVnd { get; init; }
    public string Status { get; init; } = "";
    public string? InvoiceId { get; init; }
    public bool CanPay { get; init; }
}

public sealed class StudentAcademicDashboardViewModel
{
    public string ProgramName { get; init; } = "";
    public int TotalCredits { get; init; }
    public double? OverallAssignmentAverage { get; init; }
    public List<StudentCourseProgressViewModel> Courses { get; init; } = new();
    public List<StudentTuitionItemViewModel> TuitionItems { get; init; } = new();
}

public sealed class CourseRegistrationReviewViewModel
{
    public string RegistrationId { get; init; } = "";
    public string StudentName { get; init; } = "";
    public string StudentEmail { get; init; } = "";
    public string ClassId { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string SubjectName { get; init; } = "";
    public string TermName { get; init; } = "";
    public DateTime RequestedAt { get; init; }
}

public class CourseRegistrationItemViewModel
{
    public string ClassId { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string SubjectCode { get; init; } = "";
    public string SubjectName { get; init; } = "";
    public string TeacherName { get; init; } = "";
    public string TermName { get; init; } = "";
    public int Enrolled { get; init; }
    public int Capacity { get; init; }
    public bool IsRegistered { get; init; }
    public bool IsPending { get; init; }
    public string RegistrationStatus { get; init; } = "";
}

public class AssignmentViewModel
{
    [Required] public string Title { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required] public DateTime DueDate { get; set; } = DateTime.Now.AddDays(7);
    public bool IsPublished { get; set; }
    public string UploadedFileIds { get; set; } = "";
}

public class SubmissionViewModel
{
    [StringLength(10000)] public string Content { get; set; } = "";
    public string UploadedFileIds { get; set; } = "";
}

public class GradeViewModel
{
    [Range(0, 10)] public double Grade { get; set; }
    public string TeacherComment { get; set; } = "";
}

public class TeacherAvailabilityViewModel
{
    [Required] public DateTimeOffset StartAt { get; set; } = DateTimeOffset.Now.AddDays(1);
    [Required] public DateTimeOffset EndAt { get; set; } = DateTimeOffset.Now.AddDays(1).AddHours(1);
    [StringLength(500)] public string Note { get; set; } = "";
}

public class TeacherAvailabilityPageViewModel
{
    public TeacherAvailabilityViewModel Request { get; set; } = new();
    public IReadOnlyList<AvailabilityListItem> Requests { get; init; } = Array.Empty<AvailabilityListItem>();
}

public class ScheduleSessionViewModel
{
    [Required] public string ClassId { get; set; } = "";
    [Required] public string SubjectId { get; set; } = "";
    [Required] public DateTimeOffset StartAt { get; set; } = DateTimeOffset.Now.AddDays(1);
    [Required] public DateTimeOffset EndAt { get; set; } = DateTimeOffset.Now.AddDays(1).AddHours(1);
    [Range(1, 500)] public int Capacity { get; set; } = 30;
}

public class SchedulePlannerViewModel
{
    public ScheduleSessionViewModel Request { get; set; } = new();
    public IReadOnlyList<TeachingAssignmentListItem> Classes { get; init; } = Array.Empty<TeachingAssignmentListItem>();
    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();
    public string TeacherName { get; init; } = "";
    public IReadOnlyList<AvailabilityListItem> Availabilities { get; init; } = Array.Empty<AvailabilityListItem>();
}

public class AvailabilityListItem
{
    public string Id { get; init; } = "";
    public string TeacherName { get; init; } = "";
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public string Note { get; init; } = "";
    public string Status { get; init; } = "";
}

public class SessionListItem
{
    public string Id { get; init; } = "";
    public string ClassId { get; init; } = "";
    public string SubjectId { get; init; } = "";
    public string SubjectName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string TeacherName { get; init; } = "";
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public int Capacity { get; init; }
    public int EnrolledCount { get; init; }
    public bool IsEnrolled { get; init; }
    public string Status { get; init; } = "";
    public string CancellationReason { get; init; } = "";
}

public class SessionDetailsViewModel
{
    public SessionListItem Session { get; init; } = new();
    public IReadOnlyList<SessionStudentViewModel> Students { get; init; } = Array.Empty<SessionStudentViewModel>();
}

public class SessionStudentViewModel
{
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTimeOffset EnrolledAt { get; init; }
}

public class CancelSessionViewModel
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = "";
}

public class SubmissionListItemViewModel
{
    public string Id { get; init; } = "";
    public string StudentName { get; init; } = "";
    public string StudentEmail { get; init; } = "";
    public string Content { get; init; } = "";
    public double? Grade { get; init; }
    public string TeacherComment { get; init; } = "";
    public IReadOnlyList<StoredFileReference> Attachments { get; init; } = Array.Empty<StoredFileReference>();
    public DateTimeOffset SubmittedAt { get; init; }
}
