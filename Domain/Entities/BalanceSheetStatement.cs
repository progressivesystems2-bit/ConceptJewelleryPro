using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class ProfitLossStatement
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public decimal SalesAmount { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal DirectExpenses { get; set; }
    public decimal DirectIncomes { get; set; }
    public decimal OpeningStockAmount { get; set; }
    public decimal ClosingStockAmount { get; set; }

    public decimal GrossProfit => (SalesAmount + ClosingStockAmount + DirectIncomes) - (OpeningStockAmount + PurchaseAmount + DirectExpenses);

    public decimal IndirectIncomes { get; set; }
    public decimal IndirectExpenses { get; set; }

    public decimal NetProfit => GrossProfit + IndirectIncomes - IndirectExpenses;

    public string FormattedSales => SalesAmount.ToString("N2", IndianCulture);
    public string FormattedPurchases => PurchaseAmount.ToString("N2", IndianCulture);
    public string FormattedGrossProfit => GrossProfit.ToString("N2", IndianCulture);
    public string FormattedNetProfit => NetProfit.ToString("N2", IndianCulture);
}

public sealed class BalanceSheetStatement
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public DateTime AsOnDate { get; set; } = DateTime.Today;
    public DateTime FromDate { get; set; } = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);

    public List<BalanceSheetSection> LiabilitiesSections { get; set; } = new();
    public List<BalanceSheetSection> AssetsSections { get; set; } = new();

    public ProfitLossStatement ProfitLoss { get; set; } = new();

    public decimal TotalLiabilities => LiabilitiesSections.Sum(s => s.TotalAmount);
    public decimal TotalAssets => AssetsSections.Sum(s => s.TotalAmount);

    public decimal Difference => TotalLiabilities - TotalAssets;
    public bool IsBalanced => Math.Abs(Difference) < 0.01m;

    public decimal TotalGoldLiabilities => LiabilitiesSections.Sum(s => s.TotalGoldWeight);
    public decimal TotalGoldAssets => AssetsSections.Sum(s => s.TotalGoldWeight);
    public decimal NetGoldBalance => TotalGoldAssets - TotalGoldLiabilities;

    public decimal TotalSilverLiabilities => LiabilitiesSections.Sum(s => s.TotalSilverWeight);
    public decimal TotalSilverAssets => AssetsSections.Sum(s => s.TotalSilverWeight);
    public decimal NetSilverBalance => TotalSilverAssets - TotalSilverLiabilities;

    public decimal CurrentAssetsAmount => AssetsSections
        .Where(s => s.SectionCategory.Equals("Current Assets", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Current", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Bank", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Cash", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Stock", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Debtors", StringComparison.OrdinalIgnoreCase))
        .Sum(s => s.TotalAmount);

    public decimal CurrentLiabilitiesAmount => LiabilitiesSections
        .Where(s => s.SectionCategory.Equals("Current Liabilities", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Current", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Creditors", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Duties", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Advance", StringComparison.OrdinalIgnoreCase) ||
                    s.SectionTitle.Contains("Provisions", StringComparison.OrdinalIgnoreCase))
        .Sum(s => s.TotalAmount);

    public decimal WorkingCapital => CurrentAssetsAmount - CurrentLiabilitiesAmount;

    public string FormattedTotalLiabilities => TotalLiabilities.ToString("N2", IndianCulture);
    public string FormattedTotalAssets => TotalAssets.ToString("N2", IndianCulture);
    public string FormattedDifference => Math.Abs(Difference).ToString("N2", IndianCulture);
    public string FormattedWorkingCapital => WorkingCapital.ToString("N2", IndianCulture);

    public string FormattedTotalGoldAssets => TotalGoldAssets != 0 ? $"{TotalGoldAssets:N3} g" : "-";
    public string FormattedTotalSilverAssets => TotalSilverAssets != 0 ? $"{TotalSilverAssets:N3} g" : "-";
    public string FormattedTotalGoldLiabilities => TotalGoldLiabilities != 0 ? $"{TotalGoldLiabilities:N3} g" : "-";
    public string FormattedTotalSilverLiabilities => TotalSilverLiabilities != 0 ? $"{TotalSilverLiabilities:N3} g" : "-";
}
