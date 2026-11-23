using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class BalanceSheetItem
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public string AccountName { get; set; } = string.Empty;
    public string HeadCategory { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string ScheduleName { get; set; } = string.Empty;
    public string GroupType { get; set; } = string.Empty; // Liabilities, Assets, Capital, etc.

    public decimal Amount { get; set; }
    public decimal GoldWeight { get; set; } // Fine / Gross Gold weight in grams
    public decimal SilverWeight { get; set; } // Fine / Gross Silver weight in grams

    public bool IsDebit { get; set; }
    public bool IsCredit { get; set; }

    public string FormattedAmount => Amount != 0 ? Amount.ToString("N2", IndianCulture) : "-";
    public string FormattedGoldWeight => GoldWeight != 0 ? $"{GoldWeight:N3} g" : "-";
    public string FormattedSilverWeight => SilverWeight != 0 ? $"{SilverWeight:N3} g" : "-";
}
