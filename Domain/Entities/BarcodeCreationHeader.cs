namespace ConceptJewelleryPro.Domain.Entities;

public class BarcodeCreationHeader
{
    public string VouNo { get; set; } = string.Empty;
    public int Nos { get; set; }
    public DateTime VouDate { get; set; } = DateTime.Today;
    public string ArtisianName { get; set; } = string.Empty; // Supplier
    public string SuppBillNo { get; set; } = string.Empty;
    public decimal GrossWts { get; set; }
    public decimal NetWts { get; set; }
    public decimal StoneWts { get; set; }
    public decimal StoneValues { get; set; }
    public decimal Qty { get; set; }
    public decimal MetalRate { get; set; }
    public decimal Makings { get; set; }
    public string Status { get; set; } = "Saved";
    public string ComputerName { get; set; } = Environment.MachineName;
    public string EntryUser { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; } = DateTime.Today;
    public DateTime EntryTime { get; set; } = DateTime.Now;
    public string? OldVouNo { get; set; }

    public List<BarcodeSubformItem> Items { get; set; } = [];

    public decimal TotalGrossWt => Items.Sum(i => i.GrossWt);
    public decimal TotalNetWt => Items.Sum(i => i.NetWt);
    public decimal TotalStoneWt => Items.Sum(i => i.StoneWt);
    public decimal TotalQty => Items.Sum(i => i.Qty);
    public decimal TotalStoneValue => Items.Sum(i => i.StoneValue);
    public decimal TotalMaking => Items.Sum(i => i.Making);
    public decimal TotalMRP => Items.Sum(i => i.MRP);
    public int TotalItemsCount => Items.Count;

    public string DisplayText => $"{VouNo} | {ArtisianName} | {VouDate:dd/MM/yyyy} ({Items.Count} items)";

    public override string ToString() => DisplayText;
}
