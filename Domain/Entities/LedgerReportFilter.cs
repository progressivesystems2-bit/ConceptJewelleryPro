namespace ConceptJewelleryPro.Domain.Entities;

public enum LedgerPrintOption
{
    Running,
    RunningDayTotal,
    WithNarration,
    MonthWise,
    WithAcknowledgment
}

public sealed class LedgerReportFilter
{
    public string AccountName { get; set; } = string.Empty;

    public string PhoneNo { get; set; } = string.Empty;

    public string RefName { get; set; } = string.Empty;

    public DateTime? FromDate { get; set; } = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);

    public DateTime? ToDate { get; set; } = DateTime.Today;

    public LedgerPrintOption PrintOption { get; set; } = LedgerPrintOption.Running;
}
