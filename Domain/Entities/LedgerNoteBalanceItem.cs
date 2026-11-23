using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class LedgerNoteBalanceItem
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public DateTime BillDate { get; set; } = DateTime.Today;

    public string VouNo { get; set; } = string.Empty;

    public string VouType { get; set; } = string.Empty;

    public decimal BillAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal PendingAmount { get; set; }

    public int OverdueDays { get; set; }

    public string Status { get; set; } = "Pending";

    public string Narration { get; set; } = string.Empty;

    public string FormattedDate => BillDate.ToString("dd/MM/yyyy");

    public string FormattedBillAmount => BillAmount.ToString("N2", IndianCulture);

    public string FormattedPaidAmount => PaidAmount.ToString("N2", IndianCulture);

    public string FormattedPendingAmount => PendingAmount.ToString("N2", IndianCulture);
}
