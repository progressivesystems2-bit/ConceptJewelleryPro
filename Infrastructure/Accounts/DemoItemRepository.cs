using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoItemRepository : IItemRepository
{
    private readonly List<Item> _items = new();

    public Task AddAsync(Item item, CancellationToken cancellationToken = default)
    {
        _items.Add(item);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Item item, CancellationToken cancellationToken = default)
    {
        var index = _items.FindIndex(i => string.Equals(i.ItemCode, item.ItemCode, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _items[index] = item;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(i => string.Equals(i.ItemCode, itemCode, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<Item?> FindByCodeAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        var item = _items.FirstOrDefault(i => string.Equals(i.ItemCode, itemCode, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Item> snapshot = _items.OrderBy(i => i.ItemName).ToList();
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> list = new List<string> { "GOLD", "SILVER", "DIAMOND", "PLATINUM" };
        return Task.FromResult(list);
    }
}