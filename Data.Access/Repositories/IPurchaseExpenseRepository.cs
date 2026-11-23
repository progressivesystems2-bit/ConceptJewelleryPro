using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IPurchaseExpenseRepository
{
    Task<IReadOnlyList<PurchaseExpense>> GetRecentAsync(int count = 25, CancellationToken cancellationToken = default);

    Task<PurchaseExpense?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<int> GetNextNosAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(PurchaseExpense expense, bool updateExisting = false, CancellationToken cancellationToken = default);

    Task DeleteAsync(int nos, CancellationToken cancellationToken = default);
}
