namespace ConceptJewelleryPro.Domain.Entities;

public class BarcodeStockReportFilter
{
    public string? ItemHead { get; set; }
    public string? SubGroup { get; set; }
    public string? Purity { get; set; }
    public string? ItemName { get; set; }
    public string? SupplierName { get; set; }
    public string? BoxNo { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchText { get; set; }
    public bool OnlyMismatches { get; set; }
}
