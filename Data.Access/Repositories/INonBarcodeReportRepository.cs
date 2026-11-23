using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface INonBarcodeReportRepository
{
    Task<IReadOnlyList<NonBarcodeStockReportItem>> GetItemWiseStockAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NonBarcodeTransactionDetail>> GetItemWiseTransactionsAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NonBarcodeGroupSummary>> GetItemTypeWiseStockAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NonBarcodeTransactionDetail>> GetTransactionDetailsAsync(NonBarcodeStockReportFilter filter, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> ItemNames)> GetFilterMastersAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<string> ItemHeads, IReadOnlyList<string> SubGroups, IReadOnlyList<string> ItemNames)> GetItemFilterMastersAsync(CancellationToken cancellationToken = default);
}
