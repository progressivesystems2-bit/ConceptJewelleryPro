using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IStaffRepository
{
    Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<StaffMember?> GetByNameAsync(string staffName, CancellationToken cancellationToken = default);
    Task SaveAsync(StaffMember member, bool isUpdate = false, string? oldStaffName = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string staffName, CancellationToken cancellationToken = default);
}
