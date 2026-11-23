using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ITradingAccountRepository
{
    Task<TradingAccountStatement> GetTradingAccountAsync(TradingAccountFilter filter, CancellationToken cancellationToken = default);
    Task<(decimal GoldWeight, decimal SilverWeight, decimal Value)> GetEstimatedStockValuationAsync(DateTime date, CancellationToken cancellationToken = default);
}
