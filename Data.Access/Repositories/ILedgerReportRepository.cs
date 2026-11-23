using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ILedgerReportRepository
{
    Task<IReadOnlyList<Account>> GetLedgerAccountsAsync(CancellationToken cancellationToken = default);

    Task<LedgerAccountSummary> GetAccountSummaryAsync(string accountName, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerEntry>> GetLedgerInRupeesAsync(LedgerReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerEntry>> GetLedgerInWeightAsync(LedgerReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerNoteBalanceItem>> GetNoteWiseBalanceAsync(LedgerReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerGroupAccountItem>> GetLedgerGroupSummaryAsync(string headCategory, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerEntry>> GetLedgerGroupEntriesAsync(string headCategory, DateTime fromDate, DateTime toDate, bool isWeight, CancellationToken cancellationToken = default);
}
