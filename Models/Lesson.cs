using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class Lesson
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId ClassId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsPublished { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
