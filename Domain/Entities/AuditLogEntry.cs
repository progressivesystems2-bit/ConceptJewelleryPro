namespace ConceptJewelleryPro.Domain.Entities;

public class AuditLogEntry
{
    public int IdNo { get; set; }
    public string User { get; set; } = string.Empty;
    public string EntryType { get; set; } = string.Empty;
    public string VouNo { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public DateTime Time { get; set; } = DateTime.Now;
    public bool Checked { get; set; }

    /// <summary>
    /// Combined Date and Time of the audit record.
    /// </summary>
    public DateTime Timestamp
    {
        get
        {
            var datePart = Date != DateTime.MinValue ? Date.Date : DateTime.Today;
            var timePart = Time != DateTime.MinValue ? Time.TimeOfDay : TimeSpan.Zero;
            return datePart.Add(timePart);
        }
    }

    /// <summary>
    /// Indicates whether this record represents a deletion.
    /// </summary>
    public bool IsDeleted => string.Equals(NewValue, "#Deleted#", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Indicates whether this record represents a modification.
    /// </summary>
    public bool IsModified => !IsDeleted;

    /// <summary>
    /// Action type display text ("DELETED" or "MODIFIED").
    /// </summary>
    public string ActionDisplay => IsDeleted ? "DELETED" : "MODIFIED";
}
