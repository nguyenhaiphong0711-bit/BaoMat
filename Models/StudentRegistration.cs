using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public static class StudentRegistrationStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";
}

public class StudentRegistration
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId StudentId { get; set; }
    public ObjectId ClassId { get; set; }
    public ObjectId AcademicTermId { get; set; }
    public string Status { get; set; } = StudentRegistrationStatuses.Active;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? WithdrawnAt { get; set; }
}
