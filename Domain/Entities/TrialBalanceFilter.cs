namespace ConceptJewelleryPro.Domain.Entities;

public enum TrialBalanceViewMode
{
    GroupSummary,
    Detailed
}

public sealed class TrialBalanceFilter
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string SelectedHeadCategory { get; set; } = "ALL";
    public string SearchText { get; set; } = string.Empty;
    public bool HideZeroBalances { get; set; } = true;
    public TrialBalanceViewMode ViewMode { get; set; } = TrialBalanceViewMode.GroupSummary;
}
