namespace ConceptJewelleryPro.Domain.Entities;

/// <summary>
/// Filter parameters for the Catalogue API.
/// All fields are optional — omitting a field means "no filter on that column".
/// </summary>
public sealed class CatalogueFilter
{
    /// <summary>Filter by item name (partial match, case-insensitive).</summary>
    public string? ItemName { get; set; }

    /// <summary>Filter by exact barcode number.</summary>
    public string? Barcode { get; set; }

    /// <summary>Filter by exact size value (e.g. "12", "16").</summary>
    public string? Size { get; set; }

    /// <summary>Minimum gross weight in grams (inclusive).</summary>
    public decimal? FromWt { get; set; }

    /// <summary>Maximum gross weight in grams (inclusive).</summary>
    public decimal? ToWt { get; set; }

    /// <summary>Filter by description (partial match, case-insensitive).</summary>
    public string? Description { get; set; }

    /// <summary>Whether to include sold/billed items (default: false).</summary>
    public bool IncludeSold { get; set; }
}
