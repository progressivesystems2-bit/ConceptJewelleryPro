using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IStockTransferRepository
{
    Task<string> GetNextTransferNoAsync(CancellationToken cancellationToken = default);

    Task<StockTransfer?> GetByTransferNoAsync(string transferNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockTransfer>> GetAllTransfersAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockTransferItem>> GetTransferItemsAsync(string transferNo, CancellationToken cancellationToken = default);

    Task SaveTransferAsync(StockTransfer transfer, IEnumerable<StockTransferItem> items, CancellationToken cancellationToken = default);

    Task DeleteTransferAsync(string transferNo, CancellationToken cancellationToken = default);

    Task<StockTransferItem?> LookupBarcodeForTransferAsync(string barcode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLocationsAsync(CancellationToken cancellationToken = default);
}
