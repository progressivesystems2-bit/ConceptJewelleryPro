using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IBillRepository
{
    Task<IReadOnlyList<Bill>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Bill?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillSubform>> GetSubformItemsAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentSubform>> GetPaymentItemsAsync(string vouNo, CancellationToken cancellationToken = default);

    Task AddAsync(Bill bill, IEnumerable<BillSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default);

    Task UpdateAsync(Bill bill, IEnumerable<BillSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default);

    Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesVouType>> GetSalesVouTypesAsync(string? counter = null, string? headCategory = "Sales", CancellationToken cancellationToken = default);

    Task<string> GetNextVouNoAsync(string vouType, string? shortName = null, CancellationToken cancellationToken = default);
    Task<BarcodeDetails?> GetBarcodeDetailsAsync(string barcode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAvailableStockBarcodesAsync(string? targetItemHead = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TempStockItem>> GetTempStockItemsAsync(string? targetItemHead = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceListItem>> GetInvoiceListAsync(DateTime? fromDate = null, DateTime? toDate = null, string? vouType = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentSubform>> GetSalesPaymentsAsync(DateTime? fromDate = null, DateTime? toDate = null, string? vouType = null, CancellationToken cancellationToken = default);
    Task<CustomerPurchaseSummary> GetCustomerPurchaseSummaryAsync(string? phone, string? customerName, string? customerCode, CancellationToken cancellationToken = default);
    Task<DateRangeCompanyInfo> GetCompanyDetailsAsync(CancellationToken cancellationToken = default);
}
