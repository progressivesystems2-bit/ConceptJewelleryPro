using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ISchemeDueRepository
{
    Task<IReadOnlyList<SchemeDueListItem>> GetSchemeDueListAsync(DateTime asOnDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherEntry>> GetSchemePaymentHistoryAsync(string scode, CancellationToken cancellationToken = default);
}
