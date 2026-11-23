using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface INonBarcodeRepository
{
    Task<IReadOnlyList<NonBarcode>> GetRecentAsync(int count = 25, CancellationToken cancellationToken = default);

    Task<NonBarcode?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<int> GetNextNosAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(NonBarcode stock, bool updateExisting = false, CancellationToken cancellationToken = default);

    Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default);
}
