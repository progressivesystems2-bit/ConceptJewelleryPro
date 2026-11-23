using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoSchemeCreationRepository : ISchemeCreationRepository
{
    private readonly List<SchemeCreationItem> _schemes = new();

    public DemoSchemeCreationRepository()
    {
        _schemes.Add(new SchemeCreationItem
        {
            Scode = "SC00023",
            Nos = 23,
            CustomerName = "INDIAN ACADEMY OF PEDIATRICS",
            InstallmentAmt = 50.00m,
            Month = 11m,
            TotalAmt = 550.00m,
            SchemeDiscount = 50.00m,
            TotalValue = 600.00m,
            SDate = DateTime.Today,
            EDate = DateTime.Today.AddMonths(12).AddDays(-5),
            Status = "Saved",
            Closed = false,
            Staff = "JAYANT"
        });
    }

    public Task<string> GetNextScodeAsync(CancellationToken cancellationToken = default)
    {
        var next = _schemes.Count == 0 ? 1 : _schemes.Max(x => x.Nos) + 1;
        return Task.FromResult($"SC{next:D5}");
    }

    public Task<int> GetNextNosAsync(CancellationToken cancellationToken = default)
    {
        var next = _schemes.Count == 0 ? 1 : _schemes.Max(x => x.Nos) + 1;
        return Task.FromResult(next);
    }

    public Task<IReadOnlyList<SchemeCreationItem>> GetAllAsync(int top = 100, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SchemeCreationItem> list = _schemes.Where(x => x.Status != "Deleted").OrderByDescending(x => x.Nos).Take(top).ToList();
        return Task.FromResult(list);
    }

    public Task<SchemeCreationItem?> GetByScodeAsync(string scode, CancellationToken cancellationToken = default)
    {
        var item = _schemes.FirstOrDefault(x => x.Scode == scode);
        return Task.FromResult(item);
    }

    public Task SaveAsync(SchemeCreationItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        if (isUpdate)
        {
            var existing = _schemes.FirstOrDefault(x => x.Scode == item.Scode);
            if (existing is not null)
            {
                existing.CustomerName = item.CustomerName;
                existing.InstallmentAmt = item.InstallmentAmt;
                existing.Month = item.Month;
                existing.TotalAmt = item.TotalAmt;
                existing.SchemeDiscount = item.SchemeDiscount;
                existing.TotalValue = item.TotalValue;
                existing.SDate = item.SDate;
                existing.EDate = item.EDate;
                existing.Closed = item.Closed;
                existing.Staff = item.Staff;
                existing.Status = item.Status;
            }
        }
        else
        {
            if (item.Nos <= 0)
            {
                item.Nos = _schemes.Count == 0 ? 1 : _schemes.Max(x => x.Nos) + 1;
            }
            if (string.IsNullOrWhiteSpace(item.Scode))
            {
                item.Scode = $"SC{item.Nos:D5}";
            }
            _schemes.Add(item);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string scode, CancellationToken cancellationToken = default)
    {
        var existing = _schemes.FirstOrDefault(x => x.Scode == scode);
        if (existing is not null)
        {
            existing.Status = "Deleted";
        }
        return Task.CompletedTask;
    }
}
