namespace ConceptJewelleryPro.Domain.Entities;

public sealed class NonBarcodeStockReportFilter
{
    public string ItemHead { get; set; } = "*";
    public string SubGroup { get; set; } = "*";
    public string ItemName { get; set; } = "*";
    public string BoxNo { get; set; } = "*";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string SearchText { get; set; } = string.Empty;
}
