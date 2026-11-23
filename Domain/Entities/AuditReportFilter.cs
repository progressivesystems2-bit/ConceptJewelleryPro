namespace ConceptJewelleryPro.Domain.Entities;

public enum AuditStatusFilter
{
    All = 0,
    UncheckedOnly = 1,
    CheckedOnly = 2
}

public class AuditReportFilter
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? User { get; set; }
    public string? EntryType { get; set; }
    public AuditStatusFilter Status { get; set; } = AuditStatusFilter.All;
    public string? SearchText { get; set; }
}
