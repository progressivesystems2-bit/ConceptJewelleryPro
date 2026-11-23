using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoSchemeDueRepository : ISchemeDueRepository
{
    private readonly List<SchemeDueListItem> _items = new();
    private readonly List<VoucherEntry> _vouchers = new();

    public DemoSchemeDueRepository()
    {
        var baseDate = DateTime.Today.AddMonths(-5);
        _items.Add(new SchemeDueListItem
        {
            Scode = "SCH-001",
            Nos = 1,
            CustomerName = "Rajesh Sharma",
            Phone = "9876543210",
            Address = "College Square",
            City = "CUTTACK",
            Staff = "Amit",
            SDate = baseDate,
            EDate = baseDate.AddMonths(11),
            InstallmentAmt = 3000m,
            TotalMonths = 11,
            TotalValue = 36000m,
            PaidCount = 3,
            TotalPaidAmt = 9000m,
            ExpectedCount = 5,
            OverdueCount = 2,
            OverdueAmount = 6000m,
            NextDueDate = baseDate.AddMonths(3),
            DaysOverdue = 45,
            Status = "Active"
        });

        _items.Add(new SchemeDueListItem
        {
            Scode = "SCH-002",
            Nos = 2,
            CustomerName = "Priya Mishra",
            Phone = "9861234567",
            Address = "Saheed Nagar",
            City = "BHUBANESWAR",
            Staff = "Bikash",
            SDate = DateTime.Today.AddMonths(-2),
            EDate = DateTime.Today.AddMonths(9),
            InstallmentAmt = 5000m,
            TotalMonths = 11,
            TotalValue = 60000m,
            PaidCount = 2,
            TotalPaidAmt = 10000m,
            ExpectedCount = 2,
            OverdueCount = 0,
            OverdueAmount = 0m,
            NextDueDate = DateTime.Today.AddDays(4),
            DaysRemaining = 4,
            Status = "Active"
        });
    }

    public Task<IReadOnlyList<SchemeDueListItem>> GetSchemeDueListAsync(DateTime asOnDate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SchemeDueListItem>>(_items);
    }

    public Task<IReadOnlyList<VoucherEntry>> GetSchemePaymentHistoryAsync(string scode, CancellationToken cancellationToken = default)
    {
        var list = _vouchers.Where(v => v.Scode.Equals(scode, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<VoucherEntry>>(list);
    }
}
