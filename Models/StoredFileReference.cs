using MongoDB.Bson;

namespace LMS.Models;

public class StoredFileReference
{
    public ObjectId Id { get; set; }
    public string FileName { get; set; } = "";
    public long Length { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
}
