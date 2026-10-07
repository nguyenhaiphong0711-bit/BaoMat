using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class Submission
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId AssignmentId { get; set; }
    public ObjectId StudentId { get; set; }
    public string Content { get; set; } = "";
    public double? Grade { get; set; }
    public string TeacherComment { get; set; } = "";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? GradedAt { get; set; }
}
