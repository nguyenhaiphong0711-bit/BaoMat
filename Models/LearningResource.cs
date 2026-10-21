using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public static class ResourceStates
{
    public const string Draft = "Draft", Pending = "PendingReview", Approved = "Approved",
        Rejected = "Rejected", Published = "Published";
}
public sealed class LearningResource
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId OwnerId { get; set; }
    public int Revision { get; set; } = 1;
    public bool IsArchived { get; set; }
    public int? PublishedVersion { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ResourceVersion> Versions { get; set; } = new();
}
public sealed class ResourceVersion
{
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Source { get; set; } = "";
    public string License { get; set; } = "";
    public string UsageTerms { get; set; } = "";
    public string Access { get; set; } = "Subjects";
    public string ExternalUrl { get; set; } = "";
    public List<ObjectId> SubjectIds { get; set; } = new();
    public List<StoredFileReference> Files { get; set; } = new();
    public ObjectId EditorId { get; set; }
    public string EditorName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string State { get; set; } = ResourceStates.Draft;
    public DateTime? SubmittedAt { get; set; }
    public ObjectId? ReviewerId { get; set; }
    public string ReviewerName { get; set; } = "";
    public DateTime? ReviewedAt { get; set; }
    public string ReviewComment { get; set; } = "";
}
public sealed record ResourceRequest
{
    [Range(0, int.MaxValue)] public int Revision { get; init; }
    [Required, StringLength(200)] public string Title { get; init; } = "";
    [StringLength(10000)] public string Description { get; init; } = "";
    [Required, StringLength(80)] public string Category { get; init; } = "";
    [Required, StringLength(160)] public string AuthorName { get; init; } = "";
    [Required, StringLength(1000)] public string Source { get; init; } = "";
    [Required, StringLength(160)] public string License { get; init; } = "";
    [Required, StringLength(2000)] public string UsageTerms { get; init; } = "";
    [Required, RegularExpression("^(Subjects|School)$")] public string Access { get; init; } = "Subjects";
    [StringLength(2000)] public string ExternalUrl { get; init; } = "";
    [Required, MinLength(1), MaxLength(20)] public string[] SubjectIds { get; init; } = Array.Empty<string>();
    [MaxLength(5)] public string[] FileIds { get; init; } = Array.Empty<string>();
}
public sealed record ResourceActionRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; init; }
    public bool Approve { get; init; }
    [StringLength(2000)] public string Comment { get; init; } = "";
}
public sealed record ResourceHistoryResponse(int Number, string Title, string State, DateTime CreatedAt,
    string ReviewerName, DateTime? ReviewedAt, string ReviewComment);
public sealed record ResourceResponse(string Id, int Revision, int Version, string Title, string Description,
    string Category, string AuthorName, string Source, string License, string UsageTerms, string Access,
    string ExternalUrl, IReadOnlyList<string> SubjectIds, IReadOnlyList<string> SubjectNames,
    IReadOnlyList<ApiFileResponse> Files, string State, bool IsArchived, bool CanEdit, bool CanReview,
    bool CanPublish, bool CanSubmit, bool CanArchive, string ReviewComment, IReadOnlyList<ResourceHistoryResponse> History);
