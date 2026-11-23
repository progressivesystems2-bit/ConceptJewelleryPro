using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IPurityRepository
{
    Task<IReadOnlyList<PurityMaster>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PurityMaster?> GetByPurityAsync(string purity, CancellationToken cancellationToken = default);

    Task AddAsync(PurityMaster purity, CancellationToken cancellationToken = default);

    Task UpdateAsync(string originalPurity, PurityMaster purity, CancellationToken cancellationToken = default);

    Task DeleteAsync(string purity, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetItemHeadsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default);

    Task UpdateRatesBatchAsync(IEnumerable<RateUpdateItem> rateUpdates, string username, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RateStoredEntry>> GetRateHistoryAsync(string? purity = null, int limit = 50, CancellationToken cancellationToken = default);
}
