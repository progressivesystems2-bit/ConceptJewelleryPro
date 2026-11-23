namespace ConceptJewelleryPro.Domain.Entities;

public class BillSubform
{
    public string VouNo { get; set; } = string.Empty;

    public int AutoNo { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string OldBarcode { get; set; } = string.Empty;

    public bool PcRate { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string ENote { get; set; } = string.Empty;

    public int Qty { get; set; } = 1;

    public decimal NtWt { get; set; }

    public decimal Rate { get; set; }

    public decimal Amount { get; set; }

    public decimal GrossAmt { get; set; }

    public decimal DisPer { get; set; }

    public decimal DisAmt { get; set; }

    public decimal GrWt { get; set; }

    public decimal MperGr { get; set; }

    public decimal Making { get; set; }

    public decimal MetalValue { get; set; }

    public string SalesMan { get; set; } = string.Empty;

    public decimal Taxable { get; set; }

    public decimal VatPercent { get; set; }

    public decimal VatAmt { get; set; }

    public decimal StoneValue { get; set; }

    public string Purity { get; set; } = string.Empty;

    public bool IsDiscount { get; set; }
}
