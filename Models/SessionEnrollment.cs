using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class SessionEnrollment
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId SessionId { get; set; }
    public ObjectId StudentId { get; set; }
    public string Status { get; set; } = ScheduleStatuses.Active;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
}
