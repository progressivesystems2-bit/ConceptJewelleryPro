using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ICustomerRequirementRepository
{
    /// <summary>
    /// Logs a new customer requirement to the <c>CustomerRequirement</c> table.
    /// </summary>
    /// <returns>The auto-generated ID of the newly inserted row.</returns>
    Task<int> LogAsync(CustomerRequirement requirement, CancellationToken cancellationToken = default);
}
