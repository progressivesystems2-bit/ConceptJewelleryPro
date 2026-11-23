using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IBoxMasterRepository
{
    Task<IReadOnlyList<BoxMaster>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<BoxMaster?> GetByBoxNoAsync(string boxNo, CancellationToken cancellationToken = default);

    Task AddAsync(BoxMaster box, CancellationToken cancellationToken = default);

    Task UpdateAsync(string originalBoxNo, BoxMaster box, CancellationToken cancellationToken = default);

    Task DeleteAsync(string boxNo, CancellationToken cancellationToken = default);

    Task<int> GetNextSerialAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRacksAsync(CancellationToken cancellationToken = default);
}
