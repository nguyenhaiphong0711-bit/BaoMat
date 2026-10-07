using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class TeacherAvailability
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId TeacherId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Note { get; set; } = "";
    public string Status { get; set; } = ScheduleStatuses.Pending;
    public ObjectId? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
