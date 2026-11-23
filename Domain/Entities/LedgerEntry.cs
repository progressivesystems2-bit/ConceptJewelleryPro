using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class LedgerEntry
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public string AccountName { get; set; } = string.Empty;
    public string HeadCategory { get; set; } = string.Empty;

    public DateTime VouDate { get; set; } = DateTime.Today;

    public string VouNo { get; set; } = string.Empty;

    public string VouType { get; set; } = string.Empty;

    public string Head { get; set; } = string.Empty;

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal Balance { get; set; }

    public string BalanceType { get; set; } = "Dr"; // "Dr" or "Cr"

    public string Narration { get; set; } = string.Empty;

    public string ChequeNo { get; set; } = string.Empty;

    public string ItemType { get; set; } = string.Empty;

    public string Purity { get; set; } = string.Empty;

    public decimal WeightDebit { get; set; }

    public decimal WeightCredit { get; set; }

    public decimal WeightBalance { get; set; }

    public string WeightBalanceType { get; set; } = "Dr";

    // Formatted Display Properties
    public string FormattedDate => VouDate.ToString("dd/MM/yyyy");

    public string FormattedDebit => Debit > 0 ? Debit.ToString("N2", IndianCulture) : string.Empty;

    public string FormattedCredit => Credit > 0 ? Credit.ToString("N2", IndianCulture) : string.Empty;

    public string FormattedBalance => Balance != 0 ? $"{Math.Abs(Balance).ToString("N2", IndianCulture)} {BalanceType}" : "0.00";

    public string FormattedWeightDebit => WeightDebit > 0 ? WeightDebit.ToString("N3", IndianCulture) : string.Empty;

    public string FormattedWeightCredit => WeightCredit > 0 ? WeightCredit.ToString("N3", IndianCulture) : string.Empty;

    public string FormattedWeightBalance => WeightBalance != 0 ? $"{Math.Abs(WeightBalance).ToString("N3", IndianCulture)} {WeightBalanceType}" : "0.000";
}
