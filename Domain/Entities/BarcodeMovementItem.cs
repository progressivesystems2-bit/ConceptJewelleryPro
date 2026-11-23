namespace ConceptJewelleryPro.Domain.Entities;

public class BarcodeMovementItem
{
    public DateTime? Date { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty; // "Inward (Creation)", "Outward (Sale)", "Conversion", "Sales Return"
    public string ReferenceVouNo { get; set; } = string.Empty;
    public string PartyOrSupplier { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal GrossWt { get; set; }
    public decimal NetWt { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string Narration { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
}
