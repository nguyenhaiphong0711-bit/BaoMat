using LMS.Models;
using System.ComponentModel.DataAnnotations;

namespace LMS.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
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

public class SubjectCatalogViewModel
{
    public CatalogFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();
    public IReadOnlyList<Department> Departments { get; init; } = Array.Empty<Department>();
    public IReadOnlyDictionary<string, string> DepartmentNames { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, int> ClassCounts { get; init; } = new Dictionary<string, int>();
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
    public List<string> StudentIds { get; set; } = new();
}

public class AssignmentViewModel
{
    [Required] public string Title { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required] public DateTime DueDate { get; set; } = DateTime.Now.AddDays(7);
    public bool IsPublished { get; set; }
}

public class SubmissionViewModel
{
    [Required] public string Content { get; set; } = "";
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
    public DateTimeOffset SubmittedAt { get; init; }
}
