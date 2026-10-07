using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class ClassSession
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId ClassId { get; set; }
    public ObjectId TeacherId { get; set; }
    public ObjectId? AvailabilityId { get; set; }
    public ObjectId SubjectId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public string Status { get; set; } = ScheduleStatuses.Scheduled;
    public ObjectId CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public string CancellationReason { get; set; } = "";
}
