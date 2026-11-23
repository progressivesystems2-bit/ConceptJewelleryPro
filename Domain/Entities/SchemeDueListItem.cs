namespace ConceptJewelleryPro.Domain.Entities;

public sealed class SchemeDueListItem
{
    public string Scode { get; set; } = string.Empty;
    public int Nos { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Staff { get; set; } = string.Empty;

    public DateTime SDate { get; set; }
    public DateTime EDate { get; set; }
    public decimal InstallmentAmt { get; set; }
    public int TotalMonths { get; set; }
    public decimal TotalValue { get; set; }
    public bool Closed { get; set; }
    public string Status { get; set; } = "Active";

    public int PaidCount { get; set; }
    public decimal TotalPaidAmt { get; set; }
    public DateTime? LastPaymentDate { get; set; }

    public int ExpectedCount { get; set; }
    public int OverdueCount { get; set; }
    public decimal OverdueAmount { get; set; }

    public DateTime? NextDueDate { get; set; }
    public int DaysOverdue { get; set; }
    public int DaysRemaining { get; set; }

    public string DueStatus
    {
        get
        {
            if (Closed || Status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                return "Closed";

            if (PaidCount >= TotalMonths)
                return "Completed";

            if (OverdueCount > 0)
                return "Overdue";

            if (NextDueDate.HasValue && NextDueDate.Value <= DateTime.Today.AddDays(7))
                return "Due Soon";

            return "On Track";
        }
    }
}
