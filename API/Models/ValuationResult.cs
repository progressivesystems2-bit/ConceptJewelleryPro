namespace ConceptJewelleryPro.API.Models;

/// <summary>
/// Full price breakdown returned by the Valuation API.
/// All monetary values are in Indian Rupees (₹).
/// </summary>
public sealed class ValuationResult
{
    public string Barcode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;

    // ── Weight ─────────────────────────────────────────────────────────────
    /// <summary>Gross weight in grams.</summary>
    public decimal GrossWt { get; set; }

    /// <summary>Stone / less weight in grams.</summary>
    public decimal StoneWt { get; set; }

    /// <summary>Net metal weight in grams (GrossWt − StoneWt).</summary>
    public decimal NetWt { get; set; }

    // ── Pricing breakdown ───────────────────────────────────────────────────
    /// <summary>Current rate per gram for this purity from RateMaster.</summary>
    public decimal RatePerGram { get; set; }

    /// <summary>Metal value = NetWt × RatePerGram.</summary>
    public decimal MetalValue { get; set; }

    /// <summary>Value of stones / other deductions.</summary>
    public decimal StoneValue { get; set; }

    /// <summary>Making charge applied (computed from MakingPerGram or MakingPercent).</summary>
    public decimal MakingAmount { get; set; }

    /// <summary>SubTotal = MetalValue + StoneValue + MakingAmount.</summary>
    public decimal SubTotal { get; set; }

    // ── GST ─────────────────────────────────────────────────────────────────
    public string GstMode { get; set; } = string.Empty;

    /// <summary>GST rate applied (e.g. 3.00 for 3%).</summary>
    public decimal GstPercent { get; set; }

    /// <summary>GST amount (either added on top or back-calculated).</summary>
    public decimal GstAmount { get; set; }

    /// <summary>Final price after GST treatment.</summary>
    public decimal FinalPrice { get; set; }
}
