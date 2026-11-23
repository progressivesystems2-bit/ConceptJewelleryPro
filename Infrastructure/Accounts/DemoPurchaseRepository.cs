using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoPurchaseRepository : IPurchaseRepository
{
    private readonly List<Purchase> _purchases = new();
    private readonly Dictionary<string, List<PurchaseSubform>> _subforms = new();
    private readonly Dictionary<string, List<PaymentSubform>> _payments = new();

    public DemoPurchaseRepository()
    {
        // Seed demo purchase
        var demoP = new Purchase
        {
            VouNo = "PUR-1001",
            VouType = "Purchase Gold",
            BillDate = DateTime.Today.AddDays(-2),
            DueDate = DateTime.Today.AddDays(15),
            SuppBillNo = "INV/2026/892",
            SuppDate = DateTime.Today.AddDays(-3),
            AccountName = "Shree Ganesh Bullion",
            CustomerName = "Shree Ganesh Bullion",
            CustomerCode = "SUP001",
            Phone = "9876543210",
            Address = "Gold Market Lane",
            City = "CUTTACK",
            GrossAmt = 250000.00m,
            TaxableAmount = 250000.00m,
            TaxAmt = 7500.00m,
            NetAmt = 257500.00m,
            GrWt = 35.500m,
            NtWt = 35.500m,
            TotalQty = 3,
            Status = "Saved"
        };

        _purchases.Add(demoP);
        _subforms[demoP.VouNo] = new List<PurchaseSubform>
        {
            new()
            {
                AutoNo = 1,
                VouNo = demoP.VouNo,
                SlNo = 1,
                ItemName = "Gold Bar 999",
                Purity = "99.90",
                Qty = 1,
                GrWt = 20.000m,
                NtWt = 20.000m,
                Rate = 7200.00m,
                Amount = 144000.00m,
                MetalValue = 144000.00m,
                GrossAmt = 144000.00m,
                TaxableAmount = 144000.00m,
                GstPer = 3.00m,
                GstAmt = 4320.00m,
                TotalAmt = 148320.00m
            },
            new()
            {
                AutoNo = 2,
                VouNo = demoP.VouNo,
                SlNo = 2,
                ItemName = "Making & Designing Charges",
                Qty = 1,
                Rate = 6000.00m,
                Amount = 6000.00m,
                GrossAmt = 6000.00m,
                TaxableAmount = 6000.00m,
                GstPer = 18.00m,
                GstAmt = 1080.00m,
                TotalAmt = 7080.00m
            }
        };

        _payments[demoP.VouNo] = new List<PaymentSubform>
        {
            new()
            {
                VouNo = demoP.VouNo,
                AccountName = "HDFC Bank",
                Amount = 257500.00m,
                HeadCategory = "Bank",
                Narration = "NEFT Payment"
            }
        };
    }

    public Task<IReadOnlyList<Purchase>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Purchase>>(_purchases.ToList());
    }

    public Task<Purchase?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        var p = _purchases.FirstOrDefault(b => b.VouNo.Equals(vouNo, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(p);
    }

    public Task<IReadOnlyList<PurchaseSubform>> GetSubformItemsAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        if (_subforms.TryGetValue(vouNo, out var items))
        {
            return Task.FromResult<IReadOnlyList<PurchaseSubform>>(items.ToList());
        }
        return Task.FromResult<IReadOnlyList<PurchaseSubform>>(new List<PurchaseSubform>());
    }

    public Task<IReadOnlyList<PaymentSubform>> GetPaymentItemsAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        if (_payments.TryGetValue(vouNo, out var items))
        {
            return Task.FromResult<IReadOnlyList<PaymentSubform>>(items.ToList());
        }
        return Task.FromResult<IReadOnlyList<PaymentSubform>>(new List<PaymentSubform>());
    }

    public Task AddAsync(Purchase purchase, IEnumerable<PurchaseSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default)
    {
        _purchases.RemoveAll(b => b.VouNo.Equals(purchase.VouNo, StringComparison.OrdinalIgnoreCase));
        _purchases.Add(purchase);
        _subforms[purchase.VouNo] = items.ToList();
        _payments[purchase.VouNo] = payments.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Purchase purchase, IEnumerable<PurchaseSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default)
    {
        return AddAsync(purchase, items, payments, cancellationToken);
    }

    public Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        _purchases.RemoveAll(b => b.VouNo.Equals(vouNo, StringComparison.OrdinalIgnoreCase));
        _subforms.Remove(vouNo);
        _payments.Remove(vouNo);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SalesVouType>> GetPurchaseVouTypesAsync(string? counter = null, string? headCategory = "Purchase", CancellationToken cancellationToken = default)
    {
        var list = new List<SalesVouType>
        {
            new() { VouType = "Purchase Gold", ShortName = "PG", HeadCategory = "Purchases", PrintFile = "PurchasePrint2" },
            new() { VouType = "Purchase Silver", ShortName = "PS", HeadCategory = "Purchases", PrintFile = "PurchasePrint2" },
            new() { VouType = "Purchase Diamond", ShortName = "PD", HeadCategory = "Purchases", PrintFile = "PurchasePrint2" },
            new() { VouType = "Purchase Return", ShortName = "PR", HeadCategory = "Purchase Return", PrintFile = "PurchaseReturnPrint" }
        };

        var isPurchase = string.IsNullOrWhiteSpace(headCategory) ||
                         string.Equals(headCategory, "Purchase", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(headCategory, "Purchases", StringComparison.OrdinalIgnoreCase);

        IEnumerable<SalesVouType> query = list;
        if (isPurchase)
        {
            query = query.Where(x => string.Equals(x.HeadCategory, "Purchase", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(x.HeadCategory, "Purchases", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            query = query.Where(x => string.Equals(x.HeadCategory, headCategory!.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<SalesVouType>>(query.ToList());
    }

    public Task<string> GetNextVouNoAsync(string vouType, string? shortName = null, CancellationToken cancellationToken = default)
    {
        var prefix = !string.IsNullOrWhiteSpace(shortName) ? shortName.Trim() : "PUR";
        long maxNumber = 0;

        foreach (var p in _purchases)
        {
            var numericPart = new string(p.VouNo.Where(char.IsDigit).ToArray());
            if (long.TryParse(numericPart, out var num) && num > maxNumber)
            {
                maxNumber = num;
            }
        }

        return Task.FromResult($"{prefix}-{maxNumber + 1:D4}");
    }

    public Task<IReadOnlyList<InvoiceListItem>> GetPurchaseListAsync(DateTime? fromDate = null, DateTime? toDate = null, string? vouType = null, CancellationToken cancellationToken = default)
    {
        var query = _purchases.AsEnumerable();
        if (fromDate.HasValue) query = query.Where(p => p.BillDate.Date >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(p => p.BillDate.Date <= toDate.Value.Date);
        if (!string.IsNullOrWhiteSpace(vouType)) query = query.Where(p => string.Equals(p.VouType, vouType.Trim(), StringComparison.OrdinalIgnoreCase));

        var list = query
            .OrderByDescending(p => p.BillDate)
            .ThenByDescending(p => p.VouNo)
            .Select(p => new InvoiceListItem
            {
                VouNo = p.VouNo,
                VouType = p.VouType,
                BillDate = p.BillDate,
                CustomerName = p.AccountName,
                Phone = p.Phone,
                NetAmt = p.NetAmt,
                Status = p.Status
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<InvoiceListItem>>(list);
    }

    public Task<IReadOnlyList<PurchaseRegisterItem>> GetPurchaseRegisterAsync(PurchaseRegisterFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _purchases.AsEnumerable();
        if (filter.FromDate.HasValue) query = query.Where(p => p.BillDate.Date >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue) query = query.Where(p => p.BillDate.Date <= filter.ToDate.Value.Date);
        if (!string.IsNullOrWhiteSpace(filter.VouType) && !string.Equals(filter.VouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => string.Equals(p.VouType, filter.VouType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var list = query
            .OrderBy(p => p.BillDate)
            .ThenBy(p => p.VouNo)
            .Select(p =>
            {
                var item = new PurchaseRegisterItem
                {
                    VouNo = p.VouNo,
                    VouType = p.VouType,
                    BillDate = p.BillDate,
                    DueDate = p.DueDate,
                    SuppBillNo = p.SuppBillNo,
                    SuppDate = p.SuppDate,
                    AccountName = p.AccountName,
                    CustomerName = p.CustomerName,
                    CustomerCode = p.CustomerCode,
                    Phone = p.Phone,
                    Address = p.Address,
                    Address2 = p.Address2,
                    City = p.City,
                    Pincode = p.Pincode,
                    Email = p.Email,
                    VatNo = p.VatNo,
                    VatType = p.VatType,
                    GrossAmt = p.GrossAmt,
                    DiscountAllowed = p.DiscountAllowed,
                    TaxableAmount = p.TaxableAmount,
                    Tax = p.Tax,
                    TaxAmt = p.TaxAmt,
                    CGSTAmt = p.CGSTAmt,
                    SGSTAmt = p.SGSTAmt,
                    IGSTAmt = p.IGSTAmt,
                    AddLess = p.AddLess,
                    AddLessNote = p.AddLessNote,
                    NetAmt = p.NetAmt,
                    Remarks = p.Remarks,
                    Narration = p.Narration,
                    Status = p.Status,
                    GrWt = p.GrWt,
                    NtWt = p.NtWt,
                    TotalQty = p.TotalQty,
                    Items = _subforms.TryGetValue(p.VouNo, out var sub) ? sub.ToList() : new List<PurchaseSubform>()
                };

                if (item.Items.Count > 0)
                {
                    if (item.TotalQty == 0) item.TotalQty = item.Items.Sum(x => x.Qty);
                    if (item.NtWt == 0) item.NtWt = item.Items.Sum(x => x.NtWt);
                    if (item.GrWt == 0) item.GrWt = item.Items.Sum(x => x.GrWt);
                }

                return item;
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(filter.ItemType) && !string.Equals(filter.ItemType, "ALL", StringComparison.OrdinalIgnoreCase) && !string.Equals(filter.ItemType, "*", StringComparison.OrdinalIgnoreCase))
        {
            list = list.Where(p => p.Items.Any(sub =>
                (!string.IsNullOrWhiteSpace(sub.ItemName) && sub.ItemName.Contains(filter.ItemType, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(sub.Purity) && sub.Purity.Contains(filter.ItemType, StringComparison.OrdinalIgnoreCase))
            )).ToList();
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var st = filter.SearchText.Trim();
            list = list.Where(p =>
                p.VouNo.Contains(st, StringComparison.OrdinalIgnoreCase) ||
                p.SuppBillNo.Contains(st, StringComparison.OrdinalIgnoreCase) ||
                p.PartyDisplayName.Contains(st, StringComparison.OrdinalIgnoreCase) ||
                p.City.Contains(st, StringComparison.OrdinalIgnoreCase) ||
                p.Phone.Contains(st, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        return Task.FromResult<IReadOnlyList<PurchaseRegisterItem>>(list);
    }

    public Task<IReadOnlyList<string>> GetAvailableItemTypesAsync(CancellationToken cancellationToken = default)
    {
        var types = _subforms.Values
            .SelectMany(x => x)
            .Select(x => x.ItemName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        if (!types.Contains("GOLD ORNAMENTS", StringComparer.OrdinalIgnoreCase)) types.Insert(0, "GOLD ORNAMENTS");
        if (!types.Contains("SILVER ORNAMENTS", StringComparer.OrdinalIgnoreCase)) types.Insert(1, "SILVER ORNAMENTS");

        return Task.FromResult<IReadOnlyList<string>>(types);
    }

    public async Task<DateRangeCompanyInfo> GetCompanyDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await Services.DateRangeService.GetCompanyDetailsAsync(cancellationToken);
    }
}
