namespace ConceptJewelleryPro.Domain.Entities;

/// <summary>
/// Represents a single item in the jewellery catalogue (saleable barcode stock).
/// </summary>
public sealed class CatalogueItem
{
    public string Barcode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal GrossWt { get; set; }
    public decimal NetWt { get; set; }
    public decimal StoneWt { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string ImageUrl => !string.IsNullOrWhiteSpace(Barcode) ? $"/api/Barcode/image/{Barcode.Trim()}" : string.Empty;
    public bool HasImage => (!string.IsNullOrWhiteSpace(ImagePath) && System.IO.File.Exists(ImagePath)) || !string.IsNullOrWhiteSpace(Barcode);
    public bool IsSold { get; set; }
}
