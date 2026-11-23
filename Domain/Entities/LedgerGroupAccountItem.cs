namespace ConceptJewelleryPro.Domain.Entities;

public sealed class LedgerGroupAccountItem
{
    public string AccountName { get; set; } = string.Empty;
    public string HeadCategory { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string PhoneNo { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string RefName { get; set; } = string.Empty;

    // Currency Balances (₹)
    public decimal OpeningAmount { get; set; }
    public string OpeningType { get; set; } = "Dr";
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal ClosingBalance { get; set; }
    public string ClosingType { get; set; } = "Dr";

    // Weight Balances (Grams)
    public decimal OpeningWeight { get; set; }
    public string OpeningWeightType { get; set; } = "Dr";
    public decimal TotalWeightDebit { get; set; }
    public decimal TotalWeightCredit { get; set; }
    public decimal ClosingWeight { get; set; }
    public string ClosingWeightType { get; set; } = "Dr";

    public decimal NetSignedClosingAmount => ClosingType.Equals("Cr", StringComparison.OrdinalIgnoreCase) ? -ClosingBalance : ClosingBalance;
    public decimal NetSignedClosingWeight => ClosingWeightType.Equals("Cr", StringComparison.OrdinalIgnoreCase) ? -ClosingWeight : ClosingWeight;
}
