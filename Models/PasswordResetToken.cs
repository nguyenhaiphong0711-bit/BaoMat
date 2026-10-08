using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class PasswordResetToken
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSentAt { get; set; }
    public DateTime RequestWindowStartedAt { get; set; }
    public int RequestCount { get; set; }
    public int FailedAttempts { get; set; }
}
