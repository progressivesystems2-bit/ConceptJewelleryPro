using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IAccountRepository
{
    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);

    Task DeleteAsync(string partyCode, CancellationToken cancellationToken = default);

    Task<Account?> FindByPartyCodeAsync(string partyCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAdvanceOptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetHeadCategoriesAsync(CancellationToken cancellationToken = default);
}
