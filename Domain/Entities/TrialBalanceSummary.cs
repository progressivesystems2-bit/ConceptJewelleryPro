using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class TrialBalanceSummary
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public decimal TotalOpeningDebit { get; set; }
    public decimal TotalOpeningCredit { get; set; }

    public decimal TotalPeriodDebit { get; set; }
    public decimal TotalPeriodCredit { get; set; }

    public decimal TotalClosingDebit { get; set; }
    public decimal TotalClosingCredit { get; set; }

    public decimal DifferenceClosing => TotalClosingDebit - TotalClosingCredit;
    public bool IsBalanced => Math.Abs(DifferenceClosing) < 0.01m;

    public int TotalAccountCount { get; set; }
    public int ActiveAccountCount { get; set; }

    public string FormattedTotalOpeningDebit => TotalOpeningDebit.ToString("N2", IndianCulture);
    public string FormattedTotalOpeningCredit => TotalOpeningCredit.ToString("N2", IndianCulture);

    public string FormattedTotalPeriodDebit => TotalPeriodDebit.ToString("N2", IndianCulture);
    public string FormattedTotalPeriodCredit => TotalPeriodCredit.ToString("N2", IndianCulture);

    public string FormattedTotalClosingDebit => TotalClosingDebit.ToString("N2", IndianCulture);
    public string FormattedTotalClosingCredit => TotalClosingCredit.ToString("N2", IndianCulture);

    public string FormattedDifference => Math.Abs(DifferenceClosing).ToString("N2", IndianCulture);
    public string BalancingStatusText => IsBalanced ? "Balanced (0.00)" : $"Difference: ₹ {FormattedDifference} {(DifferenceClosing > 0 ? "Dr" : "Cr")}";
}
