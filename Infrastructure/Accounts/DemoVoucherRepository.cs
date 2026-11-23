using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoVoucherRepository : IVoucherRepository
{
    private readonly List<VoucherEntry> _vouchers = new();

    public DemoVoucherRepository()
    {
        // Seed sample vouchers
        _vouchers.Add(new VoucherEntry
        {
            VouNo = "PM0001",
            VouDate = DateTime.Today,
            Nos = 1,
            VouType = VoucherType.Payments,
            DrAccount = "SAGUPTA FIRDOUS",
            CrAccount = "CASH",
            DrAmt = 5000m,
            CrAmt = 5000m,
            Narration = "BEING CASH PAID TO SAGUPTA FIRDOUS",
            Narr2 = "BEING CASH PAID TO SAGUPTA FIRDOUS",
            User = "admin",
            PaymentMode = "Cash",
            Status = "Saved"
        });

        _vouchers.Add(new VoucherEntry
        {
            VouNo = "RC0001",
            VouDate = DateTime.Today,
            Nos = 1,
            VouType = VoucherType.Receipts,
            DrAccount = "HDFC BANK -NEW 50200040965163",
            CrAccount = "DEBJANI SWAIN",
            DrAmt = 6200m,
            CrAmt = 6200m,
            Narration = "Being DrAmt Received By Cheque No : Chq Dated :  From DEBJANI SWAIN",
            Narr2 = "Being DrAmt Received By Cheque No : Chq Dated :  From DEBJANI SWAIN",
            User = "admin",
            PaymentMode = "Online",
            Status = "Saved"
        });

        _vouchers.Add(new VoucherEntry
        {
            VouNo = "JV0001",
            VouDate = DateTime.Today,
            Nos = 1,
            VouType = VoucherType.Journal,
            DrAccount = "DEBJANI SWAIN",
            CrAccount = "DISCOUNT ALLOWED",
            DrAmt = 200m,
            CrAmt = 200m,
            Narration = "Being discount adjustment entry",
            Narr2 = "Being discount adjustment entry",
            User = "admin",
            PaymentMode = "Cash",
            Status = "Saved"
        });
    }

    public Task<IReadOnlyList<VoucherEntry>> GetRecentAsync(string vouType, int count = 50, CancellationToken cancellationToken = default)
    {
        var list = _vouchers
            .Where(v => string.Equals(v.VouType, vouType, StringComparison.OrdinalIgnoreCase) && v.Status != "Deleted")
            .OrderByDescending(v => v.Nos)
            .Take(count)
            .ToList();
        return Task.FromResult<IReadOnlyList<VoucherEntry>>(list);
    }

    public Task<VoucherEntry?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        var found = _vouchers.FirstOrDefault(v => string.Equals(v.VouNo, vouNo, StringComparison.OrdinalIgnoreCase) && v.Status != "Deleted");
        return Task.FromResult(found);
    }

    public Task<int> GetNextNosAsync(string vouType, CancellationToken cancellationToken = default)
    {
        var max = _vouchers
            .Where(v => string.Equals(v.VouType, vouType, StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Nos)
            .DefaultIfEmpty(0)
            .Max();
        return Task.FromResult(max + 1);
    }

    public async Task<string> GetNextVouNoAsync(string vouType, CancellationToken cancellationToken = default)
    {
        var nextNos = await GetNextNosAsync(vouType, cancellationToken);
        var prefix = VoucherType.GetPrefix(vouType);
        return $"{prefix}{nextNos:D4}";
    }

    public Task SaveAsync(VoucherEntry voucher, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        if (updateExisting)
        {
            var idx = _vouchers.FindIndex(v => string.Equals(v.VouNo, voucher.VouNo, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _vouchers[idx] = voucher;
            }
            else
            {
                _vouchers.Add(voucher);
            }
        }
        else
        {
            if (voucher.Nos <= 0)
            {
                voucher.Nos = _vouchers.Where(v => string.Equals(v.VouType, voucher.VouType, StringComparison.OrdinalIgnoreCase)).Select(v => v.Nos).DefaultIfEmpty(0).Max() + 1;
            }
            if (string.IsNullOrWhiteSpace(voucher.VouNo))
            {
                voucher.VouNo = $"{VoucherType.GetPrefix(voucher.VouType)}{voucher.Nos:D4}";
            }
            _vouchers.Add(voucher);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        var item = _vouchers.FirstOrDefault(v => string.Equals(v.VouNo, vouNo, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            item.Status = "Deleted";
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetBankAccountsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> list = new[] { "HDFC BANK -NEW 50200040965163", "STATE BANK OF INDIA 33162560995" };
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<string>> GetCashAccountsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> list = new[] { "CASH", "PETTY CASH" };
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<string>> GetCardTypesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> list = new[] { "VISA / MASTER", "RUPAY", "AMEX", "CREDIT CARD", "DEBIT CARD" };
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<VoucherEntry>> GetByScodeAsync(string scode, CancellationToken cancellationToken = default)
    {
        var list = _vouchers
            .Where(v => string.Equals(v.Scode, scode, StringComparison.OrdinalIgnoreCase) && v.Status != "Deleted")
            .OrderBy(v => v.InstallmentNo)
            .ThenBy(v => v.Nos)
            .ToList();
        return Task.FromResult<IReadOnlyList<VoucherEntry>>(list);
    }

    public Task<decimal> GetAccountBalanceAsync(string accountName, CancellationToken cancellationToken = default)
    {
        var drSum = _vouchers.Where(v => string.Equals(v.DrAccount, accountName, StringComparison.OrdinalIgnoreCase) && v.Status != "Deleted").Sum(v => v.DrAmt);
        var crSum = _vouchers.Where(v => string.Equals(v.CrAccount, accountName, StringComparison.OrdinalIgnoreCase) && v.Status != "Deleted").Sum(v => v.CrAmt);
        return Task.FromResult(drSum - crSum);
    }
}
