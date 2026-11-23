namespace ConceptJewelleryPro.Domain.Entities;

public class Item
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string SubGroup { get; set; } = string.Empty;
    public string HeadGroup { get; set; } = string.Empty;
    public decimal Tax { get; set; }
    public decimal Disper { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string MetalType { get; set; } = string.Empty;
    public decimal Purity { get; set; }
    public string RawPurity { get; set; } = string.Empty;
    public decimal StandardWeight { get; set; }
    public decimal MinWeight { get; set; }
    public decimal MaxWeight { get; set; }
    public decimal MRP { get; set; }
    public decimal SaleRate { get; set; }
    public decimal PurchaseRate { get; set; }
    public decimal OpeningStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public string HSNCode { get; set; } = string.Empty;
    public decimal GSTRate { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool Allowed { get; set; } = true;
    public string Status { get; set; } = "Saved";
    public string Saved { get; set; } = "Yes";
    public string User { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan Time { get; set; }

    public string PurityDisplay => !string.IsNullOrWhiteSpace(RawPurity)
        ? RawPurity
        : (Purity > 0 ? Purity.ToString("N2") : string.Empty);
}