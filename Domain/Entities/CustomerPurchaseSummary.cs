namespace ConceptJewelleryPro.Domain.Entities;

public sealed class CustomerPurchaseHistoryItem
{
    public DateTime Date { get; set; }
    public string VouNo { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Qty { get; set; } = 1;
    public decimal Amount { get; set; }
    public decimal NtWt { get; set; }
}

public sealed class CustomerPurchaseSummary
{
    public decimal TotalPurchaseValue { get; set; }
    public decimal AvgPurchaseValue { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public int TotalBillsCount { get; set; }
    public IReadOnlyList<CustomerPurchaseHistoryItem> Items { get; set; } = Array.Empty<CustomerPurchaseHistoryItem>();
}
