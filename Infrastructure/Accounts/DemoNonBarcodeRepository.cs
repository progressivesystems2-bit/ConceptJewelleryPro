using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoNonBarcodeRepository : INonBarcodeRepository
{
    private readonly List<NonBarcode> _records = [];

    public Task<IReadOnlyList<NonBarcode>> GetRecentAsync(int count = 25, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NonBarcode> list = _records
            .Where(r => !string.Equals(r.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.VouDate)
            .ThenByDescending(r => r.Nos)
            .Take(count)
            .ToList();

        return Task.FromResult(list);
    }

    public Task<NonBarcode?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        var record = _records.FirstOrDefault(r =>
            string.Equals(r.VouNo, vouNo, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(r.Status, "Deleted", StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(record);
    }

    public Task<int> GetNextNosAsync(CancellationToken cancellationToken = default)
    {
        var max = _records.Count == 0 ? 0 : _records.Max(r => r.Nos);
        return Task.FromResult(max + 1);
    }

    public Task SaveAsync(NonBarcode stock, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        if (!updateExisting && stock.Nos <= 0)
        {
            stock.Nos = _records.Count == 0 ? 1 : _records.Max(r => r.Nos) + 1;
        }

        var index = _records.FindIndex(r => string.Equals(r.VouNo, stock.VouNo, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _records[index] = stock;
        }
        else
        {
            _records.Add(stock);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        var record = _records.FirstOrDefault(r => string.Equals(r.VouNo, vouNo, StringComparison.OrdinalIgnoreCase));
        if (record is not null)
        {
            record.Status = "Deleted";
        }

        return Task.CompletedTask;
    }
}
