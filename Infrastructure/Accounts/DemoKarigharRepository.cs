using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoKarigharRepository : IKarigharRepository
{
    private readonly List<MetalIssuedItem> _issued = new();
    private readonly List<MetalReceivedItem> _received = new();

    public DemoKarigharRepository()
    {
        _issued.Add(new MetalIssuedItem
        {
            IssueNo = "IS00001",
            Nos = 1,
            IssueDate = DateTime.Today.AddDays(-5),
            ArtisianName = "DUTTA ENTERPRISES",
            ItemType = "GOLD PRIMARY",
            WeightIssued = 1319.46m,
            Purity = "916",
            Note = "For making new Gold Ornaments",
            Status = "Saved"
        });

        _received.Add(new MetalReceivedItem
        {
            RecNo = "RE00001",
            Nos = 1,
            RecDate = DateTime.Today.AddDays(-2),
            PartyName = "DUTTA ENTERPRISES",
            ItemType = "GOLD NEW ORNAMENTS",
            WeightRecd = 1319.46m,
            MakingCharges = 296904.76m,
            Note = "For MC due Rs.2,96,904.76 only",
            Status = "Saved"
        });
    }

    public Task<string> GetNextIssueNoAsync(CancellationToken cancellationToken = default)
    {
        var next = _issued.Count == 0 ? 1 : _issued.Max(x => x.Nos) + 1;
        return Task.FromResult($"IS{next:D5}");
    }

    public Task<int> GetNextIssueNosAsync(CancellationToken cancellationToken = default)
    {
        var next = _issued.Count == 0 ? 1 : _issued.Max(x => x.Nos) + 1;
        return Task.FromResult(next);
    }

    public Task<IReadOnlyList<MetalIssuedItem>> GetAllMetalIssuedAsync(int top = 100, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MetalIssuedItem> list = _issued.Where(x => x.Status != "Deleted").OrderByDescending(x => x.Nos).Take(top).ToList();
        return Task.FromResult(list);
    }

    public Task<MetalIssuedItem?> GetMetalIssuedByIdAsync(string issueNo, CancellationToken cancellationToken = default)
    {
        var item = _issued.FirstOrDefault(x => x.IssueNo == issueNo);
        return Task.FromResult(item);
    }

    public Task SaveMetalIssuedAsync(MetalIssuedItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        if (isUpdate)
        {
            var existing = _issued.FirstOrDefault(x => x.IssueNo == item.IssueNo);
            if (existing is not null)
            {
                existing.IssueDate = item.IssueDate;
                existing.ArtisianName = item.ArtisianName;
                existing.ItemType = item.ItemType;
                existing.WeightIssued = item.WeightIssued;
                existing.Purity = item.Purity;
                existing.Note = item.Note;
            }
        }
        else
        {
            if (item.Nos <= 0)
            {
                item.Nos = _issued.Count == 0 ? 1 : _issued.Max(x => x.Nos) + 1;
            }
            if (string.IsNullOrWhiteSpace(item.IssueNo))
            {
                item.IssueNo = $"IS{item.Nos:D5}";
            }
            _issued.Add(item);
        }

        return Task.CompletedTask;
    }

    public Task DeleteMetalIssuedAsync(string issueNo, CancellationToken cancellationToken = default)
    {
        var existing = _issued.FirstOrDefault(x => x.IssueNo == issueNo);
        if (existing is not null)
        {
            existing.Status = "Deleted";
        }
        return Task.CompletedTask;
    }

    public Task<string> GetNextRecNoAsync(CancellationToken cancellationToken = default)
    {
        var next = _received.Count == 0 ? 1 : _received.Max(x => x.Nos) + 1;
        return Task.FromResult($"RE{next:D5}");
    }

    public Task<int> GetNextRecNosAsync(CancellationToken cancellationToken = default)
    {
        var next = _received.Count == 0 ? 1 : _received.Max(x => x.Nos) + 1;
        return Task.FromResult(next);
    }

    public Task<IReadOnlyList<MetalReceivedItem>> GetAllMetalReceivedAsync(int top = 100, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MetalReceivedItem> list = _received.Where(x => x.Status != "Deleted").OrderByDescending(x => x.Nos).Take(top).ToList();
        return Task.FromResult(list);
    }

    public Task<MetalReceivedItem?> GetMetalReceivedByIdAsync(string recNo, CancellationToken cancellationToken = default)
    {
        var item = _received.FirstOrDefault(x => x.RecNo == recNo);
        return Task.FromResult(item);
    }

    public Task SaveMetalReceivedAsync(MetalReceivedItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        if (isUpdate)
        {
            var existing = _received.FirstOrDefault(x => x.RecNo == item.RecNo);
            if (existing is not null)
            {
                existing.RecDate = item.RecDate;
                existing.PartyName = item.PartyName;
                existing.ItemType = item.ItemType;
                existing.WeightRecd = item.WeightRecd;
                existing.MakingCharges = item.MakingCharges;
                existing.Note = item.Note;
            }
        }
        else
        {
            if (item.Nos <= 0)
            {
                item.Nos = _received.Count == 0 ? 1 : _received.Max(x => x.Nos) + 1;
            }
            if (string.IsNullOrWhiteSpace(item.RecNo))
            {
                item.RecNo = $"RE{item.Nos:D5}";
            }
            _received.Add(item);
        }

        return Task.CompletedTask;
    }

    public Task DeleteMetalReceivedAsync(string recNo, CancellationToken cancellationToken = default)
    {
        var existing = _received.FirstOrDefault(x => x.RecNo == recNo);
        if (existing is not null)
        {
            existing.Status = "Deleted";
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> list = new[]
        {
            "GOLD PRIMARY",
            "GOLD NEW ORNAMENTS",
            "GOLD OLD",
            "GOLD 18CT",
            "GOLD STANDARD",
            "SILVER PRIMARY",
            "SILVER NEW ITEMS",
            "SILVER OLD",
            "SILVER STANDARD",
            "DIAMOND",
            "CZ OLD"
        };
        return Task.FromResult(list);
    }
}
