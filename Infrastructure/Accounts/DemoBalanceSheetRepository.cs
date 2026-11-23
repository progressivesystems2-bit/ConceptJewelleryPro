using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoBalanceSheetRepository : IBalanceSheetRepository
{
    public Task<decimal> GetEstimatedClosingStockValueAsync(DateTime asOnDate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(3485000m);
    }

    public Task<BalanceSheetStatement> GetBalanceSheetAsync(BalanceSheetFilter filter, CancellationToken cancellationToken = default)
    {
        var statement = new BalanceSheetStatement
        {
            AsOnDate = filter.ToDate ?? DateTime.Today,
            FromDate = filter.FromDate ?? new DateTime(2025, 4, 1),
            ProfitLoss = new ProfitLossStatement
            {
                SalesAmount = 6540000m,
                PurchaseAmount = 4820000m,
                DirectExpenses = 185000m,
                OpeningStockAmount = 2100000m,
                ClosingStockAmount = filter.ManualClosingStockAmount ?? 3485000m,
                IndirectExpenses = 340000m,
                IndirectIncomes = 25000m
            }
        };

        // 1. Capital Section
        var capitalSec = new BalanceSheetSection
        {
            SectionTitle = "Capital Account & Equity",
            SectionCategory = "Equity",
            DisplayOrder = 1,
            Items =
            [
                new BalanceSheetItem { AccountName = "PROPRIETOR CAPITAL A/C", HeadCategory = "Capital", ScheduleName = "Capital Account", GroupType = "Equity", Amount = 2500000m, IsCredit = true },
                new BalanceSheetItem { AccountName = "PARTNER SHARE CAPITAL", HeadCategory = "Capital", ScheduleName = "Capital Account", GroupType = "Equity", Amount = 1500000m, IsCredit = true },
                new BalanceSheetItem { AccountName = "PROPRIETOR DRAWINGS A/C", HeadCategory = "Capital", ScheduleName = "Capital Account", GroupType = "Equity", Amount = -250000m, IsDebit = true }
            ]
        };

        // 2. Reserves & Surplus Section
        var reservesSec = new BalanceSheetSection
        {
            SectionTitle = "Reserves & Surplus",
            SectionCategory = "Equity",
            DisplayOrder = 2,
            Items =
            [
                new BalanceSheetItem { AccountName = "GENERAL RESERVE", HeadCategory = "Reserves & Surplus", ScheduleName = "Reserves & Surplus", GroupType = "Equity", Amount = 450000m, IsCredit = true },
                new BalanceSheetItem { AccountName = "PROFIT & LOSS A/C (Current Year Net Profit)", HeadCategory = "Reserves & Surplus", ScheduleName = "Reserves & Surplus", GroupType = "Equity", Amount = statement.ProfitLoss.NetProfit, IsCredit = true }
            ]
        };

        // 3. Secured Loans Section
        var securedLoansSec = new BalanceSheetSection
        {
            SectionTitle = "Secured Loans (Bank/OD)",
            SectionCategory = "Liabilities",
            DisplayOrder = 3,
            Items =
            [
                new BalanceSheetItem { AccountName = "HDFC BANK CC/OD A/C", HeadCategory = "Secured Loans", ScheduleName = "Secured Loans", GroupType = "Liabilities", Amount = 850000m, IsCredit = true },
                new BalanceSheetItem { AccountName = "SBI GOLD METAL LOAN A/C", HeadCategory = "Secured Loans", ScheduleName = "Secured Loans", GroupType = "Liabilities", Amount = 1200000m, GoldWeight = 150.000m, IsCredit = true }
            ]
        };

        // 4. Current Liabilities Section
        var creditorsSec = new BalanceSheetSection
        {
            SectionTitle = "Sundry Creditors (Bullion / Suppliers)",
            SectionCategory = "Current Liabilities",
            DisplayOrder = 4,
            Items =
            [
                new BalanceSheetItem { AccountName = "MANOJ BULLION TRADERS", HeadCategory = "Sundry Creditors", ScheduleName = "Sundry Creditors", GroupType = "Current Liabilities", Amount = 640000m, GoldWeight = 85.250m, IsCredit = true },
                new BalanceSheetItem { AccountName = "SURAT DIAMOND IMPEX", HeadCategory = "Sundry Creditors", ScheduleName = "Sundry Creditors", GroupType = "Current Liabilities", Amount = 320000m, IsCredit = true },
                new BalanceSheetItem { AccountName = "GANESH KARIGAR (JOB WORK)", HeadCategory = "Sundry Creditors", ScheduleName = "Sundry Creditors", GroupType = "Current Liabilities", Amount = 85000m, GoldWeight = 12.400m, IsCredit = true }
            ]
        };

        var custAdvanceSec = new BalanceSheetSection
        {
            SectionTitle = "Customer Advances & Saving Schemes",
            SectionCategory = "Current Liabilities",
            DisplayOrder = 5,
            Items =
            [
                new BalanceSheetItem { AccountName = "JEWELLERY SAVING SCHEME DEPOSITS", HeadCategory = "Customer Advances", ScheduleName = "Customer Advances", GroupType = "Current Liabilities", Amount = 420000m, IsCredit = true },
                new BalanceSheetItem { AccountName = "ADVANCE ORDER BOOKINGS", HeadCategory = "Customer Advances", ScheduleName = "Customer Advances", GroupType = "Current Liabilities", Amount = 180000m, GoldWeight = 25.000m, IsCredit = true }
            ]
        };

        var statutorySec = new BalanceSheetSection
        {
            SectionTitle = "Duties & Taxes (GST / Statutory)",
            SectionCategory = "Current Liabilities",
            DisplayOrder = 6,
            Items =
            [
                new BalanceSheetItem { AccountName = "GST PAYABLE (OUTPUT - INPUT)", HeadCategory = "Duties & Taxes", ScheduleName = "Duties & Taxes", GroupType = "Current Liabilities", Amount = 115000m, IsCredit = true },
                new BalanceSheetItem { AccountName = "TDS PAYABLE (SECTION 194C)", HeadCategory = "Duties & Taxes", ScheduleName = "Duties & Taxes", GroupType = "Current Liabilities", Amount = 18500m, IsCredit = true }
            ]
        };

        // --- ASSETS ---
        // 1. Fixed Assets
        var fixedAssetsSec = new BalanceSheetSection
        {
            SectionTitle = "Fixed Assets",
            SectionCategory = "Assets",
            DisplayOrder = 1,
            Items =
            [
                new BalanceSheetItem { AccountName = "SHOWROOM FURNITURE & DISPLAY FIXTURES", HeadCategory = "Fixed Assets", ScheduleName = "Fixed Assets", GroupType = "Assets", Amount = 1450000m, IsDebit = true },
                new BalanceSheetItem { AccountName = "SECURITY VAULT & SAFES", HeadCategory = "Fixed Assets", ScheduleName = "Fixed Assets", GroupType = "Assets", Amount = 550000m, IsDebit = true },
                new BalanceSheetItem { AccountName = "LASER SOLDERING & TESTING MACHINES", HeadCategory = "Fixed Assets", ScheduleName = "Fixed Assets", GroupType = "Assets", Amount = 380000m, IsDebit = true },
                new BalanceSheetItem { AccountName = "COMPUTER HARDWARE & POS SYSTEMS", HeadCategory = "Fixed Assets", ScheduleName = "Fixed Assets", GroupType = "Assets", Amount = 120000m, IsDebit = true }
            ]
        };

        // 2. Stock-in-Hand (Inventories)
        var inventorySec = new BalanceSheetSection
        {
            SectionTitle = "Stock-in-Hand (Inventories)",
            SectionCategory = "Current Assets",
            DisplayOrder = 2,
            Items =
            [
                new BalanceSheetItem { AccountName = "GOLD ORNAMENTS STOCK (FINISHED GOODS)", HeadCategory = "Inventories", ScheduleName = "Stock-in-Hand", GroupType = "Current Assets", Amount = 2150000m, GoldWeight = 320.500m, IsDebit = true },
                new BalanceSheetItem { AccountName = "SILVER ARTICLES & PAYAL STOCK", HeadCategory = "Inventories", ScheduleName = "Stock-in-Hand", GroupType = "Current Assets", Amount = 485000m, SilverWeight = 5500.000m, IsDebit = true },
                new BalanceSheetItem { AccountName = "LOOSE CERTIFIED DIAMONDS & PRECIOUS STONES", HeadCategory = "Inventories", ScheduleName = "Stock-in-Hand", GroupType = "Current Assets", Amount = 520000m, IsDebit = true },
                new BalanceSheetItem { AccountName = "RAW FINE GOLD & KARIGAR WIP STOCK", HeadCategory = "Inventories", ScheduleName = "Stock-in-Hand", GroupType = "Current Assets", Amount = 330000m, GoldWeight = 48.000m, IsDebit = true }
            ]
        };

        // 3. Sundry Debtors
        var debtorsSec = new BalanceSheetSection
        {
            SectionTitle = "Sundry Debtors (Receivables)",
            SectionCategory = "Current Assets",
            DisplayOrder = 3,
            Items =
            [
                new BalanceSheetItem { AccountName = "RAJESH JEWELLERS WHOLESALE", HeadCategory = "Sundry Debtors", ScheduleName = "Sundry Debtors", GroupType = "Current Assets", Amount = 360000m, GoldWeight = 45.000m, IsDebit = true },
                new BalanceSheetItem { AccountName = "VIP RETAIL CLIENTS OUTSTANDING", HeadCategory = "Sundry Debtors", ScheduleName = "Sundry Debtors", GroupType = "Current Assets", Amount = 175000m, IsDebit = true }
            ]
        };

        // 4. Cash & Bank Balances
        var cashBankSec = new BalanceSheetSection
        {
            SectionTitle = "Cash & Bank Balances",
            SectionCategory = "Current Assets",
            DisplayOrder = 4,
            Items =
            [
                new BalanceSheetItem { AccountName = "CASH IN HAND (SHOWROOM COUNTER)", HeadCategory = "Cash", ScheduleName = "Cash & Bank", GroupType = "Current Assets", Amount = 185000m, IsDebit = true },
                new BalanceSheetItem { AccountName = "HDFC CURRENT A/C", HeadCategory = "Bank Accounts", ScheduleName = "Cash & Bank", GroupType = "Current Assets", Amount = 640000m, IsDebit = true },
                new BalanceSheetItem { AccountName = "ICICI BANK CURRENT A/C", HeadCategory = "Bank Accounts", ScheduleName = "Cash & Bank", GroupType = "Current Assets", Amount = 345000m, IsDebit = true }
            ]
        };

        // 5. Loans & Advances
        var advancesSec = new BalanceSheetSection
        {
            SectionTitle = "Loans, Advances & Security Deposits",
            SectionCategory = "Current Assets",
            DisplayOrder = 5,
            Items =
            [
                new BalanceSheetItem { AccountName = "SHOWROOM RENT SECURITY DEPOSIT", HeadCategory = "Deposits", ScheduleName = "Loans & Advances", GroupType = "Current Assets", Amount = 200000m, IsDebit = true },
                new BalanceSheetItem { AccountName = "STAFF ADVANCES", HeadCategory = "Loans & Advances", ScheduleName = "Loans & Advances", GroupType = "Current Assets", Amount = 35000m, IsDebit = true }
            ]
        };

        statement.LiabilitiesSections.AddRange([capitalSec, reservesSec, securedLoansSec, creditorsSec, custAdvanceSec, statutorySec]);
        statement.AssetsSections.AddRange([fixedAssetsSec, inventorySec, debtorsSec, cashBankSec, advancesSec]);

        // Adjust for any small discrepancy to match total liabilities exactly in demo
        var diff = statement.TotalLiabilities - statement.TotalAssets;
        if (diff != 0)
        {
            var closingItem = inventorySec.Items.First();
            closingItem.Amount += diff;
            statement.ProfitLoss.ClosingStockAmount = inventorySec.TotalAmount;
        }

        return Task.FromResult(statement);
    }
}
