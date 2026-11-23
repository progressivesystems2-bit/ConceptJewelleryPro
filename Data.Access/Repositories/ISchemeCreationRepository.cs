using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ISchemeCreationRepository
{
    Task<string> GetNextScodeAsync(CancellationToken cancellationToken = default);
    Task<int> GetNextNosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemeCreationItem>> GetAllAsync(int top = 100, CancellationToken cancellationToken = default);
    Task<SchemeCreationItem?> GetByScodeAsync(string scode, CancellationToken cancellationToken = default);
    Task SaveAsync(SchemeCreationItem item, bool isUpdate = false, CancellationToken cancellationToken = default);
    Task DeleteAsync(string scode, CancellationToken cancellationToken = default);
}
