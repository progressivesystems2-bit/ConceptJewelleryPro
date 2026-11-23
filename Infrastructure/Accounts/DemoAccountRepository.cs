using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts = new();

    public Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        _accounts.Add(account);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        var index = _accounts.FindIndex(a =>
            string.Equals(a.PartyCode, account.PartyCode, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            _accounts[index] = account;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string partyCode, CancellationToken cancellationToken = default)
    {
        _accounts.RemoveAll(a =>
            string.Equals(a.PartyCode, partyCode, StringComparison.OrdinalIgnoreCase));

        return Task.CompletedTask;
    }

    public Task<Account?> FindByPartyCodeAsync(string partyCode, CancellationToken cancellationToken = default)
    {
        var account = _accounts.FirstOrDefault(a =>
            string.Equals(a.PartyCode, partyCode, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(account);
    }

    public Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Account> snapshot = _accounts
            .OrderBy(a => a.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<string>> GetAdvanceOptionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> list = new List<string> { "Order Advance", "Old Gold / Scheme", "General Advance" };
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> list = new List<string>
        {
            "Advances & Deposits", "Approval Account", "Artisians", "Bank A/c", "Bank OD A/C",
            "Capital", "Cash in Hand", "Closing Stock", "Credit Card", "Creditors",
            "Current Assets", "Current Liabilities", "Debtors", "Deposits(Assets)", "Depreciation",
            "Direct Expenses", "Direct Incomes", "Duties & Taxes", "Equity Shares", "Fixed Assets",
            "Hire Account", "Indirect Expenses", "Indirect Incomes", "Intangible Asset", "Investments",
            "Loans Given", "Loans Taken", "Opening Stock", "OutStanding Liability", "Provisions",
            "Purchase Return", "Purchases", "Reserve & Surplus", "Sales", "Sales Return",
            "Suspense A/c", "Unsecured Loans"
        };
        return Task.FromResult(list);
    }
}
