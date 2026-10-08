using System.ComponentModel.DataAnnotations;

namespace LMS.Models;

/// <summary>Credentials used to create an authenticated session.</summary>
public sealed record ApiLoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    [Required, StringLength(128)]
    public required string Password { get; init; }
}

/// <summary>Identity and role information for the current session.</summary>
public sealed record ApiIdentityResponse(string Id, string FullName, string Email, string Role, IReadOnlyList<string> PermissionCodes);

/// <summary>A user record without password or authentication internals.</summary>
public sealed record ApiUserResponse(string Id, string FullName, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt);

/// <summary>Fields required to create a user account.</summary>
public sealed record ApiCreateUserRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public required string FullName { get; init; }

    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    [Required, StringLength(128, MinimumLength = 8)]
    public required string Password { get; init; }

    [Required, RegularExpression("^(Admin|Teacher|Student)$")]
    public required string Role { get; init; }
}

/// <summary>A class and its subject, teacher, and student membership.</summary>
public sealed record ApiClassResponse(
    string Id,
    string Name,
    string Description,
    string SubjectId,
    string TeacherId,
    IReadOnlyList<string> StudentIds,
    DateTimeOffset CreatedAt,
    string? AcademicTermId,
    int EnrollmentCapacity);

/// <summary>Fields required to create a class.</summary>
public sealed record ApiCreateClassRequest
{
    [Required, StringLength(160, MinimumLength = 1)]
    public required string Name { get; init; }

    [StringLength(2000)]
    public string Description { get; init; } = "";

    [Required]
    public required string SubjectId { get; init; }

    [Required]
    public required string TeacherId { get; init; }

    public string AcademicTermId { get; init; } = "";
    [Range(1, 500)] public int EnrollmentCapacity { get; init; } = 40;

    public IReadOnlyList<string> StudentIds { get; init; } = Array.Empty<string>();
}

/// <summary>A subject in the course catalog and its department.</summary>
public sealed record ApiSubjectResponse(string Id, string DepartmentId, string Code, string Name, string Description, DateTimeOffset CreatedAt);

/// <summary>Fields required to create or update a subject.</summary>
public sealed record ApiSubjectRequest
{
    [Required]
    public required string DepartmentId { get; init; }

    [Required, StringLength(20, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$")]
    public required string Code { get; init; }

    [Required, StringLength(120, MinimumLength = 2)]
    public required string Name { get; init; }

    [StringLength(1000)]
    public string Description { get; init; } = "";
}

/// <summary>A department in the course catalog.</summary>
public sealed record ApiDepartmentResponse(string Id, string Code, string Name, string Description, DateTimeOffset CreatedAt);

/// <summary>Fields required to create or update a department.</summary>
public sealed record ApiDepartmentRequest
{
    [Required, StringLength(20, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$")]
    public required string Code { get; init; }

    [Required, StringLength(120, MinimumLength = 2)]
    public required string Name { get; init; }

    [StringLength(1000)]
    public string Description { get; init; } = "";
}

/// <summary>Subject identifiers assigned to an active teacher.</summary>
public sealed record ApiTeacherSubjectsRequest
{
    public IReadOnlyList<string> SubjectIds { get; init; } = Array.Empty<string>();
}

/// <summary>Assigns an active teacher to an existing class.</summary>
public sealed record ApiTeachingAssignmentRequest
{
    [Required]
    public required string TeacherId { get; init; }
}

/// <summary>A lesson returned to an authorized class member.</summary>
public sealed record ApiLessonResponse(string Id, string ClassId, string Title, string Content, bool IsPublished, DateTimeOffset CreatedAt);

/// <summary>Fields required to create or update a lesson.</summary>
public sealed record ApiLessonRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Title { get; init; }

    [Required, StringLength(20000, MinimumLength = 1)]
    public required string Content { get; init; }

    public bool IsPublished { get; init; }
}

/// <summary>An assignment returned to an authorized class member.</summary>
public sealed record ApiAssignmentResponse(string Id, string ClassId, string Title, string Description, DateTimeOffset DueDate, bool IsPublished, DateTimeOffset CreatedAt);

/// <summary>Fields required to create an assignment.</summary>
public sealed record ApiAssignmentRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Title { get; init; }

    [Required, StringLength(20000, MinimumLength = 1)]
    public required string Description { get; init; }

    public required DateTimeOffset DueDate { get; init; }

    public bool IsPublished { get; init; }
}

/// <summary>A student's submission without unrelated account data.</summary>
public sealed record ApiSubmissionResponse(string Id, string AssignmentId, string StudentId, string Content, double? Grade, string TeacherComment, DateTimeOffset SubmittedAt, DateTimeOffset? GradedAt);

/// <summary>Content submitted by a student.</summary>
public sealed record ApiSubmissionRequest
{
    [Required, StringLength(20000, MinimumLength = 1)]
    public required string Content { get; init; }
}

/// <summary>A grade and optional teacher feedback.</summary>
public sealed record ApiGradeRequest
{
    [Range(0, 10)]
    public double Grade { get; init; }

    [StringLength(4000)]
    public string TeacherComment { get; init; } = "";
}

/// <summary>A recorded authentication event available to administrators.</summary>
public sealed record ApiActivityLogResponse(string Id, string Email, string Action, bool Success, string IpAddress, string Message, DateTimeOffset CreatedAt);

/// <summary>Times during which a teacher is available to teach.</summary>
public sealed record ApiTeacherAvailabilityRequest
{
    [Required]
    public required DateTimeOffset StartAt { get; init; }

    [Required]
    public required DateTimeOffset EndAt { get; init; }

    [StringLength(500)]
    public string Note { get; init; } = "";
}

/// <summary>An administrator's decision about a teacher availability request.</summary>
public sealed record ApiAvailabilityReviewRequest
{
    public bool Approve { get; init; }
}

/// <summary>Schedules a class session for its assigned teacher and subject.</summary>
public sealed record ApiCreateSessionRequest
{
    [Required]
    public required string ClassId { get; init; }

    [Required]
    public required string SubjectId { get; init; }

    [Required]
    public required DateTimeOffset StartAt { get; init; }

    [Required]
    public required DateTimeOffset EndAt { get; init; }

    [Range(1, 500)]
    public int Capacity { get; init; } = 30;
}

/// <summary>A reason for cancelling a class session.</summary>
public sealed record ApiCancelSessionRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public required string Reason { get; init; }
}

/// <summary>A scheduled class session with enrollment capacity information.</summary>
public sealed record ApiSessionResponse(
    string Id,
    string ClassId,
    string SubjectId,
    string SubjectName,
    string ClassName,
    string TeacherName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int Capacity,
    int EnrolledCount,
    bool IsEnrolled,
    string Status,
    string CancellationReason);

/// <summary>A teacher availability request and its approval state.</summary>
public sealed record ApiTeacherAvailabilityResponse(
    string Id,
    string TeacherId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Note,
    string Status);
