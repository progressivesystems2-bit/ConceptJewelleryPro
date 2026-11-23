using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class BalanceSheetSection
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public string SectionTitle { get; set; } = string.Empty;
    public string SectionCategory { get; set; } = string.Empty; // "Liabilities", "Assets", "Equity"
    public int DisplayOrder { get; set; }

    public List<BalanceSheetItem> Items { get; set; } = new();

    public decimal TotalAmount => Items.Sum(i => i.Amount);
    public decimal TotalGoldWeight => Items.Sum(i => i.GoldWeight);
    public decimal TotalSilverWeight => Items.Sum(i => i.SilverWeight);

    public string FormattedTotalAmount => TotalAmount != 0 ? TotalAmount.ToString("N2", IndianCulture) : "0.00";
    public string FormattedTotalGoldWeight => TotalGoldWeight != 0 ? $"{TotalGoldWeight:N3} g" : "-";
    public string FormattedTotalSilverWeight => TotalSilverWeight != 0 ? $"{TotalSilverWeight:N3} g" : "-";

    public bool HasItems => Items.Count > 0;
}
