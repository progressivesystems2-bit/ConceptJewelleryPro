using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IVoucherRepository
{
    Task<IReadOnlyList<VoucherEntry>> GetRecentAsync(string vouType, int count = 50, CancellationToken cancellationToken = default);

    Task<VoucherEntry?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<int> GetNextNosAsync(string vouType, CancellationToken cancellationToken = default);

    Task<string> GetNextVouNoAsync(string vouType, CancellationToken cancellationToken = default);

    Task SaveAsync(VoucherEntry voucher, bool updateExisting = false, CancellationToken cancellationToken = default);

    Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetBankAccountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCashAccountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCardTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VoucherEntry>> GetByScodeAsync(string scode, CancellationToken cancellationToken = default);

    Task<decimal> GetAccountBalanceAsync(string accountName, CancellationToken cancellationToken = default);
}
