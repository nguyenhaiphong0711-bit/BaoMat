using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class ProgramCourse
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId ProgramId { get; set; }
    public ObjectId SubjectId { get; set; }
    public int SemesterNumber { get; set; } = 1;
    public int Credits { get; set; } = 3;
    public bool IsRequired { get; set; } = true;
}
