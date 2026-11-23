namespace ConceptJewelleryPro.Domain.Entities;

public sealed class NonBarcodeStockReportItem
{
    public string ItemName { get; set; } = string.Empty;
    public string HeadGroup { get; set; } = string.Empty;
    public string SubGroup { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;

    public decimal OpeningQty { get; set; }
    public decimal OpeningWt { get; set; }

    public decimal InwardQty { get; set; }
    public decimal InwardWt { get; set; }

    public decimal OutwardQty { get; set; }
    public decimal OutwardWt { get; set; }

    public decimal ClosingQty => OpeningQty + InwardQty - OutwardQty;
    public decimal ClosingWt => OpeningWt + InwardWt - OutwardWt;

    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}
