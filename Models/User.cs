using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class User
{
    [BsonId] public ObjectId Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Student;
    public List<ObjectId> TeachingSubjectIds { get; set; } = new();
    public ObjectId? DepartmentId { get; set; }
    public ObjectId? ProgramId { get; set; }
    public int CurrentSemester { get; set; } = 1;
    public List<string> PermissionCodes { get; set; } = new();
    public bool PermissionsInitialized { get; set; }
    public bool StudentPortalPermissionsInitialized { get; set; }
    public bool AcademicProfileInitialized { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
