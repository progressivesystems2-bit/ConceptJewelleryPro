namespace ConceptJewelleryPro.Domain.Entities;

public class AuditGroupedEntry
{
    public string GroupKey { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string EntryType { get; set; } = string.Empty;
    public string VouNo { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsDeleted { get; set; }
    public string ActionDisplay => IsDeleted ? "DELETED" : "MODIFIED";
    public bool AllChecked { get; set; }
    public int ChangeCount => Changes.Count;
    public List<AuditLogEntry> Changes { get; set; } = new();

    public string SummaryText
    {
        get
        {
            if (IsDeleted)
            {
                return $"Record '{VouNo}' deleted with {ChangeCount} attributes.";
            }

            var fields = string.Join(", ", Changes.Select(c => c.FieldName).Distinct().Take(4));
            if (Changes.Select(c => c.FieldName).Distinct().Count() > 4)
            {
                fields += "...";
            }
            return $"Modified fields: {fields}";
        }
    }
}
