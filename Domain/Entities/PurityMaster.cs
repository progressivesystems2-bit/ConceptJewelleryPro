namespace ConceptJewelleryPro.Domain.Entities;

public class PurityMaster
{
    public string Purity { get; set; } = string.Empty;

    public decimal RatePerGram { get; set; }

    public string ItemHead { get; set; } = string.Empty;

    public string ItemType { get; set; } = string.Empty;

    public bool Hide { get; set; }

    public override string ToString() => Purity;
}
