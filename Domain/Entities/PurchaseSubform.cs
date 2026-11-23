namespace ConceptJewelleryPro.Domain.Entities;

public class PurchaseSubform
{
    public int AutoNo { get; set; }

    public string VouNo { get; set; } = string.Empty;

    public int SlNo { get; set; } = 1;

    public string Barcode { get; set; } = string.Empty;

    public string OldBarcode { get; set; } = string.Empty;

    public bool PcRate { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string Purity { get; set; } = string.Empty;

    public string HSN { get; set; } = string.Empty;

    public string ENote { get; set; } = string.Empty;

    public decimal Qty { get; set; } = 1m;

    public decimal GrWt { get; set; }

    public decimal LessWt { get; set; }

    public decimal NtWt { get; set; }

    public decimal Rate { get; set; }

    public decimal Amount { get; set; }

    public decimal MetalValue { get; set; }

    public decimal Making { get; set; }

    public decimal MperGr { get; set; }

    public decimal StoneValue { get; set; }

    public decimal DisPer { get; set; }

    public decimal DisAmt { get; set; }

    public decimal GrossAmt { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal GstPer { get; set; } = 3.00m;

    public decimal GstAmt { get; set; }

    public decimal TotalAmt { get; set; }

    public bool IsDiscount { get; set; }
}
