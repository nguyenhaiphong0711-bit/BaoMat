using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public sealed class LessonCompletion
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId LessonId { get; set; }
    public ObjectId ClassId { get; set; }
    public ObjectId StudentId { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
