using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

// Contains permission snapshots only, never passwords or other account secrets.
public class PermissionMigration
{
    [BsonId] public ObjectId Id { get; set; }
    public string Version { get; set; } = "permissions-v2";
    public ObjectId UserId { get; set; }
    public string Role { get; set; } = "";
    public List<string> Before { get; set; } = new();
    public List<string> After { get; set; } = new();
    public bool Applied { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
