using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IUserPermissionRepository
{
    Task<IReadOnlyList<UserPermission>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserPermission?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task AddAsync(UserPermission userPermission, CancellationToken cancellationToken = default);

    Task UpdateAsync(string originalUsername, UserPermission userPermission, CancellationToken cancellationToken = default);

    Task DeleteAsync(string username, CancellationToken cancellationToken = default);
}
