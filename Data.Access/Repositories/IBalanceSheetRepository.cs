using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IBalanceSheetRepository
{
    Task<BalanceSheetStatement> GetBalanceSheetAsync(BalanceSheetFilter filter, CancellationToken cancellationToken = default);
    Task<decimal> GetEstimatedClosingStockValueAsync(DateTime asOnDate, CancellationToken cancellationToken = default);
}
