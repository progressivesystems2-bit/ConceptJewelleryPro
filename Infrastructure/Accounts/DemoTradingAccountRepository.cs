using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoTradingAccountRepository : ITradingAccountRepository
{
    public Task<(decimal GoldWeight, decimal SilverWeight, decimal Value)> GetEstimatedStockValuationAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((368.500m, 5500.000m, 3485000m));
    }

    public Task<TradingAccountStatement> GetTradingAccountAsync(TradingAccountFilter filter, CancellationToken cancellationToken = default)
    {
        var fromDate = filter.FromDate ?? new DateTime(2025, 4, 1);
        var toDate = filter.ToDate ?? DateTime.Today;

        var openingStockAmt = filter.ManualOpeningStockAmount ?? 2100000m;
        var openingGoldWt = filter.ManualOpeningGoldWeight ?? 280.000m;
        var openingSilverWt = filter.ManualOpeningSilverWeight ?? 4200.000m;

        var closingStockAmt = filter.ManualClosingStockAmount ?? 3485000m;
        var closingGoldWt = filter.ManualClosingGoldWeight ?? 368.500m;
        var closingSilverWt = filter.ManualClosingSilverWeight ?? 5500.000m;

        var statement = new TradingAccountStatement
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        // --- DEBIT ITEMS ---
        // 1. Opening Stock
        var opStock = new TradingAccountItem
        {
            Particulars = "To Opening Stock (Gold, Silver & Diamonds)",
            AccountName = "Opening Stock",
            HeadCategory = "Stock-in-Hand",
            ScheduleName = "Opening Stock",
            ItemType = TradingItemType.OpeningStock,
            GrossWeight = openingGoldWt,
            NetWeight = openingGoldWt * 0.98m,
            FineGoldWeight = openingGoldWt * 0.916m,
            SilverWeight = openingSilverWt,
            Amount = openingStockAmt,
            IsDebit = true,
            Notes = "Valuation as per previous year closing balance"
        };
        statement.DebitItems.Add(opStock);

        // 2. Purchases
        var purBullion = new TradingAccountItem
        {
            Particulars = "To Bullion Purchase (24K Standard Bars)",
            AccountName = "Bullion Purchases A/c",
            HeadCategory = "Purchases",
            ScheduleName = "Purchases",
            ItemType = TradingItemType.PurchaseBullion,
            GrossWeight = 450.000m,
            NetWeight = 450.000m,
            FineGoldWeight = 449.550m,
            PurityPercent = 99.9m,
            Amount = 3150000m,
            IsDebit = true,
            Notes = "Bank Delivery Bullion Bars"
        };
        var purOrnaments = new TradingAccountItem
        {
            Particulars = "To Ready Jewellery Purchases (Hallmarked 916)",
            AccountName = "Ornaments Purchases A/c",
            HeadCategory = "Purchases",
            ScheduleName = "Purchases",
            ItemType = TradingItemType.PurchaseJewellery,
            GrossWeight = 185.200m,
            NetWeight = 180.500m,
            FineGoldWeight = 165.338m,
            SilverWeight = 2400.000m,
            Amount = 1290000m,
            IsDebit = true,
            Notes = "Wholesale Ready Stock Imports & Local"
        };
        var purOldGold = new TradingAccountItem
        {
            Particulars = "To Old Gold Exchange Purchases (Urdhu Purchases)",
            AccountName = "Old Gold Purchase A/c",
            HeadCategory = "Purchases",
            ScheduleName = "Purchases",
            ItemType = TradingItemType.PurchaseOldGold,
            GrossWeight = 78.400m,
            NetWeight = 74.500m,
            FineGoldWeight = 62.580m,
            Amount = 380000m,
            IsDebit = true,
            Notes = "Customer Exchange & Direct Purchases"
        };
        statement.DebitItems.AddRange([purBullion, purOrnaments, purOldGold]);

        // 3. Direct Expenses
        var expKarigar = new TradingAccountItem
        {
            Particulars = "To Karigar Wages / Making Charges Paid",
            AccountName = "Karigar Mazdoori A/c",
            HeadCategory = "Direct Expenses",
            ScheduleName = "Direct Expenses",
            ItemType = TradingItemType.DirectExpenseKarigarWages,
            Amount = 128000m,
            IsDebit = true,
            Notes = "Manufacturing and setting labour"
        };
        var expHallmark = new TradingAccountItem
        {
            Particulars = "To BIS Hallmarking & Laser Soldering Charges",
            AccountName = "Hallmarking Charges A/c",
            HeadCategory = "Direct Expenses",
            ScheduleName = "Direct Expenses",
            ItemType = TradingItemType.DirectExpenseHallmarking,
            Amount = 24500m,
            IsDebit = true,
            Notes = "Assaying & Hallmarking Centre Fees"
        };
        var expMelting = new TradingAccountItem
        {
            Particulars = "To Refinery & Melting Charges",
            AccountName = "Melting & Refining Exp A/c",
            HeadCategory = "Direct Expenses",
            ScheduleName = "Direct Expenses",
            ItemType = TradingItemType.DirectExpenseMeltingLoss,
            Amount = 32500m,
            IsDebit = true,
            Notes = "Old Gold refining & bar casting"
        };
        statement.DebitItems.AddRange([expKarigar, expHallmark, expMelting]);

        // --- CREDIT ITEMS ---
        // 1. Sales
        var salesRetail = new TradingAccountItem
        {
            Particulars = "By Retail Jewellery Sales (Gold & Silver Ornaments)",
            AccountName = "Retail Sales A/c",
            HeadCategory = "Sales",
            ScheduleName = "Sales",
            ItemType = TradingItemType.SalesRetail,
            GrossWeight = 542.800m,
            NetWeight = 531.000m,
            FineGoldWeight = 486.396m,
            SilverWeight = 1100.000m,
            Amount = 5820000m,
            IsCredit = true,
            Notes = "Tax Invoice Showroom Billing"
        };
        var salesWholesale = new TradingAccountItem
        {
            Particulars = "By Wholesale & Bullion Dispatches",
            AccountName = "Wholesale Sales A/c",
            HeadCategory = "Sales",
            ScheduleName = "Sales",
            ItemType = TradingItemType.SalesWholesale,
            GrossWeight = 80.000m,
            NetWeight = 80.000m,
            FineGoldWeight = 79.920m,
            Amount = 720000m,
            IsCredit = true,
            Notes = "Inter-dealer trades"
        };
        statement.CreditItems.AddRange([salesRetail, salesWholesale]);

        // 2. Closing Stock
        var clStock = new TradingAccountItem
        {
            Particulars = "By Closing Stock-in-Hand (Physical Inventory)",
            AccountName = "Closing Stock",
            HeadCategory = "Stock-in-Hand",
            ScheduleName = "Closing Stock",
            ItemType = TradingItemType.ClosingStock,
            GrossWeight = closingGoldWt,
            NetWeight = closingGoldWt * 0.98m,
            FineGoldWeight = closingGoldWt * 0.916m,
            SilverWeight = closingSilverWt,
            Amount = closingStockAmt,
            IsCredit = true,
            Notes = "Valuation as per physical audit & lower of cost or NRV"
        };
        statement.CreditItems.Add(clStock);

        // All Items list
        statement.AllItems.AddRange(statement.DebitItems);
        statement.AllItems.AddRange(statement.CreditItems);

        // Gross Profit Item
        if (statement.IsGrossProfit)
        {
            statement.DebitItems.Add(new TradingAccountItem
            {
                Particulars = "To Gross Profit c/d (Transferred to Profit & Loss A/c)",
                AccountName = "Gross Profit Transfer",
                HeadCategory = "Gross Profit",
                ScheduleName = "Profit Margin",
                ItemType = TradingItemType.GrossProfitTransfer,
                Amount = statement.GrossProfit,
                IsDebit = true
            });
        }
        else
        {
            statement.CreditItems.Add(new TradingAccountItem
            {
                Particulars = "By Gross Loss c/d (Transferred to Profit & Loss A/c)",
                AccountName = "Gross Loss Transfer",
                HeadCategory = "Gross Loss",
                ScheduleName = "Profit Margin",
                ItemType = TradingItemType.GrossLossTransfer,
                Amount = Math.Abs(statement.GrossProfit),
                IsCredit = true
            });
        }

        // Metal Reconciliation
        statement.GoldReconciliation = new MetalReconciliationSummary
        {
            MetalType = "GOLD (in Grams)",
            OpeningWeight = openingGoldWt,
            InwardWeight = purBullion.GrossWeight + purOrnaments.GrossWeight + purOldGold.GrossWeight,
            OutwardWeight = salesRetail.GrossWeight + salesWholesale.GrossWeight,
            ClosingWeight = closingGoldWt
        };

        statement.SilverReconciliation = new MetalReconciliationSummary
        {
            MetalType = "SILVER (in Grams)",
            OpeningWeight = openingSilverWt,
            InwardWeight = purOrnaments.SilverWeight,
            OutwardWeight = salesRetail.SilverWeight,
            ClosingWeight = closingSilverWt
        };

        return Task.FromResult(statement);
    }
}
