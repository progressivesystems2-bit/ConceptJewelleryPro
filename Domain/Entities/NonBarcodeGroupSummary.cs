namespace ConceptJewelleryPro.Domain.Entities;

public sealed class NonBarcodeGroupSummary
{
    public string GroupKey { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int ItemCount { get; set; }

    public decimal OpeningQty { get; set; }
    public decimal OpeningWt { get; set; }

    public decimal InwardQty { get; set; }
    public decimal InwardWt { get; set; }

    public decimal OutwardQty { get; set; }
    public decimal OutwardWt { get; set; }

    public decimal ClosingQty => OpeningQty + InwardQty - OutwardQty;
    public decimal ClosingWt => OpeningWt + InwardWt - OutwardWt;
}
