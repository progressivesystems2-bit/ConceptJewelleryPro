namespace ConceptJewelleryPro.Domain.Entities;

public sealed class CustomerLookupItem
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string GstinPan { get; set; } = string.Empty;
    public string VatType { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string SourceType { get; set; } = "Customer";

    public string DisplayText => string.IsNullOrWhiteSpace(Phone)
        ? Name
        : $"{Name} ({Phone})";
}
