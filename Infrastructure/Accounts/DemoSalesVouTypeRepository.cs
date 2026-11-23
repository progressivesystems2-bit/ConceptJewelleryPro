using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoSalesVouTypeRepository : ISalesVouTypeRepository
{
    private readonly List<SalesVouType> _items = new()
    {
        new SalesVouType
        {
            VouType = "TAX INVOICE GOLD",
            ShortName = "ATIG",
            SalesAccount = "SALE GOLD",
            VatAccount = "GST",
            CardCommission = 1.68m,
            CardBank = "AXIS BANK CREDIT CARD",
            CommAc = "BANK CHARGES",
            VatPercentage = 3m,
            Note = "Goods once sold will not be taken back without original bill",
            Status = "Active",
            HeadCategory = "Sales",
            Hide = false,
            ItemHead = "GOLD",
            PrintFile = "BillPrint",
            CounterNo = "1, 2, ALL"
        },
        new SalesVouType
        {
            VouType = "RETAIL INVOICE GOLD",
            ShortName = "SVN",
            SalesAccount = "SALE GOLD",
            VatAccount = "GST",
            CardCommission = 1.68m,
            CardBank = "AXIS BANK CREDIT CARD",
            CommAc = "BANK CHARGES",
            VatPercentage = 3m,
            Note = "Thank you for visiting us",
            Status = "Active",
            HeadCategory = "Sales",
            Hide = false,
            ItemHead = "GOLD",
            PrintFile = "BillPrint",
            CounterNo = "ALL"
        },
        new SalesVouType
        {
            VouType = "TAX INVOICE SILVER",
            ShortName = "T1",
            SalesAccount = "SALE GOLD",
            VatAccount = "GST",
            CardCommission = 0m,
            VatPercentage = 0m,
            Status = "Active",
            HeadCategory = "Sales",
            Hide = false,
            ItemHead = "SILVER",
            PrintFile = "BillPrintSilver",
            CounterNo = "ALL"
        },
        new SalesVouType
        {
            VouType = "RETAIL INVOICE DIAMOND",
            ShortName = "TID",
            SalesAccount = "SALE GOLD",
            VatAccount = "GST",
            CardCommission = 1.6836m,
            CardBank = "AXIS BANK CREDIT CARD",
            VatPercentage = 3m,
            Status = "Active",
            HeadCategory = "Sales",
            Hide = false,
            ItemHead = "DIAMOND",
            PrintFile = "BillPrintDiamond",
            CounterNo = "ALL"
        },
        new SalesVouType
        {
            VouType = "APPROVAL INVOICE GOLD",
            ShortName = "AIG",
            SalesAccount = "SALES APPROVAL",
            VatAccount = "GST APPROVAL",
            CardCommission = 0m,
            VatPercentage = 3m,
            Status = "Active",
            HeadCategory = "Sales",
            Hide = false,
            ItemHead = "GOLD",
            PrintFile = "BillPrint",
            CounterNo = "ALL"
        },
        new SalesVouType
        {
            VouType = "OLD GOLD PURCHASE",
            ShortName = "OGP",
            SalesAccount = "OLD GOLD PURCHASES",
            VatAccount = "GST",
            CardCommission = 0m,
            VatPercentage = 3m,
            Status = "Active",
            HeadCategory = "Purchases",
            Hide = false,
            ItemHead = "GOLD",
            PrintFile = "PurchasePrint2",
            ItemPurchased = "GOLD OLD",
            ItemConvertedTo = "GOLD PRIMARY"
        },
        new SalesVouType
        {
            VouType = "NEW GOLD ORNAMENTS PURCHASE",
            ShortName = "NOP",
            SalesAccount = "NEW GOLD ORNAMENTS PURCHASE",
            VatAccount = "GST",
            CardCommission = 0m,
            VatPercentage = 3m,
            Status = "Active",
            HeadCategory = "Purchases",
            Hide = false,
            ItemHead = "GOLD",
            PrintFile = "PurchasePrint2",
            ItemPurchased = "GOLD NEW ORNAMENTS",
            ItemConvertedTo = "GOLD NEW ORNAMENTS"
        },
        new SalesVouType
        {
            VouType = "SALES RETURN GOLD",
            ShortName = "SRG",
            SalesAccount = "SALES RETURN GOLD",
            VatAccount = "GST",
            CardCommission = 0m,
            VatPercentage = 3m,
            Status = "Active",
            HeadCategory = "Sales Return",
            Hide = false,
            ItemHead = "GOLD",
            PrintFile = "SalesReturnPrint"
        },
        new SalesVouType
        {
            VouType = "PURCHASE RETURN GOLD",
            ShortName = "PRG",
            SalesAccount = "PURCHASE RETURN GOLD",
            VatAccount = "GST",
            CardCommission = 0m,
            VatPercentage = 3m,
            Status = "Active",
            HeadCategory = "Purchase Return",
            Hide = false,
            ItemHead = "GOLD",
            PrintFile = "PurchaseReturnPrint"
        }
    };

    public Task<IReadOnlyList<SalesVouType>> GetAllAsync(string? headCategory = null, bool includeHidden = true, CancellationToken cancellationToken = default)
    {
        var query = _items.AsEnumerable();
        if (!includeHidden)
        {
            query = query.Where(x => !x.Hide);
        }
        if (!string.IsNullOrWhiteSpace(headCategory) && !string.Equals(headCategory, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => string.Equals(x.HeadCategory, headCategory.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<SalesVouType>>(query.OrderBy(x => x.HeadCategory).ThenBy(x => x.VouType).ToList());
    }

    public Task<SalesVouType?> GetByVouTypeAsync(string vouType, CancellationToken cancellationToken = default)
    {
        var item = _items.FirstOrDefault(x => string.Equals(x.VouType, vouType.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }

    public Task AddAsync(SalesVouType salesVouType, CancellationToken cancellationToken = default)
    {
        _items.Add(salesVouType);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string originalVouType, SalesVouType salesVouType, CancellationToken cancellationToken = default)
    {
        var index = _items.FindIndex(x => string.Equals(x.VouType, originalVouType.Trim(), StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _items[index] = salesVouType;
        }
        else
        {
            _items.Add(salesVouType);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string vouType, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(x => string.Equals(x.VouType, vouType.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var list = _items
            .Select(x => x.HeadCategory)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Union(new[] { "Sales", "Purchases", "Sales Return", "Purchase Return", "Estimate", "Approval", "Issue", "Receive", "Others" })
            .OrderBy(c => c)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(list);
    }

    public Task<IReadOnlyList<string>> GetPrintFilesAsync(CancellationToken cancellationToken = default)
    {
        var list = _items
            .Select(x => x.PrintFile)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Union(new[] { "BillPrint", "BillPrintSilver", "BillPrintDiamond", "Tattva", "PurchasePrint2", "PurchaseReturnPrint", "SalesReturnPrint" })
            .OrderBy(p => p)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(list);
    }
}
