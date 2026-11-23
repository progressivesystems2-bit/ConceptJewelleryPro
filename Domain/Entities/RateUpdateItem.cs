namespace ConceptJewelleryPro.Domain.Entities;

public class RateUpdateItem
{
    public string Purity { get; set; } = string.Empty;

    public decimal OldRate { get; set; }

    public decimal NewRate { get; set; }

    public string ItemHead { get; set; } = string.Empty;

    public string ItemType { get; set; } = string.Empty;
}
