using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public class AcademicTerm
{
    [BsonId] public ObjectId Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string AcademicYear { get; set; } = "";
    public int SemesterNumber { get; set; } = 1;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime RegistrationOpensAt { get; set; }
    public DateTime RegistrationClosesAt { get; set; }
    public decimal TuitionPerCredit { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
