using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public static class TicketStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
}

public sealed class SupportTicket
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId StudentId { get; set; }
    public ObjectId DepartmentId { get; set; }
    public ObjectId? AssignedStaffId { get; set; }
    public string Subject { get; set; } = "";
    public string Category { get; set; } = "Khác";
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = TicketStatuses.Open;
    public int Revision { get; set; } = 1;
    public List<SupportMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

public sealed class SupportMessage
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public ObjectId SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string SenderRole { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public sealed record SupportCreateRequest
{
    [Required, StringLength(200)] public string Subject { get; init; } = "";
    [Required, StringLength(80)] public string Category { get; init; } = "Khác";
    [Required, RegularExpression("^(Low|Normal|High)$")] public string Priority { get; init; } = "Normal";
    [Required, StringLength(5000)] public string Content { get; init; } = "";
}

public sealed record SupportMessageRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; init; }
    [Required, StringLength(5000)] public string Content { get; init; } = "";
}

public sealed record SupportManageRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; init; }
    [Required, RegularExpression("^(Open|InProgress|Resolved|Closed)$")] public string Status { get; init; } = TicketStatuses.Open;
    public string? AssignedStaffId { get; init; }
}

public sealed record SupportMessageResponse(string Id, string SenderName, string SenderRole, string Content, DateTime SentAt);
public sealed record SupportTicketResponse(string Id, int Revision, string Subject, string Category, string Priority,
    string Status, string StudentName, string DepartmentName, string AssignedStaffName, string? AssignedStaffId,
    DateTime CreatedAt, DateTime UpdatedAt, bool CanReply, bool CanManage,
    IReadOnlyList<SupportMessageResponse> Messages);
