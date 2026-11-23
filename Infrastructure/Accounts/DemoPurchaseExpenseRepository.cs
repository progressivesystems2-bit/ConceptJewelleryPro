using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoPurchaseExpenseRepository : IPurchaseExpenseRepository
{
    private readonly List<PurchaseExpense> _expenses = [];

    public Task<IReadOnlyList<PurchaseExpense>> GetRecentAsync(int count = 25, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PurchaseExpense> list = _expenses
            .Where(e => !string.Equals(e.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.BillDate)
            .ThenByDescending(e => e.Nos)
            .Take(count)
            .ToList();

        return Task.FromResult(list);
    }

    public Task<PurchaseExpense?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        var expense = _expenses.FirstOrDefault(e =>
            string.Equals(e.VouNo, vouNo, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(e.Status, "Deleted", StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(expense);
    }

    public Task<int> GetNextNosAsync(CancellationToken cancellationToken = default)
    {
        var max = _expenses.Count == 0 ? 0 : _expenses.Max(e => e.Nos);
        return Task.FromResult(max + 1);
    }

    public Task SaveAsync(PurchaseExpense expense, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        if (!updateExisting && expense.Nos <= 0)
        {
            expense.Nos = _expenses.Count == 0 ? 1 : _expenses.Max(e => e.Nos) + 1;
        }

        var index = _expenses.FindIndex(e => string.Equals(e.VouNo, expense.VouNo, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _expenses[index] = expense;
        }
        else
        {
            _expenses.Add(expense);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(int nos, CancellationToken cancellationToken = default)
    {
        var expense = _expenses.FirstOrDefault(e => e.Nos == nos);
        if (expense is not null)
        {
            expense.Status = "Deleted";
        }

        return Task.CompletedTask;
    }
}
