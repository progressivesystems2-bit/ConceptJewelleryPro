using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public enum TradingItemType
{
    OpeningStock,
    PurchaseBullion,
    PurchaseJewellery,
    PurchaseOldGold,
    PurchaseGeneral,
    DirectExpenseMakingCharges,
    DirectExpenseKarigarWages,
    DirectExpenseHallmarking,
    DirectExpenseMeltingLoss,
    DirectExpenseFreight,
    DirectExpenseGeneral,
    DirectIncome,
    SalesRetail,
    SalesWholesale,
    SalesOldGoldRefinery,
    SalesGeneral,
    ClosingStock,
    GrossProfitTransfer,
    GrossLossTransfer
}

public sealed class TradingAccountItem
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public string Particulars { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string HeadCategory { get; set; } = string.Empty;
    public string ScheduleName { get; set; } = string.Empty;
    public TradingItemType ItemType { get; set; }

    public decimal GrossWeight { get; set; }
    public decimal NetWeight { get; set; }
    public decimal PurityPercent { get; set; }
    public decimal FineGoldWeight { get; set; }
    public decimal FineSilverWeight { get; set; }
    public decimal SilverWeight { get; set; }

    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }

    public bool IsDebit { get; set; }
    public bool IsCredit { get; set; }
    public string Notes { get; set; } = string.Empty;

    public string FormattedGrossWeight => GrossWeight != 0 ? $"{GrossWeight:N3} g" : "-";
    public string FormattedNetWeight => NetWeight != 0 ? $"{NetWeight:N3} g" : "-";
    public string FormattedFineGoldWeight => FineGoldWeight != 0 ? $"{FineGoldWeight:N3} g" : "-";
    public string FormattedFineSilverWeight => FineSilverWeight != 0 ? $"{FineSilverWeight:N3} g" : "-";
    public string FormattedAmount => Amount.ToString("N2", IndianCulture);
}
