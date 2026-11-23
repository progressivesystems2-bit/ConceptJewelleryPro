namespace ConceptJewelleryPro.Domain.Entities;

public class BarcodeGroupSummary
{
    public string GroupKey { get; set; } = string.Empty;
    public string HeadGroup { get; set; } = string.Empty;
    public string SubGroup { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalGrossWt { get; set; }
    public decimal TotalStoneWt { get; set; }
    public decimal TotalNetWt { get; set; }
    public decimal TotalStoneValue { get; set; }
    public decimal TotalMaking { get; set; }
    public decimal TotalAmount { get; set; }
}
