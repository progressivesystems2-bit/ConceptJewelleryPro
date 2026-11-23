namespace ConceptJewelleryPro.Domain.Entities;

public sealed class SalesVouType
{
    public string VouType { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string SalesAccount { get; set; } = string.Empty;
    public string VatAccount { get; set; } = string.Empty;
    public decimal CardCommission { get; set; }
    public string CardBank { get; set; } = string.Empty;
    public string CommAc { get; set; } = string.Empty;
    public decimal VatPercentage { get; set; }
    public string Note { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string HeadCategory { get; set; } = string.Empty;
    public bool Hide { get; set; }
    public string ItemHead { get; set; } = string.Empty;
    public string PrintFile { get; set; } = string.Empty;
    public string ItemPurchased { get; set; } = string.Empty;
    public string ItemConvertedTo { get; set; } = string.Empty;
    public string CounterNo { get; set; } = string.Empty;
}
