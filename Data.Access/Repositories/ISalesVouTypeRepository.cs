using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ISalesVouTypeRepository
{
    Task<IReadOnlyList<SalesVouType>> GetAllAsync(string? headCategory = null, bool includeHidden = true, CancellationToken cancellationToken = default);

    Task<SalesVouType?> GetByVouTypeAsync(string vouType, CancellationToken cancellationToken = default);

    Task AddAsync(SalesVouType salesVouType, CancellationToken cancellationToken = default);

    Task UpdateAsync(string originalVouType, SalesVouType salesVouType, CancellationToken cancellationToken = default);

    Task DeleteAsync(string vouType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPrintFilesAsync(CancellationToken cancellationToken = default);
}
