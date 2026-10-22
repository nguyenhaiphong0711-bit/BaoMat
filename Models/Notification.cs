using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public sealed class Notification
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Link { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public sealed record NotificationResponse(string Id, string Type, string Title, string Message, string Link,
    DateTime CreatedAt, DateTime? ReadAt);
