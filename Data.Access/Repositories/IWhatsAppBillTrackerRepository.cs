using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IWhatsAppBillTrackerRepository
{
    Task AddAsync(WhatsAppBillTracker tracker, CancellationToken cancellationToken = default);
    Task<WhatsAppBillTracker?> GetByWamidAsync(string wamid, CancellationToken cancellationToken = default);
    Task<WhatsAppBillTracker?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, WhatsAppBillTracker>> GetByInvoiceNosAsync(IEnumerable<string> invoiceNos, CancellationToken cancellationToken = default);
    Task UpdateFeedbackAsync(string wamid, string rating, string? reply = null, CancellationToken cancellationToken = default);
    Task UpdateDeliveryStatusAsync(string wamid, string deliveryStatus, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WhatsAppBillTracker>> GetTrackersForStatusSyncAsync(int daysBack = 14, CancellationToken cancellationToken = default);
}
