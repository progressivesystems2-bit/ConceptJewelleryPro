using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ITrialBalanceRepository
{
    Task<IReadOnlyList<TrialBalanceItem>> GetTrialBalanceAsync(TrialBalanceFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherAuditItem>> AuditVouchersAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}
