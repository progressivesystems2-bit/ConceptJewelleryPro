using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IItemRepository
{
    Task AddAsync(Item item, CancellationToken cancellationToken = default);

    Task UpdateAsync(Item item, CancellationToken cancellationToken = default);

    Task DeleteAsync(string itemCode, CancellationToken cancellationToken = default);

    Task<Item?> FindByCodeAsync(string itemCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default);
}