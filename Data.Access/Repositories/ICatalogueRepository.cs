using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ICatalogueRepository
{
    /// <summary>
    /// Returns a filtered list of active barcode items for the catalogue.
    /// All filter fields are optional; omitting them returns all active items.
    /// </summary>
    Task<IReadOnlyList<CatalogueItem>> GetCatalogueAsync(
        CatalogueFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns distinct active item names in stock.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableItemNamesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns distinct sizes in stock.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableSizesAsync(
        CancellationToken cancellationToken = default);
}
