namespace ConceptJewelleryPro.Domain.Entities;

public class PaymentSubform
{
    public string VouNo { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string HeadCategory { get; set; } = string.Empty;

    public string BankName { get; set; } = string.Empty;

    public string CardType { get; set; } = string.Empty;

    public string ChequeNo { get; set; } = string.Empty;

    public string Narration { get; set; } = string.Empty;

    public string ItemType { get; set; } = string.Empty;

    public string ItemName { get; set; } = "OLD";

    public decimal Wt { get; set; }

    public decimal LessWt { get; set; }

    public decimal GrWt { get; set; }

    public decimal Alloy { get; set; } = 100m;

    public decimal NtWt { get; set; }

    public decimal Rate { get; set; }
}
