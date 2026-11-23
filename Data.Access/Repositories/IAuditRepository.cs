using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IAuditRepository
{
    Task LogAuditAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task LogAuditBatchAsync(IEnumerable<AuditLogEntry> entries, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(AuditReportFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetDistinctUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetDistinctEntryTypesAsync(CancellationToken cancellationToken = default);
    Task MarkCheckedAsync(IEnumerable<int> idNos, bool isChecked, CancellationToken cancellationToken = default);
    Task DeleteAuditLogsAsync(IEnumerable<int> idNos, CancellationToken cancellationToken = default);
}
