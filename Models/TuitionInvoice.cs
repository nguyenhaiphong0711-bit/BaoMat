using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

public static class TuitionInvoiceStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
}

public class TuitionInvoice
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId StudentId { get; set; }
    public ObjectId ClassId { get; set; }
    public ObjectId AcademicTermId { get; set; }
    public int Credits { get; set; }
    public decimal TuitionPerCredit { get; set; }
    public long AmountVnd { get; set; }
    public string Status { get; set; } = TuitionInvoiceStatuses.Pending;
    public string VnpayTransactionReference { get; set; } = "";
    public string VnpayTransactionNumber { get; set; } = "";
    public DateTime? PaymentInitiatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
