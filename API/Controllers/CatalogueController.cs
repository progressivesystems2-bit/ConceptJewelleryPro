using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConceptJewelleryPro.API.Controllers;

/// <summary>
/// Returns the jewellery catalogue — a filterable list of
/// active (unsold, in-stock) barcode items for the mobile app.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CatalogueController : ControllerBase
{
    private readonly ICatalogueRepository _catalogueRepository;
    private readonly ILogger<CatalogueController> _logger;

    public CatalogueController(
        ICatalogueRepository catalogueRepository,
        ILogger<CatalogueController> logger)
    {
        _catalogueRepository = catalogueRepository;
        _logger              = logger;
    }

    /// <summary>
    /// Get the jewellery catalogue with optional filters.
    /// All query parameters are optional — omitting them returns all active items.
    /// </summary>
    /// <param name="itemName">Filter by item name (partial match).</param>
    /// <param name="barcode">Filter by exact barcode number.</param>
    /// <param name="size">Filter by exact size (e.g. "12").</param>
    /// <param name="fromWt">Minimum gross weight in grams.</param>
    /// <param name="toWt">Maximum gross weight in grams.</param>
    /// <param name="description">Filter by description (partial match).</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CatalogueItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(
        [FromQuery] string? itemName,
        [FromQuery] string? barcode,
        [FromQuery] string? size,
        [FromQuery] decimal? fromWt,
        [FromQuery] decimal? toWt,
        [FromQuery] string? description,
        CancellationToken cancellationToken)
    {
        var filter = new CatalogueFilter
        {
            ItemName    = itemName,
            Barcode     = barcode,
            Size        = size,
            FromWt      = fromWt,
            ToWt        = toWt,
            Description = description
        };

        _logger.LogInformation(
            "Catalogue requested — Filters: ItemName={ItemName}, Barcode={Barcode}, Size={Size}, FromWt={FromWt}, ToWt={ToWt}, Desc={Desc}",
            itemName, barcode, size, fromWt, toWt, description);

        var items = await _catalogueRepository.GetCatalogueAsync(filter, cancellationToken);

        _logger.LogInformation("Catalogue returned {Count} items", items.Count);

        return Ok(items);
    }

    /// <summary>
    /// Returns distinct available item names in stock.
    /// </summary>
    [HttpGet("itemnames")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItemNames(CancellationToken cancellationToken)
    {
        var itemNames = await _catalogueRepository.GetAvailableItemNamesAsync(cancellationToken);
        return Ok(itemNames);
    }

    /// <summary>
    /// Returns distinct available sizes in stock.
    /// </summary>
    [HttpGet("sizes")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSizes(CancellationToken cancellationToken)
    {
        var sizes = await _catalogueRepository.GetAvailableSizesAsync(cancellationToken);
        return Ok(sizes);
    }
}

