using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public static class CourseOpeningStatuses
{
    public const string Pending = "Pending", Processing = "Processing", Approved = "Approved", Rejected = "Rejected";
}

public sealed class CourseOpeningRequest
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId TeacherId { get; set; }
    public ObjectId SubjectId { get; set; }
    public ObjectId AcademicTermId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int EnrollmentCapacity { get; set; } = 40;
    public string Status { get; set; } = CourseOpeningStatuses.Pending;
    public string DecisionComment { get; set; } = "";
    public ObjectId? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public ObjectId? CreatedClassId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed record CourseOpeningCreateRequest
{
    [Required] public string SubjectId { get; init; } = "";
    [Required] public string AcademicTermId { get; init; } = "";
    [Required, StringLength(160, MinimumLength = 2)] public string Name { get; init; } = "";
    [StringLength(2000)] public string Description { get; init; } = "";
    [Range(1, 500)] public int EnrollmentCapacity { get; init; } = 40;
}

public sealed record CourseOpeningDecisionRequest
{
    public bool Approve { get; init; }
    [StringLength(1000)] public string Comment { get; init; } = "";
}

public sealed record CourseOpeningResponse(string Id, string TeacherName, string SubjectName, string TermName,
    string Name, string Description, int EnrollmentCapacity, string Status, string DecisionComment,
    string? CreatedClassId, DateTime CreatedAt, DateTime? ReviewedAt);
