using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class ActivityLog
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId? UserId { get; set; }
    public string Email { get; set; } = "";
    public string Action { get; set; } = "";
    public bool Success { get; set; }
    public string IpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
