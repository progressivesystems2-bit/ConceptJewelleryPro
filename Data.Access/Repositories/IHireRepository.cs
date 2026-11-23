using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IHireRepository
{
    // Hire Issue
    Task<string> GetNextIssueNoAsync(CancellationToken cancellationToken = default);
    Task<int> GetNextIssueNosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HireIssuedItem>> GetRecentIssuedAsync(int top = 50, CancellationToken cancellationToken = default);
    Task<HireIssuedItem?> GetIssuedByNoAsync(string issueNo, CancellationToken cancellationToken = default);
    Task SaveIssuedAsync(HireIssuedItem item, bool isUpdate = false, CancellationToken cancellationToken = default);
    Task DeleteIssuedAsync(string issueNo, CancellationToken cancellationToken = default);

    // Hire Receive
    Task<string> GetNextRecNoAsync(CancellationToken cancellationToken = default);
    Task<int> GetNextRecNosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HireReceivedItem>> GetRecentReceivedAsync(int top = 50, CancellationToken cancellationToken = default);
    Task<HireReceivedItem?> GetReceivedByNoAsync(string recNo, CancellationToken cancellationToken = default);
    Task SaveReceivedAsync(HireReceivedItem item, bool isUpdate = false, CancellationToken cancellationToken = default);
    Task DeleteReceivedAsync(string recNo, CancellationToken cancellationToken = default);

    // Lookups
    Task<IReadOnlyList<Account>> GetArtisansAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default);
}
