namespace ConceptJewelleryPro.Domain.Entities;

public sealed class NonBarcodeTransactionDetail
{
    public DateTime? Date { get; set; }
    public string VouNo { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty; // "Inward (Stock)", "Inward (Old Gold)", "Outward (Sale)"
    public string PartyName { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string HeadGroup { get; set; } = string.Empty;
    public string SubGroup { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;

    public decimal InQty { get; set; }
    public decimal InWt { get; set; }
    public decimal OutQty { get; set; }
    public decimal OutWt { get; set; }

    public decimal NetQty => InQty - OutQty;
    public decimal NetWt => InWt - OutWt;

    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string Narration { get; set; } = string.Empty;
    public string EntryUser { get; set; } = string.Empty;
}
