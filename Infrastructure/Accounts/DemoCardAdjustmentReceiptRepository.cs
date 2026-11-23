using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoCardAdjustmentReceiptRepository : ICardAdjustmentReceiptRepository
{
    private readonly List<CardAdjustmentReceipt> _receipts = new();
    private readonly List<CardAdjustmentReceiptItem> _pendingItems = new()
    {
        new CardAdjustmentReceiptItem
        {
            SrNo = 1,
            DbName = "SALE",
            BillNo = "AR00001",
            BillDate = DateTime.Today.AddDays(-2),
            AccountName = "WALK-IN CUSTOMER",
            CardNo = "541234******1234",
            CardBank = "HDFC",
            CardAmount = 3000m,
            CommissionPercent = 0m,
            CommissionAmount = 0m,
            ReceivedAmount = 3000m,
            IsSelected = true
        },
        new CardAdjustmentReceiptItem
        {
            SrNo = 2,
            DbName = "SALE",
            BillNo = "AR00002",
            BillDate = DateTime.Today.AddDays(-1),
            AccountName = "PRIYA SHARMA",
            CardNo = "411111******1111",
            CardBank = "HDFC",
            CardAmount = 15000m,
            CommissionPercent = 0m,
            CommissionAmount = 0m,
            ReceivedAmount = 15000m,
            IsSelected = true
        }
    };

    public Task<string> GetNextReceiptNoAsync(string? counter = null, CancellationToken cancellationToken = default)
    {
        var nextNos = _receipts.Count + 1;
        var counterStr = string.IsNullOrWhiteSpace(counter) ? "" : counter.Trim();
        return Task.FromResult($"CAR{counterStr}{nextNos:D4}");
    }

    public Task<CardAdjustmentReceipt?> GetByReceiptNoAsync(string receiptNo, CancellationToken cancellationToken = default)
    {
        var found = _receipts.FirstOrDefault(r => string.Equals(r.ReceiptNo, receiptNo, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<CardAdjustmentReceipt>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var q = _receipts.Where(r => r.Status != "Deleted");
        if (fromDate.HasValue) q = q.Where(r => r.ReceiptDate >= fromDate.Value.Date);
        if (toDate.HasValue) q = q.Where(r => r.ReceiptDate <= toDate.Value.Date);
        IReadOnlyList<CardAdjustmentReceipt> list = q.OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.Nos).ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<CardAdjustmentReceiptItem>> GetPendingCardTransactionsAsync(string cardBank, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var items = _pendingItems
            .Where(x => string.Equals(x.CardBank, cardBank, StringComparison.OrdinalIgnoreCase) &&
                        x.BillDate.Date >= fromDate.Date && x.BillDate.Date <= toDate.Date)
            .ToList();
        return Task.FromResult<IReadOnlyList<CardAdjustmentReceiptItem>>(items);
    }

    public Task<IReadOnlyList<string>> GetCardBanksAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> banks = new List<string> { "HDFC", "SBI", "ICICI", "AXIS", "paytm pay" };
        return Task.FromResult(banks);
    }

    public Task SaveAsync(CardAdjustmentReceipt receipt, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        if (updateExisting)
        {
            _receipts.RemoveAll(r => string.Equals(r.ReceiptNo, receipt.ReceiptNo, StringComparison.OrdinalIgnoreCase));
        }

        if (receipt.Nos <= 0) receipt.Nos = _receipts.Count + 1;
        if (string.IsNullOrWhiteSpace(receipt.ReceiptNo)) receipt.ReceiptNo = $"CAR{receipt.Nos:D4}";
        if (string.IsNullOrWhiteSpace(receipt.VouNo)) receipt.VouNo = $"RC{receipt.Nos:D4}";

        _receipts.Add(receipt);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string receiptNo, CancellationToken cancellationToken = default)
    {
        var found = _receipts.FirstOrDefault(r => string.Equals(r.ReceiptNo, receiptNo, StringComparison.OrdinalIgnoreCase));
        if (found != null) found.Status = "Deleted";
        return Task.CompletedTask;
    }

    public Task<string?> NavigateReceiptNoAsync(string? currentReceiptNo, ReceiptNavigationDirection direction, CancellationToken cancellationToken = default)
    {
        var active = _receipts.Where(r => r.Status != "Deleted").OrderBy(r => r.ReceiptDate).ThenBy(r => r.Nos).ToList();
        if (active.Count == 0) return Task.FromResult<string?>(null);

        if (direction == ReceiptNavigationDirection.First) return Task.FromResult<string?>(active.First().ReceiptNo);
        if (direction == ReceiptNavigationDirection.Last) return Task.FromResult<string?>(active.Last().ReceiptNo);

        var idx = active.FindIndex(r => string.Equals(r.ReceiptNo, currentReceiptNo, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return Task.FromResult<string?>(active.Last().ReceiptNo);

        if (direction == ReceiptNavigationDirection.Previous)
        {
            var prevIdx = Math.Max(0, idx - 1);
            return Task.FromResult<string?>(active[prevIdx].ReceiptNo);
        }

        if (direction == ReceiptNavigationDirection.Next)
        {
            var nextIdx = Math.Min(active.Count - 1, idx + 1);
            return Task.FromResult<string?>(active[nextIdx].ReceiptNo);
        }

        return Task.FromResult<string?>(currentReceiptNo);
    }
}
