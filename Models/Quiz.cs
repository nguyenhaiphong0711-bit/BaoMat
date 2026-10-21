using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public sealed class Quiz
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId ClassId { get; set; }
    public ObjectId OwnerId { get; set; }
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public int DurationMinutes { get; set; } = 30;
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public List<ObjectId> QuestionIds { get; set; } = new();
    public bool IsPublished { get; set; }
    public bool IsArchived { get; set; }
    public int Revision { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class QuizAttempt
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId QuizId { get; set; }
    public ObjectId StudentId { get; set; }
    public List<QuizQuestionSnapshot> Questions { get; set; } = new();
    public List<int> Answers { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public double? Score { get; set; }
}

public sealed class QuizQuestionSnapshot
{
    public ObjectId SourceQuestionId { get; set; }
    public string Content { get; set; } = "";
    public List<QuestionOption> Options { get; set; } = new();
    public int CorrectOption { get; set; }
    public string Explanation { get; set; } = "";
}

public sealed record QuizRequest
{
    [Range(0, int.MaxValue)] public int Revision { get; init; }
    [Required] public string ClassId { get; init; } = "";
    [Required, StringLength(200)] public string Title { get; init; } = "";
    [StringLength(5000)] public string Instructions { get; init; } = "";
    [Range(5, 180)] public int DurationMinutes { get; init; } = 30;
    public DateTime OpensAt { get; init; }
    public DateTime ClosesAt { get; init; }
    [Required, MinLength(1), MaxLength(50)] public string[] QuestionIds { get; init; } = Array.Empty<string>();
}

public sealed record QuizActionRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; init; }
}
public sealed record QuizSubmitRequest
{
    [Required, MaxLength(50)] public int[] Answers { get; init; } = Array.Empty<int>();
}

public sealed record QuizSummaryResponse(string Id, int Revision, string ClassId, string ClassName,
    string Title, int DurationMinutes, DateTime OpensAt, DateTime ClosesAt, int QuestionCount,
    bool IsPublished, bool IsArchived, string AttemptStatus, double? Score);

public sealed record QuizManageResponse(QuizSummaryResponse Quiz, string Instructions,
    IReadOnlyList<QuestionResponse> Questions, bool CanEdit, bool CanPublish, bool CanArchive,
    IReadOnlyList<QuizAttemptSummary> Attempts);

public sealed record QuizAttemptSummary(string StudentId, string StudentName, DateTime StartedAt,
    DateTime? SubmittedAt, double? Score);

public sealed record QuizAttemptResponse(string Id, string QuizId, string Title, string Instructions,
    DateTime StartedAt, DateTime ExpiresAt, DateTime? SubmittedAt, double? Score,
    IReadOnlyList<QuizAttemptQuestionResponse> Questions, IReadOnlyList<int> Answers);

public sealed record QuizAttemptQuestionResponse(string Content, IReadOnlyList<QuestionOption> Options,
    int? CorrectOption, string Explanation);
