using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

// Retained independently of the OTP, including after successful password reset.
public sealed class PasswordResetRequestLimit
{
    [BsonId] public ObjectId UserId { get; set; }
    public DateTime WindowStartedAt { get; set; }
    public DateTime LastRequestedAt { get; set; }
    public int Count { get; set; }
    public DateTime ExpiresAt { get; set; }
}
