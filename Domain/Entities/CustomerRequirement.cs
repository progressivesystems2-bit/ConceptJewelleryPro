namespace ConceptJewelleryPro.Domain.Entities;

/// <summary>
/// Represents a customer's jewellery requirement logged via the mobile app.
/// Stored in the <c>CustomerRequirement</c> table of the Access database.
/// </summary>
public sealed class CustomerRequirement
{
    /// <summary>Auto-incremented primary key (0 for new records).</summary>
    public int Id { get; set; }

    /// <summary>Date and time the requirement was logged.</summary>
    public DateTime EntryDate { get; set; } = DateTime.Now;

    /// <summary>Category of the entry: "Order" or "Requirement".</summary>
    public string OrderCategory { get; set; } = "Requirement";

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;

    /// <summary>Type of item requested (e.g. Ring, Necklace, Earring).</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Detailed description of the requirement.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Size required (optional, e.g. "12" for a ring).</summary>
    public string? Size { get; set; }

    /// <summary>Approximate weight range required (optional, e.g. "10-15g").</summary>
    public string? WeightRange { get; set; }

    /// <summary>Purity required (optional, e.g. "22K", "18K").</summary>
    public string? Purity { get; set; }

    /// <summary>Any additional remarks from the salesman.</summary>
    public string? Remarks { get; set; }

    /// <summary>Username of the API caller (from JWT).</summary>
    public string EntryUser { get; set; } = string.Empty;

    /// <summary>Relative or stored file path of reference design photo / CAD sketch.</summary>
    public string? ImagePath { get; set; }
}
