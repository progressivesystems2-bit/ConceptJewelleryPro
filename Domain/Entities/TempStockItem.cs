namespace ConceptJewelleryPro.Domain.Entities;

public sealed class TempStockItem
{
    public string Barcode { get; set; } = string.Empty;
    public int? BarcodeAutoNo { get; set; }
    public int? Nos { get; set; }
    public string? VouNo { get; set; }
    public string? BType { get; set; }
    public string? ItemName { get; set; }
    public string? ItemCode { get; set; }
    public string? HeadGroup { get; set; }
    public string? Purity { get; set; }
    public string? Description { get; set; }
    public string? Description2 { get; set; }
    public string? Size { get; set; }
    public decimal Qty { get; set; } = 1;
    public decimal GrossWt { get; set; }
    public decimal StoneWt { get; set; }
    public decimal NetWt { get; set; }
    public decimal Making { get; set; }
    public string? MakingType { get; set; }
    public decimal MPerGr { get; set; }
    public decimal StoneValue { get; set; }
    public decimal CostTounch { get; set; }
    public decimal CostPg { get; set; }
    public decimal CostMaking { get; set; }
    public decimal CostPrice { get; set; }
    public decimal CostRate { get; set; }
    public decimal MRP { get; set; }
    public string? Supplier { get; set; }
    public string? SupplierName { get; set; }
    public string? SuppInvNo { get; set; }
    public DateTime? SuppDate { get; set; }
    public DateTime? PDate { get; set; }
    public DateTime? PunchedOn { get; set; }
    public string? Photo { get; set; }
    public string? Sti { get; set; }
    public string? Status { get; set; }
    public string? BoxNo { get; set; }
    public string? Godown { get; set; }

    public decimal DisplayWt => NetWt > 0 ? NetWt : GrossWt;
    public string DisplayText => $"{Barcode} - {ItemName} ({DisplayWt:N3}g)";
    public override string ToString() => Barcode;
}
