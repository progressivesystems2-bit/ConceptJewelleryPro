using System;

namespace ConceptJewelleryPro.Domain.Entities;

public class WhatsAppBillTracker
{
    public long WhatsAppBillTrackerId { get; set; }
    public string Wamid { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.Now;
    public string? DeliveryStatus { get; set; }
    public string? Rating { get; set; }
    public string? Reply { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
