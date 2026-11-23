using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class VoucherAuditItem
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public string VouNo { get; set; } = string.Empty;
    public DateTime VouDate { get; set; } = DateTime.Today;
    public string VouType { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;

    public string DebitAccounts { get; set; } = string.Empty;
    public string CreditAccounts { get; set; } = string.Empty;

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    public decimal Difference => TotalDebit - TotalCredit;
    public bool IsBalanced => Math.Abs(Difference) < 0.0001m;

    public string FormattedVouDate => VouDate.ToString("dd/MM/yyyy");
    public string FormattedDebit => TotalDebit.ToString("N2", IndianCulture);
    public string FormattedCredit => TotalCredit.ToString("N2", IndianCulture);
    public string FormattedDifference => Math.Abs(Difference).ToString("N2", IndianCulture);

    public string StatusText
    {
        get
        {
            if (IsBalanced) return "Balanced";
            var diffVal = Math.Abs(Difference);
            var diffStr = diffVal.ToString(diffVal < 0.01m ? "N4" : "N2", IndianCulture);
            return $"Imbalance (₹{diffStr} {(Difference > 0 ? "Dr excess" : "Cr excess")})";
        }
    }

    public string Remarks { get; set; } = string.Empty;
}
