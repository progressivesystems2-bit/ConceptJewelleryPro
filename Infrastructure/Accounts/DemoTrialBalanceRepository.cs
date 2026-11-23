using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoTrialBalanceRepository : ITrialBalanceRepository
{
    private static readonly List<TrialBalanceItem> SampleData =
    [
        new() { AccountName = "CAPITAL ACCOUNT", HeadCategory = "Capital", OpeningCredit = 500000m, ClosingCredit = 500000m },
        new() { AccountName = "HDFC BANK", HeadCategory = "Bank A/c", OpeningDebit = 150000m, PeriodDebit = 75000m, PeriodCredit = 25000m, ClosingDebit = 200000m },
        new() { AccountName = "CASH IN HAND", HeadCategory = "Cash in Hand", OpeningDebit = 50000m, PeriodDebit = 35000m, PeriodCredit = 20000m, ClosingDebit = 65000m },
        new() { AccountName = "RAJESH JEWELLERS", HeadCategory = "Debtors", OpeningDebit = 25000m, PeriodDebit = 34700m, PeriodCredit = 34700m, ClosingDebit = 25000m },
        new() { AccountName = "MANOJ BULLION TRADERS", HeadCategory = "Creditors", OpeningCredit = 150000m, PeriodDebit = 50000m, PeriodCredit = 20000m, ClosingCredit = 120000m },
        new() { AccountName = "SALES ACCOUNT", HeadCategory = "Sales", PeriodCredit = 120000m, ClosingCredit = 120000m },
        new() { AccountName = "PURCHASE ACCOUNT", HeadCategory = "Purchases", PeriodDebit = 85000m, ClosingDebit = 85000m },
        new() { AccountName = "SALARY EXPENSES", HeadCategory = "Indirect Expenses", PeriodDebit = 15000m, ClosingDebit = 15000m },
        new() { AccountName = "SHOP RENT", HeadCategory = "Indirect Expenses", PeriodDebit = 10000m, ClosingDebit = 10000m }
    ];

    public Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var cats = SampleData.Select(x => x.HeadCategory).Distinct().OrderBy(x => x).ToList();
        return Task.FromResult<IReadOnlyList<string>>(cats);
    }

    public Task<IReadOnlyList<TrialBalanceItem>> GetTrialBalanceAsync(TrialBalanceFilter filter, CancellationToken cancellationToken = default)
    {
        var query = SampleData.AsEnumerable();

        if (filter.SelectedHeadCategory != "ALL")
        {
            query = query.Where(x => string.Equals(x.HeadCategory, filter.SelectedHeadCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            query = query.Where(x => x.AccountName.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     x.HeadCategory.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.HideZeroBalances)
        {
            query = query.Where(x => x.HasActivityOrBalance);
        }

        return Task.FromResult<IReadOnlyList<TrialBalanceItem>>(query.ToList());
    }

    public Task<IReadOnlyList<VoucherAuditItem>> AuditVouchersAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<VoucherAuditItem> list = Array.Empty<VoucherAuditItem>();
        return Task.FromResult(list);
    }
}
