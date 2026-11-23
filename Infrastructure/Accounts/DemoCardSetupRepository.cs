using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoCardSetupRepository : ICardSetupRepository
{
    private readonly List<CardSetupItem> _items = new()
    {
        new CardSetupItem { CardBank = "HDFC", CardComm = 1.5m, Sbi = 0m, Order = 1 },
        new CardSetupItem { CardBank = "SBI", CardComm = 1.2m, Sbi = 0m, Order = 2 },
        new CardSetupItem { CardBank = "ICICI", CardComm = 1.4m, Sbi = 0m, Order = 3 },
        new CardSetupItem { CardBank = "AXIS", CardComm = 1.5m, Sbi = 0m, Order = 4 }
    };

    public Task<IReadOnlyList<CardSetupItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CardSetupItem> list = _items.OrderBy(x => x.Order).ThenBy(x => x.CardBank).ToList();
        return Task.FromResult(list);
    }

    public Task SaveAllAsync(IEnumerable<CardSetupItem> items, CancellationToken cancellationToken = default)
    {
        _items.Clear();
        _items.AddRange(items.Where(x => !string.IsNullOrWhiteSpace(x.CardBank)));
        return Task.CompletedTask;
    }
}
