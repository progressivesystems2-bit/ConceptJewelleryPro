namespace ConceptJewelleryPro.Domain.Entities;

public class PurchaseRegisterFilter
{
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public string? ItemType { get; set; }

    public string? VouType { get; set; }

    public string? SearchText { get; set; }
}
