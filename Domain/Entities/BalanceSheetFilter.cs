namespace ConceptJewelleryPro.Domain.Entities;

public enum BalanceSheetViewMode
{
    HorizontalTFormat,
    VerticalSchedule
}

public sealed class BalanceSheetFilter
{
    public DateTime? FromDate { get; set; } = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
    public DateTime? ToDate { get; set; } = DateTime.Today;

    public decimal? ManualClosingStockAmount { get; set; }
    public decimal? ManualClosingGoldWeight { get; set; }
    public decimal? ManualClosingSilverWeight { get; set; }

    public bool HideZeroBalances { get; set; } = true;
    public BalanceSheetViewMode ViewMode { get; set; } = BalanceSheetViewMode.HorizontalTFormat;
}
