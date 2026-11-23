namespace ConceptJewelleryPro.Domain.Entities;

public sealed class TradingAccountFilter
{
    public DateTime? FromDate { get; set; } = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
    public DateTime? ToDate { get; set; } = DateTime.Today;

    public decimal? ManualOpeningStockAmount { get; set; }
    public decimal? ManualClosingStockAmount { get; set; }

    public decimal? ManualOpeningGoldWeight { get; set; }
    public decimal? ManualClosingGoldWeight { get; set; }

    public decimal? ManualOpeningSilverWeight { get; set; }
    public decimal? ManualClosingSilverWeight { get; set; }

    public bool HideZeroBalances { get; set; } = true;
    public string SelectedMetalType { get; set; } = "ALL";
    public bool IncludeDirectExpensesOnly { get; set; } = true;
}
