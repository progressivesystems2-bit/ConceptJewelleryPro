using System.ComponentModel.DataAnnotations;

namespace ConceptJewelleryPro.API.Models;

/// <summary>
/// Request body for logging a customer jewellery requirement.
/// </summary>
public sealed class CustomerRequirementRequest
{
    /// <summary>Category of the entry: "Order" or "Requirement".</summary>
    [MaxLength(50)]
    public string OrderCategory { get; set; } = "Requirement";

    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? CustomerMobile { get; set; }

    [Required, MaxLength(150)]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Detailed description of what the customer wants.</summary>
    public string? Description { get; set; }

    /// <summary>Size required (e.g. ring size "12").</summary>
    [MaxLength(50)]
    public string? Size { get; set; }

    /// <summary>Approximate weight range (e.g. "10-15g").</summary>
    [MaxLength(50)]
    public string? WeightRange { get; set; }

    /// <summary>Purity required (e.g. "22K", "18K").</summary>
    [MaxLength(50)]
    public string? Purity { get; set; }

    /// <summary>Additional notes from the salesman.</summary>
    public string? Remarks { get; set; }

    /// <summary>Uploaded image filename or relative URL of design photo.</summary>
    public string? ImagePath { get; set; }
}
