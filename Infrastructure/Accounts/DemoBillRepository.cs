using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoBillRepository : IBillRepository
{
    private readonly List<Bill> _bills = new();
    private readonly Dictionary<string, List<BillSubform>> _subforms = new();
    private readonly Dictionary<string, List<PaymentSubform>> _payments = new();

    public Task<IReadOnlyList<Bill>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Bill>>(_bills.ToList());
    }

    public Task<Bill?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        var bill = _bills.FirstOrDefault(b => b.VouNo.Equals(vouNo, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(bill);
    }

    public Task<IReadOnlyList<BillSubform>> GetSubformItemsAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        if (_subforms.TryGetValue(vouNo, out var items))
        {
            return Task.FromResult<IReadOnlyList<BillSubform>>(items.ToList());
        }
        return Task.FromResult<IReadOnlyList<BillSubform>>(new List<BillSubform>());
    }

    public Task<IReadOnlyList<PaymentSubform>> GetPaymentItemsAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        if (_payments.TryGetValue(vouNo, out var items))
        {
            return Task.FromResult<IReadOnlyList<PaymentSubform>>(items.ToList());
        }
        return Task.FromResult<IReadOnlyList<PaymentSubform>>(new List<PaymentSubform>());
    }

    public Task AddAsync(Bill bill, IEnumerable<BillSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default)
    {
        _bills.RemoveAll(b => b.VouNo.Equals(bill.VouNo, StringComparison.OrdinalIgnoreCase));
        _bills.Add(bill);
        _subforms[bill.VouNo] = items.ToList();
        _payments[bill.VouNo] = payments.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Bill bill, IEnumerable<BillSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default)
    {
        return AddAsync(bill, items, payments, cancellationToken);
    }

    public Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        _bills.RemoveAll(b => b.VouNo.Equals(vouNo, StringComparison.OrdinalIgnoreCase));
        _subforms.Remove(vouNo);
        _payments.Remove(vouNo);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SalesVouType>> GetSalesVouTypesAsync(string? counter = null, string? headCategory = "Sales", CancellationToken cancellationToken = default)
    {
        var list = new List<SalesVouType>
        {
            new() { VouType = "TAX INVOICE GOLD", ShortName = "ATIG", HeadCategory = "Sales", CounterNo = "ad, AS, rg, b, us", PrintFile = "BillPrint" },
            new() { VouType = "RETAIL INVOICE GOLD", ShortName = "SVN", HeadCategory = "Sales", CounterNo = "LE, rg, us, ad", PrintFile = "BillPrint" },
            new() { VouType = "TAX INVOICE ARTICLES", ShortName = "ART", HeadCategory = "Sales", CounterNo = "ad, LE, rg, b, us", PrintFile = "BillPrint" },
            new() { VouType = "TAX INVOICE BULLION", ShortName = "TIB", HeadCategory = "Sales", CounterNo = "ad, AS, rg, b, us", PrintFile = "BillPrint" },
            new() { VouType = "RETAIL INVOICE DIAMOND", ShortName = "TID", HeadCategory = "Sales", CounterNo = "ad, c4, ba,rg, us", PrintFile = "BillPrintDiamond" },
            new() { VouType = "RETAIL INVOICE SILVER", ShortName = "AR", HeadCategory = "Sales", CounterNo = "LE, ad", PrintFile = "BillPrintSilver" },
            new() { VouType = "TAX INVOICE SILVER", ShortName = "T1", HeadCategory = "Sales", CounterNo = "LE, AS, ad", PrintFile = "BillPrintSilver" },
            new() { VouType = "RETAIL INVOICE ARTICLES", ShortName = "ARTN", HeadCategory = "Sales", CounterNo = "LE, us, ra, ad", PrintFile = "BillPrint" }
        };

        var filtered = list.Where(item =>
        {
            if (!string.IsNullOrWhiteSpace(headCategory) && !string.Equals(item.HeadCategory, headCategory.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(counter)) return true;

            if (string.IsNullOrWhiteSpace(item.CounterNo)) return true;
            var target = counter.Trim();
            var counters = item.CounterNo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return counters.Any(c => string.Equals(c, target, StringComparison.OrdinalIgnoreCase));
        }).ToList();

        return Task.FromResult<IReadOnlyList<SalesVouType>>(filtered);
    }

    public Task<string> GetNextVouNoAsync(string vouType, string? shortName = null, CancellationToken cancellationToken = default)
    {
        var shortPrefix = shortName;
        if (string.IsNullOrWhiteSpace(shortPrefix))
        {
            shortPrefix = vouType switch
            {
                "TAX INVOICE GOLD" => "ATIG",
                "RETAIL INVOICE GOLD" => "SVN",
                "TAX INVOICE ARTICLES" => "ART",
                "TAX INVOICE BULLION" => "TIB",
                "RETAIL INVOICE DIAMOND" => "TID",
                "RETAIL INVOICE SILVER" => "AR",
                "TAX INVOICE SILVER" => "T1",
                "RETAIL INVOICE ARTICLES" => "ARTN",
                _ => "INV"
            };
        }

        var maxNumber = 0;
        var digitsCount = 5;
        var regex = new System.Text.RegularExpressions.Regex(@"\d+");

        foreach (var bill in _bills.Where(b => string.Equals(b.VouType, vouType, StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrWhiteSpace(bill.VouNo))
            {
                var match = regex.Match(bill.VouNo);
                if (match.Success && int.TryParse(match.Value, out var num))
                {
                    if (num > maxNumber)
                    {
                        maxNumber = num;
                        if (match.Value.Length > digitsCount)
                        {
                            digitsCount = match.Value.Length;
                        }
                    }
                }
            }
        }

        var nextNum = maxNumber + 1;
        var padding = new string('0', digitsCount);
        return Task.FromResult($"{shortPrefix.Trim()}{nextNum.ToString(padding)}");
    }

    public Task<BarcodeDetails?> GetBarcodeDetailsAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return Task.FromResult<BarcodeDetails?>(null);
        var clean = barcode.Trim();

        var demo = new BarcodeDetails
        {
            Barcode = clean,
            ItemName = "CHAIN",
            Purity = "916HALL MARKED",
            Qty = 1,
            GrossWt = 9.12m,
            NetWt = 9.12m,
            Making = 3648m,
            MPerGr = 450m,
            Description = "FIGARRO"
        };
        return Task.FromResult<BarcodeDetails?>(demo);
    }

    public Task<IReadOnlyList<string>> GetAvailableStockBarcodesAsync(string? targetItemHead = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> list = new List<string> { "000001", "000002", "000003", "000004", "000005" };
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<TempStockItem>> GetTempStockItemsAsync(string? targetItemHead = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TempStockItem> list = new List<TempStockItem>
        {
            new TempStockItem { Barcode = "000001", ItemName = "CHAIN", HeadGroup = "GOLD", GrossWt = 9.12m, NetWt = 9.12m, Qty = 1 },
            new TempStockItem { Barcode = "000002", ItemName = "RING", HeadGroup = "GOLD", GrossWt = 4.20m, NetWt = 4.20m, Qty = 1 }
        };
        return Task.FromResult(list);
    }

    public async Task<IReadOnlyList<InvoiceListItem>> GetInvoiceListAsync(DateTime? fromDate = null, DateTime? toDate = null, string? vouType = null, CancellationToken cancellationToken = default)
    {
        var salesTypes = await GetSalesVouTypesAsync(null, "Sales", cancellationToken);
        var validVouTypes = new HashSet<string>(salesTypes.Select(s => s.VouType), StringComparer.OrdinalIgnoreCase);

        var filtered = _bills.Where(b => validVouTypes.Contains(b.VouType));

        if (fromDate.HasValue)
        {
            filtered = filtered.Where(b => b.BillDate.Date >= fromDate.Value.Date);
        }
        if (toDate.HasValue)
        {
            filtered = filtered.Where(b => b.BillDate.Date <= toDate.Value.Date);
        }
        if (!string.IsNullOrWhiteSpace(vouType) && !string.Equals(vouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(b => string.Equals(b.VouType, vouType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var result = filtered.Select(b =>
        {
            var subs = _subforms.TryGetValue(b.VouNo, out var sList) ? sList : new List<BillSubform>();
            var totalQty = subs.Sum(s => s.Qty);
            var totalNtWt = subs.Sum(s => s.NtWt);
            var totalGrWt = subs.Sum(s => s.GrWt);
            var rawTaxAmt = b.TaxAmt != 0 ? b.TaxAmt : subs.Sum(s => s.VatAmt);
            var rawTaxRate = b.Tax;

            decimal taxAmt = rawTaxAmt;
            decimal taxable;

            if ((taxAmt <= 28m || taxAmt >= b.NetAmt) && b.NetAmt > 0)
            {
                decimal rate = rawTaxRate > 0 ? rawTaxRate : (rawTaxAmt > 0 && rawTaxAmt <= 28m ? rawTaxAmt : 3.00m);
                taxable = Math.Round(b.NetAmt / (1m + (rate / 100m)), 2);
                taxAmt = b.NetAmt - taxable;
            }
            else
            {
                taxable = b.NetAmt - taxAmt;
                if (taxable <= 0 && b.NetAmt > 0) taxable = b.NetAmt;
            }

            return new InvoiceListItem
            {
                VouNo = b.VouNo,
                BillDate = b.BillDate,
                VouType = b.VouType,
                CustomerName = b.CustomerName,
                Phone = b.Phone,
                City = b.City,
                Address = b.Address,
                NetAmt = b.NetAmt,
                GrossAmt = b.GrossAmt,
                DiscountAllowed = b.DiscountAllowed,
                Tax = b.Tax,
                TaxAmt = taxAmt,
                TaxableAmt = taxable,
                Qty = totalQty,
                NetWt = totalNtWt,
                GrWt = totalGrWt,
                BillCanceled = b.BillCanceled,
                Status = b.Status,
                Items = subs
            };
        }).OrderByDescending(b => b.BillDate).ThenByDescending(b => b.VouNo).ToList();

        return result;
    }

    public Task<CustomerPurchaseSummary> GetCustomerPurchaseSummaryAsync(
        string? phone,
        string? customerName,
        string? customerCode,
        CancellationToken cancellationToken = default)
    {
        var summary = new CustomerPurchaseSummary();
        var cleanPhone = !string.IsNullOrWhiteSpace(phone) ? new string(phone.Where(char.IsDigit).ToArray()) : string.Empty;
        var cleanName = customerName?.Trim() ?? string.Empty;
        var cleanCode = customerCode?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cleanPhone) && string.IsNullOrWhiteSpace(cleanName) && string.IsNullOrWhiteSpace(cleanCode))
        {
            return Task.FromResult(summary);
        }

        var matchedBills = _bills
            .Where(b => !b.BillCanceled && (
                (cleanPhone.Length >= 10 && b.Phone.Contains(cleanPhone.Substring(cleanPhone.Length - 10))) ||
                (!string.IsNullOrWhiteSpace(cleanName) && string.Equals(b.CustomerName, cleanName, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(cleanCode) && string.Equals(b.CustomerCode, cleanCode, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(b => b.BillDate)
            .ThenByDescending(b => b.VouNo)
            .ToList();

        if (matchedBills.Count == 0)
        {
            return Task.FromResult(summary);
        }

        summary.TotalBillsCount = matchedBills.Count;
        summary.TotalPurchaseValue = matchedBills.Sum(x => x.NetAmt);
        summary.AvgPurchaseValue = Math.Round(summary.TotalPurchaseValue / matchedBills.Count, 2);
        summary.LastPurchaseDate = matchedBills[0].BillDate;

        var historyItems = new List<CustomerPurchaseHistoryItem>();
        foreach (var b in matchedBills)
        {
            if (_subforms.TryGetValue(b.VouNo, out var items))
            {
                foreach (var item in items)
                {
                    historyItems.Add(new CustomerPurchaseHistoryItem
                    {
                        Date = b.BillDate,
                        VouNo = b.VouNo,
                        ItemName = item.ItemName,
                        Qty = item.Qty,
                        Amount = item.Amount,
                        NtWt = item.NtWt
                    });
                }
            }
        }

        summary.Items = historyItems;
        return Task.FromResult(summary);
    }

    public Task<IReadOnlyList<PaymentSubform>> GetSalesPaymentsAsync(DateTime? fromDate = null, DateTime? toDate = null, string? vouType = null, CancellationToken cancellationToken = default)
    {
        var result = new List<PaymentSubform>();
        var query = _bills.AsEnumerable();

        if (fromDate.HasValue) query = query.Where(b => b.BillDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(b => b.BillDate <= toDate.Value.Date);
        if (!string.IsNullOrWhiteSpace(vouType) && !string.Equals(vouType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => string.Equals(b.VouType, vouType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        foreach (var bill in query)
        {
            if (_payments.TryGetValue(bill.VouNo, out var pList))
            {
                result.AddRange(pList);
            }
        }

        return Task.FromResult<IReadOnlyList<PaymentSubform>>(result);
    }

    public async Task<DateRangeCompanyInfo> GetCompanyDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await Services.DateRangeService.GetCompanyDetailsAsync(cancellationToken);
    }
}
