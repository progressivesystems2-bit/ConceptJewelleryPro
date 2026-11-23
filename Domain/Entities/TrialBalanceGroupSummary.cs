using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class TrialBalanceGroupSummary
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public string HeadCategory { get; set; } = string.Empty;
    public int AccountCount { get; set; }

    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }

    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }

    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }

    public decimal NetClosingBalance => ClosingDebit - ClosingCredit;

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
