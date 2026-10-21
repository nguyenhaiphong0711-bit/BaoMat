using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public sealed class LearningEvent
{
    [BsonId] public ObjectId Id { get; set; }
    public string Type { get; set; } = "";
    public ObjectId ActorId { get; set; }
    public string ActorRole { get; set; } = "";
    public ObjectId? StudentId { get; set; }
    public ObjectId? DepartmentId { get; set; }
    public ObjectId? ClassId { get; set; }
    public string EntityType { get; set; } = "";
    public ObjectId EntityId { get; set; }
    public string Summary { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public sealed record ReportMetric(string Key, string Label, double Value, string Unit);
public sealed record ReportGroup(string Key, string Label, double Value);
public sealed record ReportEvent(string Type, string Summary, DateTime OccurredAt);
public sealed record LearningReportResponse(string ScopeName, IReadOnlyList<ReportMetric> Metrics,
    IReadOnlyList<ReportGroup> SupportStatuses, IReadOnlyList<ReportEvent> RecentEvents);
