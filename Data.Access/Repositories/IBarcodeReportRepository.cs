using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IBarcodeReportRepository
{
    Task<IReadOnlyList<BarcodeStockReportItem>> GetStockInHandAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeGroupSummary>> GetStockHeadGroupWiseAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeGroupSummary>> GetStockSupplierWiseAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeGroupSummary>> GetItemWiseStockAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeStockReportItem>> GetItemWiseSalesAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeStockReportItem>> GetItemWiseSalesReturnAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeMovementItem>> GetBarcodeMovementAsync(string? barcode, BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeGroupSummary>> GetHeadGroupSummariesAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeGroupSummary>> GetSubGroupSummariesAsync(string headGroup, BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeStockReportItem>> GetHeadGroupBarcodeDetailsAsync(string headGroup, string subGroup, BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeStockReportItem>> GetItemMismatchesAsync(BarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> Purities, IReadOnlyList<string> ItemNames, IReadOnlyList<string> Suppliers, IReadOnlyList<string> BoxNos)> GetFilterMastersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetBoxNosAsync(CancellationToken cancellationToken = default);

    Task UpdateMrpAsync(string barcode, decimal mrp, CancellationToken cancellationToken = default);

    Task UpdateBoxNoAsync(string barcode, string boxNo, CancellationToken cancellationToken = default);

    Task UpdateBoxNosAsync(IEnumerable<string> barcodes, string boxNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeStockReportItem>> GetScannedStockCheckItemsAsync(CancellationToken cancellationToken = default);

    Task SaveScannedStockCheckItemAsync(BarcodeStockReportItem item, CancellationToken cancellationToken = default);

    Task RemoveScannedStockCheckItemAsync(string barcode, CancellationToken cancellationToken = default);

    Task ClearScannedStockCheckItemsAsync(CancellationToken cancellationToken = default);

    Task<BarcodeStockReportItem?> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    void InvalidateMasterCache();
}
