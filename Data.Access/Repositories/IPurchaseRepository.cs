using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IPurchaseRepository
{
    Task<IReadOnlyList<Purchase>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Purchase?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseSubform>> GetSubformItemsAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentSubform>> GetPaymentItemsAsync(string vouNo, CancellationToken cancellationToken = default);

    Task AddAsync(Purchase purchase, IEnumerable<PurchaseSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default);

    Task UpdateAsync(Purchase purchase, IEnumerable<PurchaseSubform> items, IEnumerable<PaymentSubform> payments, CancellationToken cancellationToken = default);

    Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesVouType>> GetPurchaseVouTypesAsync(string? counter = null, string? headCategory = "Purchase", CancellationToken cancellationToken = default);

    Task<string> GetNextVouNoAsync(string vouType, string? shortName = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceListItem>> GetPurchaseListAsync(DateTime? fromDate = null, DateTime? toDate = null, string? vouType = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseRegisterItem>> GetPurchaseRegisterAsync(PurchaseRegisterFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAvailableItemTypesAsync(CancellationToken cancellationToken = default);
    Task<DateRangeCompanyInfo> GetCompanyDetailsAsync(CancellationToken cancellationToken = default);
}
