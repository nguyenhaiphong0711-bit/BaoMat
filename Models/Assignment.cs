using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class Assignment
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId ClassId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime DueDate { get; set; }
    public bool IsPublished { get; set; }
    public bool IsArchived { get; set; }
    public List<StoredFileReference> Attachments { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
