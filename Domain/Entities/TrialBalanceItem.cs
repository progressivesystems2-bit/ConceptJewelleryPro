using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class TrialBalanceItem
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public string AccountName { get; set; } = string.Empty;
    public string HeadCategory { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string PhoneNo { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }

    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }

    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }

    public decimal NetClosingBalance => ClosingDebit - ClosingCredit;

    public bool HasActivityOrBalance =>
        OpeningDebit > 0 || OpeningCredit > 0 ||
        PeriodDebit > 0 || PeriodCredit > 0 ||
        ClosingDebit > 0 || ClosingCredit > 0;

    public string FormattedOpeningDebit => OpeningDebit > 0 ? OpeningDebit.ToString("N2", IndianCulture) : "-";
    public string FormattedOpeningCredit => OpeningCredit > 0 ? OpeningCredit.ToString("N2", IndianCulture) : "-";

    public string FormattedPeriodDebit => PeriodDebit > 0 ? PeriodDebit.ToString("N2", IndianCulture) : "-";
    public string FormattedPeriodCredit => PeriodCredit > 0 ? PeriodCredit.ToString("N2", IndianCulture) : "-";

    public string FormattedClosingDebit => ClosingDebit > 0 ? ClosingDebit.ToString("N2", IndianCulture) : "-";
    public string FormattedClosingCredit => ClosingCredit > 0 ? ClosingCredit.ToString("N2", IndianCulture) : "-";

    public string FormattedNetClosing =>
        NetClosingBalance == 0 ? "0.00" :
        NetClosingBalance > 0 ? $"{NetClosingBalance.ToString("N2", IndianCulture)} Dr" :
        $"{Math.Abs(NetClosingBalance).ToString("N2", IndianCulture)} Cr";
}
