using System.Globalization;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class LedgerAccountSummary
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public string Category { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public string PhoneNo { get; set; } = string.Empty;

    public string RefName { get; set; } = string.Empty;

    public decimal OpeningAmount { get; set; }

    public string OpeningType { get; set; } = "Dr";

    public decimal TotalDebits { get; set; }

    public decimal TotalCredits { get; set; }

    public decimal ClosingBalance { get; set; }

    public string ClosingType { get; set; } = "Dr";

    // Weight Ledger Summary
    public decimal OpeningGoldWeight { get; set; }

    public string OpeningWeightType { get; set; } = "Dr";

    public decimal TotalWeightIssued { get; set; }

    public decimal TotalWeightReceived { get; set; }

    public decimal ClosingGoldWeight { get; set; }

    public string ClosingWeightType { get; set; } = "Dr";

    // Formatted properties
    public string FormattedOpening => OpeningAmount != 0 ? $"{Math.Abs(OpeningAmount).ToString("N2", IndianCulture)} {OpeningType}" : "0.00";

    public string FormattedTotalDebits => TotalDebits.ToString("N2", IndianCulture);

    public string FormattedTotalCredits => TotalCredits.ToString("N2", IndianCulture);

    public string FormattedClosingBalance => ClosingBalance != 0 ? $"{Math.Abs(ClosingBalance).ToString("N2", IndianCulture)} {ClosingType}" : "0.00";

    public string FormattedOpeningWeight => OpeningGoldWeight != 0 ? $"{Math.Abs(OpeningGoldWeight).ToString("N3", IndianCulture)} {OpeningWeightType}" : "0.000";

    public string FormattedTotalWeightIssued => TotalWeightIssued.ToString("N3", IndianCulture);

    public string FormattedTotalWeightReceived => TotalWeightReceived.ToString("N3", IndianCulture);

    public string FormattedClosingWeight => ClosingGoldWeight != 0 ? $"{Math.Abs(ClosingGoldWeight).ToString("N3", IndianCulture)} {ClosingWeightType}" : "0.000";
}
