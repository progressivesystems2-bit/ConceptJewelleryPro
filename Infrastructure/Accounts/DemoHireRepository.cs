using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoHireRepository : IHireRepository
{
    private readonly List<HireIssuedItem> _issued = new();
    private readonly List<HireReceivedItem> _received = new();

    private static readonly List<Account> SampleArtisans =
    [
        new() { PartyCode = "ART001", AccountName = "GANESH DAS", HeadCategory = "Artisians", City = "CUTTACK", PhoneNo = "9437012345" },
        new() { PartyCode = "ART002", AccountName = "RAMESH SAHOO", HeadCategory = "Artisians", City = "CUTTACK", PhoneNo = "9861098765" },
        new() { PartyCode = "ART003", AccountName = "MOHAN PATRA", HeadCategory = "Artisians", City = "BHUBANESWAR", PhoneNo = "9937055443" }
    ];

    private static readonly List<string> SampleItemTypes = ["GOLD 22K", "GOLD 18K", "SILVER", "DIAMOND", "PLATINUM"];

    public DemoHireRepository()
    {
        _issued.Add(new HireIssuedItem
        {
            IssueNo = "HI00001",
            Nos = 1,
            IssueDate = DateTime.Today,
            ArtisianName = "GANESH DAS",
            ItemType = "GOLD 22K",
            WeightIssued = 25.500m,
            Note = "Bangles casting work",
            Status = "Saved"
        });

        _received.Add(new HireReceivedItem
        {
            RecNo = "HR00001",
            Nos = 1,
            RecDate = DateTime.Today,
            PartyName = "GANESH DAS",
            ItemType = "GOLD 22K",
            WeightRecd = 25.400m,
            Note = "Finished bangles received",
            Status = "Saved"
        });
    }

    public Task<int> GetNextIssueNosAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_issued.Count + 1);
    }

    public Task<string> GetNextIssueNoAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"HI{_issued.Count + 1:D5}");
    }

    public Task<IReadOnlyList<HireIssuedItem>> GetRecentIssuedAsync(int top = 50, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<HireIssuedItem>>(_issued.OrderByDescending(i => i.Nos).Take(top).ToList());
    }

    public Task<HireIssuedItem?> GetIssuedByNoAsync(string issueNo, CancellationToken cancellationToken = default)
    {
        var item = _issued.FirstOrDefault(i => string.Equals(i.IssueNo, issueNo, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }

    public Task SaveIssuedAsync(HireIssuedItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        var existing = _issued.FirstOrDefault(i => string.Equals(i.IssueNo, item.IssueNo, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _issued.Remove(existing);
        }
        _issued.Add(item);
        return Task.CompletedTask;
    }

    public Task DeleteIssuedAsync(string issueNo, CancellationToken cancellationToken = default)
    {
        var item = _issued.FirstOrDefault(i => string.Equals(i.IssueNo, issueNo, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            _issued.Remove(item);
        }
        return Task.CompletedTask;
    }

    public Task<int> GetNextRecNosAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_received.Count + 1);
    }

    public Task<string> GetNextRecNoAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"HR{_received.Count + 1:D5}");
    }

    public Task<IReadOnlyList<HireReceivedItem>> GetRecentReceivedAsync(int top = 50, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<HireReceivedItem>>(_received.OrderByDescending(i => i.Nos).Take(top).ToList());
    }

    public Task<HireReceivedItem?> GetReceivedByNoAsync(string recNo, CancellationToken cancellationToken = default)
    {
        var item = _received.FirstOrDefault(i => string.Equals(i.RecNo, recNo, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }

    public Task SaveReceivedAsync(HireReceivedItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        var existing = _received.FirstOrDefault(i => string.Equals(i.RecNo, item.RecNo, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _received.Remove(existing);
        }
        _received.Add(item);
        return Task.CompletedTask;
    }

    public Task DeleteReceivedAsync(string recNo, CancellationToken cancellationToken = default)
    {
        var item = _received.FirstOrDefault(i => string.Equals(i.RecNo, recNo, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            _received.Remove(item);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Account>> GetArtisansAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Account>>(SampleArtisans);
    }

    public Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(SampleItemTypes);
    }
}
