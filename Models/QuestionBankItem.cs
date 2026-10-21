using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public static class QuestionStates
{
    public const string Draft = "Draft";
    public const string Pending = "PendingReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Published = "Published";
}

public sealed class QuestionBankItem
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId OwnerId { get; set; }
    public ObjectId SubjectId { get; set; }
    public string Topic { get; set; } = "";
    public string Difficulty { get; set; } = "Medium";
    public string Content { get; set; } = "";
    public List<QuestionOption> Options { get; set; } = new();
    public int CorrectOption { get; set; }
    public string Explanation { get; set; } = "";
    public string State { get; set; } = QuestionStates.Draft;
    public int Revision { get; set; } = 1;
    public bool IsArchived { get; set; }
    public ObjectId EditorId { get; set; }
    public string EditorName { get; set; } = "";
    public ObjectId? ReviewerId { get; set; }
    public string ReviewerName { get; set; } = "";
    public string ReviewComment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public sealed class QuestionOption
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed record QuestionRequest
{
    [Range(0, int.MaxValue)] public int Revision { get; init; }
    [Required] public string SubjectId { get; init; } = "";
    [Required, StringLength(120)] public string Topic { get; init; } = "";
    [Required, RegularExpression("^(Easy|Medium|Hard)$")] public string Difficulty { get; init; } = "Medium";
    [Required, StringLength(5000)] public string Content { get; init; } = "";
    [Required, MinLength(2), MaxLength(6)] public string[] Options { get; init; } = Array.Empty<string>();
    [Range(0, 5)] public int CorrectOption { get; init; }
    [StringLength(5000)] public string Explanation { get; init; } = "";
}

public sealed record QuestionActionRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; init; }
    public bool Approve { get; init; }
    [StringLength(2000)] public string Comment { get; init; } = "";
}

public sealed record QuestionResponse(string Id, int Revision, string SubjectId, string SubjectName,
    string Topic, string Difficulty, string Content, IReadOnlyList<QuestionOption> Options,
    int CorrectOption, string Explanation, string State, bool IsArchived, string EditorName,
    string ReviewerName, string ReviewComment, bool CanEdit, bool CanSubmit, bool CanReview,
    bool CanPublish, bool CanArchive);
