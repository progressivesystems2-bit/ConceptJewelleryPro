using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ICardSetupRepository
{
    Task<IReadOnlyList<CardSetupItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<CardSetupItem> items, CancellationToken cancellationToken = default);
}
