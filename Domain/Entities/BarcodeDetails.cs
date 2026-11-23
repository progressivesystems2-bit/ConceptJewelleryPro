namespace ConceptJewelleryPro.Domain.Entities;

public sealed class BarcodeDetails
{
    public string Barcode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public int Qty { get; set; } = 1;
    public decimal GrossWt { get; set; }
    public decimal NetWt { get; set; }
    public decimal StoneWt { get; set; }
    public decimal StoneValue { get; set; }
    public decimal Making { get; set; }
    public decimal MPerGr { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Description2 { get; set; } = string.Empty;
    public decimal MRP { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public decimal CostRate { get; set; }
    public string BType { get; set; } = string.Empty;
}
