using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoWhatsAppBillTrackerRepository : IWhatsAppBillTrackerRepository
{
    private readonly ConcurrentDictionary<string, WhatsAppBillTracker> _store = new(StringComparer.OrdinalIgnoreCase);
    private long _autoId = 1;

    public Task AddAsync(WhatsAppBillTracker tracker, CancellationToken cancellationToken = default)
    {
        tracker.WhatsAppBillTrackerId = Interlocked.Increment(ref _autoId);
        _store[tracker.Wamid] = tracker;
        return Task.CompletedTask;
    }

    public Task<WhatsAppBillTracker?> GetByWamidAsync(string wamid, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(wamid, out var tracker);
        return Task.FromResult(tracker);
    }

    public Task<WhatsAppBillTracker?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default)
    {
        var match = _store.Values.FirstOrDefault(x => string.Equals(x.InvoiceNo, invoiceNo, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<IReadOnlyDictionary<string, WhatsAppBillTracker>> GetByInvoiceNosAsync(IEnumerable<string> invoiceNos, CancellationToken cancellationToken = default)
    {
        var set = new HashSet<string>(invoiceNos.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
        var dict = _store.Values
            .Where(x => set.Contains(x.InvoiceNo))
            .ToDictionary(x => x.InvoiceNo, x => x, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyDictionary<string, WhatsAppBillTracker>>(dict);
    }

    public Task UpdateFeedbackAsync(string wamid, string rating, string? reply = null, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(wamid, out var tracker))
        {
            tracker.Rating = rating;
            tracker.Reply = reply;
            tracker.UpdatedAt = DateTime.Now;
        }
        return Task.CompletedTask;
    }

    public Task UpdateDeliveryStatusAsync(string wamid, string deliveryStatus, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(wamid, out var tracker))
        {
            tracker.DeliveryStatus = deliveryStatus;
            tracker.Rating = deliveryStatus;
            tracker.UpdatedAt = DateTime.Now;
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WhatsAppBillTracker>> GetTrackersForStatusSyncAsync(int daysBack = 14, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.Today.AddDays(-daysBack);
        var matches = _store.Values
            .Where(t => t.SentAt >= cutoff && !string.IsNullOrWhiteSpace(t.Wamid))
            .Where(t =>
            {
                var st = t.DeliveryStatus ?? t.Rating;
                return !string.Equals(st, "Read", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(st, "Failed", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(t => t.SentAt)
            .Take(100)
            .ToList();

        return Task.FromResult<IReadOnlyList<WhatsAppBillTracker>>(matches);
    }
}
