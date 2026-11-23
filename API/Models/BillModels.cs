using System.ComponentModel.DataAnnotations;

namespace ConceptJewelleryPro.API.Models;

public class VouTypeOptionDto
{
    public string VouType { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public decimal VatPercent { get; set; }
    public string HeadCategory { get; set; } = string.Empty;
}

public class NextVouNoResponse
{
    public string VouType { get; set; } = string.Empty;
    public string VouNo { get; set; } = string.Empty;
}

public class SaveBillItemRequest
{
    public string Barcode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public int Qty { get; set; } = 1;
    public decimal NtWt { get; set; }
    public decimal GrWt { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public decimal GrossAmt { get; set; }
    public decimal Making { get; set; }
    public decimal MperGr { get; set; }
    public decimal MetalValue { get; set; }
    public decimal StoneValue { get; set; }
    public decimal VatAmt { get; set; }
    public decimal DisAmt { get; set; }
    public decimal DisPer { get; set; }
    public string? ENote { get; set; }
    public string? SalesMan { get; set; }
}

public class SaveBillRequest
{
    [Required]
    public string VouType { get; set; } = string.Empty;

    public string? CustomerName { get; set; }
    public string? Phone { get; set; }
    public string? CustomerCode { get; set; }
    public string? Address { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? Pincode { get; set; }
    public string? Email { get; set; }
    public string? VatNo { get; set; }

    public string? PaymentMode { get; set; } = "Cash";
    public decimal DiscountAllowed { get; set; }
    public string? Remarks { get; set; }

    [MinLength(1, ErrorMessage = "At least one item is required to save the bill.")]
    public List<SaveBillItemRequest> Items { get; set; } = new();
}

public class SaveBillResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public string VouNo { get; set; } = string.Empty;
    public string VouType { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal GrossAmt { get; set; }
    public decimal DiscountAllowed { get; set; }
    public decimal TaxAmt { get; set; }
    public decimal NetAmt { get; set; }
    public DateTime BillDate { get; set; }
}
