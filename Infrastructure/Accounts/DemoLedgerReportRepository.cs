using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoLedgerReportRepository : ILedgerReportRepository
{
    private static readonly List<Account> SampleAccounts =
    [
        new()
        {
            PartyCode = "ACC001",
            AccountName = "RAJESH JEWELLERS",
            HeadCategory = "Sundry Debtors",
            PhoneNo = "9876543210",
            RefName = "RAJESH SHARMA",
            OpAmt1 = 25000m,
            OpGoldWeight = 15.500m,
            City = "CUTTACK",
            State = "ODISHA"
        },
        new()
        {
            PartyCode = "ACC002",
            AccountName = "MANOJ BULLION TRADERS",
            HeadCategory = "Sundry Creditors",
            PhoneNo = "9123456780",
            RefName = "MANOJ PATEL",
            OpAmt1 = 150000m,
            OpGoldWeight = 50.000m,
            City = "BHUBANESWAR",
            State = "ODISHA"
        },
        new()
        {
            PartyCode = "ACC003",
            AccountName = "PRIYA ENTERPRISES",
            HeadCategory = "Debtors",
            PhoneNo = "9988776655",
            RefName = "PRIYA MOHAPATRA",
            OpAmt1 = 12000m,
            OpGoldWeight = 0m,
            City = "CUTTACK",
            State = "ODISHA"
        },
        new()
        {
            PartyCode = "ACC004",
            AccountName = "SHREE GANESH ARTISIANS",
            HeadCategory = "Artisians",
            PhoneNo = "9437012345",
            RefName = "GANESH DAS",
            OpAmt1 = 5000m,
            OpGoldWeight = 32.750m,
            City = "CUTTACK",
            State = "ODISHA"
        },
        new()
        {
            PartyCode = "ACC005",
            AccountName = "HDFC BANK",
            HeadCategory = "Bank A/c",
            PhoneNo = "0671234567",
            RefName = "MAIN BRANCH",
            OpAmt1 = 500000m,
            OpGoldWeight = 0m,
            City = "CUTTACK",
            State = "ODISHA"
        },
        new()
        {
            PartyCode = "ACC006",
            AccountName = "CASH ACCOUNT",
            HeadCategory = "Cash in Hand",
            PhoneNo = "",
            RefName = "MAIN CASH COUNTER",
            OpAmt1 = 100000m,
            OpGoldWeight = 0m,
            City = "CUTTACK",
            State = "ODISHA"
        }
    ];

    public Task<IReadOnlyList<Account>> GetLedgerAccountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Account>>(SampleAccounts);
    }

    public Task<LedgerAccountSummary> GetAccountSummaryAsync(string accountName, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var acc = SampleAccounts.FirstOrDefault(a => string.Equals(a.AccountName, accountName, StringComparison.OrdinalIgnoreCase)) ?? SampleAccounts[0];

        var summary = new LedgerAccountSummary
        {
            AccountName = acc.AccountName,
            Category = acc.HeadCategory,
            PhoneNo = acc.PhoneNo,
            RefName = acc.RefName,
            OpeningAmount = acc.OpAmt1,
            OpeningType = acc.HeadCategory.Contains("Creditor") ? "Cr" : "Dr",
            TotalDebits = 145200m,
            TotalCredits = 98500m,
            ClosingBalance = acc.OpAmt1 + 145200m - 98500m,
            ClosingType = "Dr",
            OpeningGoldWeight = acc.OpGoldWeight,
            OpeningWeightType = "Dr",
            TotalWeightIssued = 45.250m,
            TotalWeightReceived = 38.100m,
            ClosingGoldWeight = acc.OpGoldWeight + 45.250m - 38.100m,
            ClosingWeightType = "Dr"
        };

        return Task.FromResult(summary);
    }

    public Task<IReadOnlyList<LedgerEntry>> GetLedgerInRupeesAsync(LedgerReportFilter filter, CancellationToken cancellationToken = default)
    {
        var acc = SampleAccounts.FirstOrDefault(a => string.Equals(a.AccountName, filter.AccountName, StringComparison.OrdinalIgnoreCase)) ?? SampleAccounts[0];
        var fromDate = filter.FromDate ?? new DateTime(DateTime.Today.Year, 4, 1);

        var list = new List<LedgerEntry>
        {
            new()
            {
                VouDate = fromDate,
                VouNo = "-",
                VouType = "Opening Balance",
                Head = "Opening Balance",
                Debit = acc.OpAmt1,
                Credit = 0m,
                Balance = acc.OpAmt1,
                BalanceType = "Dr",
                Narration = "Opening Balance as on " + fromDate.ToString("dd/MM/yyyy")
            },
            new()
            {
                VouDate = fromDate.AddDays(5),
                VouNo = "INV/2026/0101",
                VouType = "Tax Invoice",
                Head = "Sales A/c",
                Debit = 65400m,
                Credit = 0m,
                Balance = acc.OpAmt1 + 65400m,
                BalanceType = "Dr",
                Narration = "Gold Ornaments 22K Hallmarked"
            },
            new()
            {
                VouDate = fromDate.AddDays(5),
                VouNo = "INV/2026/0101",
                VouType = "Pmt (HDFC Bank)",
                Head = "HDFC Bank",
                Debit = 0m,
                Credit = 25000m,
                Balance = acc.OpAmt1 + 65400m - 25000m,
                BalanceType = "Dr",
                Narration = "UPI / Card Payment"
            },
            new()
            {
                VouDate = fromDate.AddDays(14),
                VouNo = "RC0012",
                VouType = "Receipts",
                Head = "Cash in Hand",
                Debit = 0m,
                Credit = 20000m,
                Balance = acc.OpAmt1 + 65400m - 25000m - 20000m,
                BalanceType = "Dr",
                Narration = "Cash received against bill"
            },
            new()
            {
                VouDate = fromDate.AddDays(22),
                VouNo = "INV/2026/0188",
                VouType = "Retail Invoice",
                Head = "Sales A/c",
                Debit = 79800m,
                Credit = 0m,
                Balance = acc.OpAmt1 + 65400m - 25000m - 20000m + 79800m,
                BalanceType = "Dr",
                Narration = "Diamond Ring 18K"
            },
            new()
            {
                VouDate = fromDate.AddDays(25),
                VouNo = "RC0045",
                VouType = "Receipts",
                Head = "HDFC Bank",
                Debit = 0m,
                Credit = 53500m,
                Balance = acc.OpAmt1 + 65400m - 25000m - 20000m + 79800m - 53500m,
                BalanceType = "Dr",
                Narration = "Cheque clearing Chq#445892",
                ChequeNo = "445892"
            }
        };

        return Task.FromResult<IReadOnlyList<LedgerEntry>>(list);
    }

    public Task<IReadOnlyList<LedgerEntry>> GetLedgerInWeightAsync(LedgerReportFilter filter, CancellationToken cancellationToken = default)
    {
        var acc = SampleAccounts.FirstOrDefault(a => string.Equals(a.AccountName, filter.AccountName, StringComparison.OrdinalIgnoreCase)) ?? SampleAccounts[0];
        var fromDate = filter.FromDate ?? new DateTime(DateTime.Today.Year, 4, 1);

        var list = new List<LedgerEntry>
        {
            new()
            {
                VouDate = fromDate,
                VouNo = "-",
                VouType = "Opening Balance",
                Head = "Opening Weight",
                ItemType = "GOLD",
                WeightDebit = acc.OpGoldWeight,
                WeightCredit = 0m,
                WeightBalance = acc.OpGoldWeight,
                WeightBalanceType = "Dr",
                Narration = "Opening Weight as on " + fromDate.ToString("dd/MM/yyyy")
            },
            new()
            {
                VouDate = fromDate.AddDays(7),
                VouNo = "ISS0032",
                VouType = "Karighar Issue",
                Head = "GOLD 22K",
                ItemType = "GOLD",
                Purity = "91.6",
                WeightDebit = 25.500m,
                WeightCredit = 0m,
                WeightBalance = acc.OpGoldWeight + 25.500m,
                WeightBalanceType = "Dr",
                Narration = "Issued for Necklace order #102"
            },
            new()
            {
                VouDate = fromDate.AddDays(15),
                VouNo = "REC0021",
                VouType = "Karighar Receive",
                Head = "GOLD 22K",
                ItemType = "GOLD",
                Purity = "91.6",
                WeightDebit = 0m,
                WeightCredit = 24.800m,
                WeightBalance = acc.OpGoldWeight + 25.500m - 24.800m,
                WeightBalanceType = "Dr",
                Narration = "Received Finished Necklace (Loss 0.700g)"
            },
            new()
            {
                VouDate = fromDate.AddDays(20),
                VouNo = "INV/2026/0188",
                VouType = "Old Metal Received",
                Head = "OLD GOLD",
                ItemType = "OLD GOLD",
                WeightDebit = 0m,
                WeightCredit = 13.300m,
                WeightBalance = acc.OpGoldWeight + 25.500m - 24.800m - 13.300m,
                WeightBalanceType = "Dr",
                Narration = "Old Bangles melting weight"
            }
        };

        return Task.FromResult<IReadOnlyList<LedgerEntry>>(list);
    }

    public Task<IReadOnlyList<LedgerNoteBalanceItem>> GetNoteWiseBalanceAsync(LedgerReportFilter filter, CancellationToken cancellationToken = default)
    {
        var fromDate = filter.FromDate ?? new DateTime(DateTime.Today.Year, 4, 1);
        var list = new List<LedgerNoteBalanceItem>
        {
            new()
            {
                BillDate = fromDate.AddDays(5),
                VouNo = "INV/2026/0101",
                VouType = "Tax Invoice",
                BillAmount = 65400m,
                PaidAmount = 45000m,
                PendingAmount = 20400m,
                OverdueDays = 15,
                Status = "Partial",
                Narration = "Gold Ornaments 22K"
            },
            new()
            {
                BillDate = fromDate.AddDays(22),
                VouNo = "INV/2026/0188",
                VouType = "Retail Invoice",
                BillAmount = 79800m,
                PaidAmount = 53500m,
                PendingAmount = 26300m,
                OverdueDays = 2,
                Status = "Pending",
                Narration = "Diamond Ring 18K"
            },
            new()
            {
                BillDate = fromDate.AddDays(-20),
                VouNo = "INV/2025/0984",
                VouType = "Tax Invoice",
                BillAmount = 110000m,
                PaidAmount = 110000m,
                PendingAmount = 0m,
                OverdueDays = 0,
                Status = "Paid",
                Narration = "Bridal Set"
            }
        };

        return Task.FromResult<IReadOnlyList<LedgerNoteBalanceItem>>(list);
    }

    public Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var cats = SampleAccounts.Select(a => a.HeadCategory).Distinct().OrderBy(c => c).ToList();
        return Task.FromResult<IReadOnlyList<string>>(cats);
    }

    public Task<IReadOnlyList<LedgerGroupAccountItem>> GetLedgerGroupSummaryAsync(string headCategory, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var accounts = SampleAccounts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(headCategory) && !headCategory.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            accounts = accounts.Where(a => string.Equals(a.HeadCategory, headCategory.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var list = new List<LedgerGroupAccountItem>();
        foreach (var a in accounts)
        {
            var isCr = a.HeadCategory.Contains("Creditor");
            list.Add(new LedgerGroupAccountItem
            {
                AccountName = a.AccountName,
                HeadCategory = a.HeadCategory,
                SubCategory = a.SubCategory,
                PhoneNo = a.PhoneNo,
                City = a.City,
                RefName = a.RefName,
                OpeningAmount = a.OpAmt1,
                OpeningType = isCr ? "Cr" : "Dr",
                TotalDebits = 15000m,
                TotalCredits = 10000m,
                ClosingBalance = isCr ? (a.OpAmt1 - 5000m) : (a.OpAmt1 + 5000m),
                ClosingType = isCr ? "Cr" : "Dr",
                OpeningWeight = a.OpGoldWeight,
                OpeningWeightType = isCr ? "Cr" : "Dr",
                TotalWeightDebit = 5.000m,
                TotalWeightCredit = 2.500m,
                ClosingWeight = isCr ? (a.OpGoldWeight - 2.500m) : (a.OpGoldWeight + 2.500m),
                ClosingWeightType = isCr ? "Cr" : "Dr"
            });
        }

        return Task.FromResult<IReadOnlyList<LedgerGroupAccountItem>>(list);
    }

    public Task<IReadOnlyList<LedgerEntry>> GetLedgerGroupEntriesAsync(string headCategory, DateTime fromDate, DateTime toDate, bool isWeight, CancellationToken cancellationToken = default)
    {
        var list = new List<LedgerEntry>();
        var accounts = SampleAccounts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(headCategory) && !headCategory.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            accounts = accounts.Where(a => string.Equals(a.HeadCategory, headCategory.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        foreach (var a in accounts)
        {
            list.Add(new LedgerEntry
            {
                AccountName = a.AccountName,
                HeadCategory = a.HeadCategory,
                VouDate = fromDate,
                VouNo = "OP-001",
                VouType = "Opening Balance",
                Head = "Opening Balance",
                Debit = a.OpAmt1,
                Credit = 0m,
                Balance = a.OpAmt1,
                BalanceType = "Dr",
                WeightDebit = a.OpGoldWeight,
                WeightCredit = 0m,
                WeightBalance = a.OpGoldWeight,
                WeightBalanceType = "Dr"
            });
        }

        return Task.FromResult<IReadOnlyList<LedgerEntry>>(list);
    }
}
