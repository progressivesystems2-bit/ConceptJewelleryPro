namespace ConceptJewelleryPro.API.Models;

public sealed class InvoiceResult
{
    public string? VendorName { get; set; }

    public string? VendorAddress { get; set; }

    public string? InvoiceNumber { get; set; }

    public string? InvoiceDate { get; set; }

    public string? DueDate { get; set; }

    public string? PurchaseOrderNumber { get; set; }

    public string? CustomerName { get; set; }

    public List<InvoiceLineItem> Items { get; set; } = new();

    public decimal? SubTotal { get; set; }

    public decimal? TotalTax { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? CurrencyCode { get; set; }

    public string? SourceFilePath { get; set; }
}

public sealed class InvoiceLineItem
{
    public string? Description { get; set; }

    public decimal? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal? Amount { get; set; }

    public string? Unit { get; set; }

    public decimal? Tax { get; set; }
}
