using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class MetalReconciliationSummary
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public string MetalType { get; set; } = "GOLD";

    public decimal OpeningWeight { get; set; }
    public decimal InwardWeight { get; set; }
    public decimal OutwardWeight { get; set; }
    public decimal ClosingWeight { get; set; }

    public decimal TotalInflow => OpeningWeight + InwardWeight;
    public decimal TotalOutflow => OutwardWeight + ClosingWeight;

    // Weight Difference (Inflow - Outflow). Negative indicates unaccounted shortage/melting loss; Positive indicates excess/gain
    public decimal Difference => TotalInflow - TotalOutflow;
    public bool IsTallied => Math.Abs(Difference) < 0.005m;

    public string FormattedOpening => $"{OpeningWeight:N3} g";
    public string FormattedInward => $"{InwardWeight:N3} g";
    public string FormattedOutward => $"{OutwardWeight:N3} g";
    public string FormattedClosing => $"{ClosingWeight:N3} g";
    public string FormattedDifference => $"{Difference:N3} g";
}

public sealed class TradingAccountStatement
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public DateTime FromDate { get; set; } = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
    public DateTime ToDate { get; set; } = DateTime.Today;

    public List<TradingAccountItem> DebitItems { get; set; } = new();
    public List<TradingAccountItem> CreditItems { get; set; } = new();
    public List<TradingAccountItem> AllItems { get; set; } = new();

    public decimal TotalOpeningStockAmount => DebitItems.Where(x => x.ItemType == TradingItemType.OpeningStock).Sum(x => x.Amount);
    public decimal TotalPurchasesAmount => DebitItems.Where(x => x.ItemType is TradingItemType.PurchaseBullion or TradingItemType.PurchaseJewellery or TradingItemType.PurchaseOldGold or TradingItemType.PurchaseGeneral).Sum(x => x.Amount);
    public decimal TotalDirectExpensesAmount => DebitItems.Where(x => x.ItemType is TradingItemType.DirectExpenseMakingCharges or TradingItemType.DirectExpenseKarigarWages or TradingItemType.DirectExpenseHallmarking or TradingItemType.DirectExpenseMeltingLoss or TradingItemType.DirectExpenseFreight or TradingItemType.DirectExpenseGeneral).Sum(x => x.Amount);

    public decimal TotalSalesAmount => CreditItems.Where(x => x.ItemType is TradingItemType.SalesRetail or TradingItemType.SalesWholesale or TradingItemType.SalesOldGoldRefinery or TradingItemType.SalesGeneral).Sum(x => x.Amount);
    public decimal TotalDirectIncomesAmount => CreditItems.Where(x => x.ItemType == TradingItemType.DirectIncome).Sum(x => x.Amount);
    public decimal TotalClosingStockAmount => CreditItems.Where(x => x.ItemType == TradingItemType.ClosingStock).Sum(x => x.Amount);

    public decimal TotalCostOfGoodsAvailable => TotalOpeningStockAmount + TotalPurchasesAmount + TotalDirectExpensesAmount;
    public decimal CostOfGoodsSold => TotalCostOfGoodsAvailable - TotalClosingStockAmount;

    public decimal GrossProfit => (TotalSalesAmount + TotalClosingStockAmount + TotalDirectIncomesAmount) - (TotalOpeningStockAmount + TotalPurchasesAmount + TotalDirectExpensesAmount);
    public bool IsGrossProfit => GrossProfit >= 0;

    public decimal GrossProfitPercentage => TotalSalesAmount > 0 ? (GrossProfit / TotalSalesAmount) * 100m : 0m;

    public decimal TotalDebitsTFormat => TotalOpeningStockAmount + TotalPurchasesAmount + TotalDirectExpensesAmount + (IsGrossProfit ? GrossProfit : 0m);
    public decimal TotalCreditsTFormat => TotalSalesAmount + TotalClosingStockAmount + TotalDirectIncomesAmount + (!IsGrossProfit ? Math.Abs(GrossProfit) : 0m);

    // Metal Summaries
    public decimal TotalOpeningGoldWeight => DebitItems.Where(x => x.ItemType == TradingItemType.OpeningStock).Sum(x => x.GrossWeight);
    public decimal TotalPurchasedGoldWeight => DebitItems.Where(x => x.ItemType is TradingItemType.PurchaseBullion or TradingItemType.PurchaseJewellery or TradingItemType.PurchaseOldGold or TradingItemType.PurchaseGeneral).Sum(x => x.GrossWeight);
    public decimal TotalSoldGoldWeight => CreditItems.Where(x => x.ItemType is TradingItemType.SalesRetail or TradingItemType.SalesWholesale or TradingItemType.SalesOldGoldRefinery or TradingItemType.SalesGeneral).Sum(x => x.GrossWeight);
    public decimal TotalClosingGoldWeight => CreditItems.Where(x => x.ItemType == TradingItemType.ClosingStock).Sum(x => x.GrossWeight);

    public decimal TotalOpeningSilverWeight => DebitItems.Where(x => x.ItemType == TradingItemType.OpeningStock).Sum(x => x.FineSilverWeight);
    public decimal TotalPurchasedSilverWeight => DebitItems.Where(x => x.ItemType is TradingItemType.PurchaseBullion or TradingItemType.PurchaseJewellery or TradingItemType.PurchaseOldGold or TradingItemType.PurchaseGeneral).Sum(x => x.FineSilverWeight);
    public decimal TotalSoldSilverWeight => CreditItems.Where(x => x.ItemType is TradingItemType.SalesRetail or TradingItemType.SalesWholesale or TradingItemType.SalesOldGoldRefinery or TradingItemType.SalesGeneral).Sum(x => x.FineSilverWeight);
    public decimal TotalClosingSilverWeight => CreditItems.Where(x => x.ItemType == TradingItemType.ClosingStock).Sum(x => x.FineSilverWeight);

    public MetalReconciliationSummary GoldReconciliation { get; set; } = new() { MetalType = "GOLD" };
    public MetalReconciliationSummary SilverReconciliation { get; set; } = new() { MetalType = "SILVER" };

    // Formatted strings
    public string FormattedTotalSales => TotalSalesAmount.ToString("N2", IndianCulture);
    public string FormattedTotalPurchases => TotalPurchasesAmount.ToString("N2", IndianCulture);
    public string FormattedTotalDirectExpenses => TotalDirectExpensesAmount.ToString("N2", IndianCulture);
    public string FormattedOpeningStock => TotalOpeningStockAmount.ToString("N2", IndianCulture);
    public string FormattedClosingStock => TotalClosingStockAmount.ToString("N2", IndianCulture);
    public string FormattedCostOfGoodsSold => CostOfGoodsSold.ToString("N2", IndianCulture);
    public string FormattedGrossProfit => Math.Abs(GrossProfit).ToString("N2", IndianCulture);
    public string FormattedGrossProfitPercentage => $"{GrossProfitPercentage:F2}%";
    public string FormattedTotalDebitsTFormat => TotalDebitsTFormat.ToString("N2", IndianCulture);
    public string FormattedTotalCreditsTFormat => TotalCreditsTFormat.ToString("N2", IndianCulture);
}
