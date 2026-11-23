namespace ConceptJewelleryPro.Domain.Entities;

public class RateStoredEntry
{
    public int AutoNo { get; set; }

    public string Purity { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;

    public DateTime Time { get; set; } = DateTime.Now;
}
