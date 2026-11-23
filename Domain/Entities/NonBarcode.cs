namespace ConceptJewelleryPro.Domain.Entities;

public class NonBarcode
{
    public string VouNo { get; set; } = string.Empty;
    public int Nos { get; set; }
    public DateTime VouDate { get; set; } = DateTime.Today;
    public string SupplierName { get; set; } = string.Empty;
    public string EntryUser { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; } = DateTime.Today;
    public DateTime EntryTime { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Active";
    public string ComputerName { get; set; } = Environment.MachineName;

    public List<NonBarcodeSubform> Lines { get; set; } = [];

    public decimal TotalQty => Lines.Sum(l => l.Qty);
    public decimal TotalWeight => Lines.Sum(l => l.Weight);
    public int TotalItems => Lines.Count;

    public string DisplayText => $"{VouNo} | {SupplierName} | {VouDate:dd/MM/yyyy}";

    public override string ToString() => DisplayText;
}
