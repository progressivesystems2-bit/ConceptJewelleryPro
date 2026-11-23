namespace ConceptJewelleryPro.Domain.Entities;

public sealed class DateRangeCompanyInfo
{
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;
    public string TinRaw { get; set; } = string.Empty;
    public string StateCode { get; set; } = "21";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
