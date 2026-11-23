namespace ConceptJewelleryPro.Domain.Entities;

public class Bill
{
    public string VouNo { get; set; } = string.Empty;

    public string VouType { get; set; } = "Sales Entry";

    public DateTime BillDate { get; set; } = DateTime.Today;

    public DateTime DueDate { get; set; } = DateTime.Today;

    public string Phone { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerCode { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Address2 { get; set; } = string.Empty;

    public string City { get; set; } = "CUTTACK";

    public string Pincode { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string VatNo { get; set; } = string.Empty;

    public string VatType { get; set; } = string.Empty;

    public bool Check { get; set; }

    public bool BillCanceled { get; set; }

    public decimal GrossAmt { get; set; }

    public decimal DiscountAllowed { get; set; }

    public decimal NetAmt { get; set; }

    public decimal ABeforeTax { get; set; }

    public decimal Tax { get; set; }

    public decimal TaxAmt { get; set; }

    public int Q1 { get; set; } // DESIGNS (1-5)

    public int Q2 { get; set; } // PRICING (1-5)

    public int Q3 { get; set; } // STAFF BEHAVIOUR (1-5)

    public int Q4 { get; set; }

    public int Q5 { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public string Narration { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string EntryUser { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; } = DateTime.Today;

    public DateTime EntryTime { get; set; } = DateTime.Now;

    public string ComputerName { get; set; } = string.Empty;

    public string Status { get; set; } = "Saved";

    public string AccountName { get; set; } = string.Empty;

    public string InOut { get; set; } = "Out";

    public decimal GrWt { get; set; }

    public decimal NtWt { get; set; }
}
