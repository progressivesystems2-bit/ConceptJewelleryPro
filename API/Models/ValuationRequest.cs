using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConceptJewelleryPro.API.Models;

/// <summary>
/// Request body for the Valuation API endpoint.
/// Provide either <see cref="MakingPerGram"/> or <see cref="MakingPercent"/> — not both.
/// </summary>
public sealed class ValuationRequest
{
    /// <summary>The barcode number to valuate.</summary>
    [Required]
    public string Barcode { get; set; } = string.Empty;

    /// <summary>
    /// Making charge per gram of net weight (in ₹).
    /// Takes priority over <see cref="MakingPercent"/> if both are provided.
    /// </summary>
    public decimal? MakingPerGram { get; set; }

    /// <summary>
    /// Making charge as a percentage of the metal value (0–100).
    /// Used only when <see cref="MakingPerGram"/> is null.
    /// </summary>
    public decimal? MakingPercent { get; set; }

    /// <summary>
    /// Raw GST mode string sent by mobile or web clients.
    /// Supports "Exclusive", "Inclusive", "ExclusiveGST", "InclusiveGST".
    /// </summary>
    [JsonPropertyName("gstMode")]
    public string? RawGstMode { get; set; }

    /// <summary>
    /// Resolved GST mode for calculation.
    /// Defaults to <c>ExclusiveGST</c>.
    /// </summary>
    [JsonIgnore]
    public GstMode GstMode
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RawGstMode)) return GstMode.ExclusiveGST;
            var clean = RawGstMode.Trim().ToLowerInvariant();
            if (clean.StartsWith("inc") || clean == "1")
                return GstMode.InclusiveGST;
            return GstMode.ExclusiveGST;
        }
    }
}

/// <summary>GST treatment for the valuation calculation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GstMode
{
    /// <summary>GST is added on top of the subtotal.</summary>
    ExclusiveGST,

    /// <summary>The subtotal already includes GST; back-calculated for display.</summary>
    InclusiveGST
}
