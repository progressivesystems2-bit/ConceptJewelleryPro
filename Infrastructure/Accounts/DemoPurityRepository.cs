using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoPurityRepository : IPurityRepository
{
    private readonly List<PurityMaster> _purities = new()
    {
        new PurityMaster { Purity = "916", RatePerGram = 15000m, ItemHead = "GOLD", ItemType = "GOLD NEW ORNAMENTS", Hide = false },
        new PurityMaster { Purity = "916HALL MARKED", RatePerGram = 9945m, ItemHead = "GOLD", ItemType = "GOLD NEW ORNAMENTS", Hide = false },
        new PurityMaster { Purity = "22/20", RatePerGram = 15000m, ItemHead = "GOLD", ItemType = "GOLD NEW ORNAMENTS", Hide = false },
        new PurityMaster { Purity = "22/22", RatePerGram = 13620m, ItemHead = "GOLD", ItemType = "GOLD NEW ORNAMENTS", Hide = false },
        new PurityMaster { Purity = "18ct", RatePerGram = 9845m, ItemHead = "DIAMOND", ItemType = "GOLD 18CT", Hide = false },
        new PurityMaster { Purity = "99.5", RatePerGram = 11000m, ItemHead = "GOLD", ItemType = "GOLD STANDARD", Hide = false },
        new PurityMaster { Purity = "999", RatePerGram = 11050m, ItemHead = "GOLD", ItemType = "GOLD STANDARD", Hide = false },
        new PurityMaster { Purity = "999.9", RatePerGram = 11070m, ItemHead = "GOLD", ItemType = "GOLD STANDARD", Hide = false },
        new PurityMaster { Purity = "999ART", RatePerGram = 11070m, ItemHead = "ART", ItemType = "GOLD NEW ORNAMENTS", Hide = false },
        new PurityMaster { Purity = "KDM", RatePerGram = 9945m, ItemHead = "GOLD", ItemType = "GOLD NEW ORNAMENTS", Hide = false }
    };

    private readonly List<string> _itemHeads = new()
    {
        "18ct C.Z", "ART", "CZ", "Diamond", "Gold", "Ornaments Gold", "Silver"
    };

    private readonly List<string> _itemTypes = new()
    {
        "CZ OLD", "DIAMOND", "EXPENSES", "GOLD 18CT", "GOLD NEW ORNAMENTS", "GOLD OLD",
        "GOLD PRIMARY", "GOLD STANDARD", "SILVER NEW ITEMS", "SILVER OLD", "SILVER PRIMARY", "SILVER STANDARD"
    };

    public Task<IReadOnlyList<PurityMaster>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PurityMaster>>(_purities.ToList());
    }

    public Task<PurityMaster?> GetByPurityAsync(string purity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_purities.FirstOrDefault(p => string.Equals(p.Purity, purity, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AddAsync(PurityMaster purity, CancellationToken cancellationToken = default)
    {
        _purities.RemoveAll(p => string.Equals(p.Purity, purity.Purity, StringComparison.OrdinalIgnoreCase));
        _purities.Add(purity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string originalPurity, PurityMaster purity, CancellationToken cancellationToken = default)
    {
        _purities.RemoveAll(p => string.Equals(p.Purity, originalPurity, StringComparison.OrdinalIgnoreCase));
        _purities.Add(purity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string purity, CancellationToken cancellationToken = default)
    {
        _purities.RemoveAll(p => string.Equals(p.Purity, purity, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    private readonly List<RateStoredEntry> _rateLogs = new();

    public Task<IReadOnlyList<string>> GetItemHeadsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_itemHeads.ToList());
    }

    public Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_itemTypes.ToList());
    }

    public Task UpdateRatesBatchAsync(IEnumerable<RateUpdateItem> rateUpdates, string username, CancellationToken cancellationToken = default)
    {
        var items = rateUpdates?.Where(r => !string.IsNullOrWhiteSpace(r.Purity)).ToList();
        if (items is null || items.Count == 0) return Task.CompletedTask;

        var today = DateTime.Today;
        var now = DateTime.Now;

        foreach (var item in items)
        {
            var p = _purities.FirstOrDefault(x => string.Equals(x.Purity, item.Purity, StringComparison.OrdinalIgnoreCase));
            if (p != null)
            {
                p.RatePerGram = item.NewRate;
            }

            _rateLogs.Add(new RateStoredEntry
            {
                AutoNo = _rateLogs.Count + 1,
                Purity = item.Purity.Trim(),
                Rate = item.NewRate,
                Date = today,
                Time = now
            });
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RateStoredEntry>> GetRateHistoryAsync(string? purity = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(purity)
            ? _rateLogs.AsEnumerable()
            : _rateLogs.Where(r => string.Equals(r.Purity, purity, StringComparison.OrdinalIgnoreCase));

        var result = query
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.AutoNo)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<RateStoredEntry>>(result);
    }
}
