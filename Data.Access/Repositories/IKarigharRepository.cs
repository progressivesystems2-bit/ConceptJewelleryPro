using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IKarigharRepository
{
    // Metal Issued (Karighar Issue)
    Task<string> GetNextIssueNoAsync(CancellationToken cancellationToken = default);
    Task<int> GetNextIssueNosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetalIssuedItem>> GetAllMetalIssuedAsync(int top = 100, CancellationToken cancellationToken = default);
    Task<MetalIssuedItem?> GetMetalIssuedByIdAsync(string issueNo, CancellationToken cancellationToken = default);
    Task SaveMetalIssuedAsync(MetalIssuedItem item, bool isUpdate = false, CancellationToken cancellationToken = default);
    Task DeleteMetalIssuedAsync(string issueNo, CancellationToken cancellationToken = default);

    // Metal Received (Karighar Receive)
    Task<string> GetNextRecNoAsync(CancellationToken cancellationToken = default);
    Task<int> GetNextRecNosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetalReceivedItem>> GetAllMetalReceivedAsync(int top = 100, CancellationToken cancellationToken = default);
    Task<MetalReceivedItem?> GetMetalReceivedByIdAsync(string recNo, CancellationToken cancellationToken = default);
    Task SaveMetalReceivedAsync(MetalReceivedItem item, bool isUpdate = false, CancellationToken cancellationToken = default);
    Task DeleteMetalReceivedAsync(string recNo, CancellationToken cancellationToken = default);

    // Shared Lookups
    Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default);
}
