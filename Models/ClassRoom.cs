using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class ClassRoom
{
    [BsonId] public ObjectId Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ObjectId SubjectId { get; set; }
    public ObjectId TeacherId { get; set; }
    public List<ObjectId> StudentIds { get; set; } = new();
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
