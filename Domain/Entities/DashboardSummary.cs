using System;
using System.Collections.Generic;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class TodayGlanceMetrics
{
    public int BillsCountToday { get; set; }
    public decimal BillsAmountToday { get; set; }
    public int ActiveStaffCount { get; set; }
    public int BarcodesCreatedToday { get; set; }
    public decimal GoldGrossWeightSoldToday { get; set; }
    public decimal SilverGrossWeightSoldToday { get; set; }
    public decimal ReceiptsCollectedToday { get; set; }
}

public sealed class PendingCustomerOrderItem
{
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public decimal Qty { get; set; } = 1;
    public decimal AppWeight { get; set; }
    public decimal AppValue { get; set; }
    public decimal AdvancePaid { get; set; }
    public decimal BalanceDue => Math.Max(0m, AppValue - AdvancePaid);
    public DateTime? OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string KarigarName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public bool IsOverdue => DeliveryDate.HasValue && DeliveryDate.Value.Date < DateTime.Today;
    public bool IsDueToday => DeliveryDate.HasValue && DeliveryDate.Value.Date == DateTime.Today;
    public int DaysUntilDue => DeliveryDate.HasValue ? (DeliveryDate.Value.Date - DateTime.Today).Days : 0;

    public string UrgencyLabel
    {
        get
        {
            if (IsOverdue) return $"Overdue by {Math.Abs(DaysUntilDue)}d";
            if (IsDueToday) return "Due Today";
            return $"Due in {DaysUntilDue}d";
        }
    }
}

public sealed class CustomerCelebrationItem
{
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string EventType { get; set; } = "Birthday"; // "Birthday" or "Anniversary"
    public DateTime EventDate { get; set; }
    public bool IsToday { get; set; }
    public int DaysRemaining { get; set; }

    public string BestContactNumber => !string.IsNullOrWhiteSpace(Mobile) ? Mobile : Phone;

    public string EventBadge => EventType == "Birthday" ? "🎂 Birthday" : "💍 Anniversary";

    public string GreetingMessage => EventType == "Birthday"
        ? $"Dear {CustomerName}, warm wishes on your Birthday from the entire team at Concept Jewellery! May your day be as sparkling and precious as gold. Wishing you joy, health, and prosperity!"
        : $"Dear {CustomerName}, happy Wedding Anniversary from all of us at Concept Jewellery! Wishing you and your spouse a lifetime of love, happiness, and togetherness!";
}

public sealed class SchemeDueSummaryItem
{
    public string Scode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal InstallmentAmt { get; set; }
    public int PaidCount { get; set; }
    public int TotalMonths { get; set; }
    public DateTime? NextDueDate { get; set; }
    public int DaysOffset { get; set; } // -3 to +7
    public decimal OverdueAmount { get; set; }
    public string Staff { get; set; } = string.Empty;

    public string Category
    {
        get
        {
            if (DaysOffset < 0) return "Overdue";
            if (DaysOffset == 0) return "Due Today";
            return "Upcoming";
        }
    }

    public string OffsetLabel
    {
        get
        {
            if (DaysOffset < 0) return $"{Math.Abs(DaysOffset)} days overdue";
            if (DaysOffset == 0) return "Due Today";
            return $"Due in {DaysOffset} days";
        }
    }
}

public sealed class SystemStatusInfo
{
    public string LoggedUser { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string CounterName { get; set; } = string.Empty;
    public string MachineName { get; set; } = Environment.MachineName;
    public bool IsSystemOnline { get; set; } = true;
    public string DatabaseType { get; set; } = "Microsoft Access";
    public string DatabasePath { get; set; } = string.Empty;
    public DateTime? LastBackupTime { get; set; }
    public string LastBackupFileName { get; set; } = string.Empty;
    public long LastBackupSizeBytes { get; set; }
    public DateTime LastSyncTime { get; set; } = DateTime.Now;
    public double ResponseTimeMs { get; set; }

    public string FormattedBackupSize
    {
        get
        {
            if (LastBackupSizeBytes <= 0) return "N/A";
            if (LastBackupSizeBytes < 1024 * 1024) return $"{LastBackupSizeBytes / 1024.0:F1} KB";
            return $"{LastBackupSizeBytes / (1024.0 * 1024.0):F2} MB";
        }
    }

    public string FormattedBackupTime => LastBackupTime.HasValue
        ? LastBackupTime.Value.ToString("dd MMM yyyy, hh:mm tt")
        : "No backup recorded";
}

public sealed class DashboardMetalRateItem
{
    public string ItemHead { get; set; } = string.Empty;
    public string PurityName { get; set; } = string.Empty;
    public decimal CurrentRate { get; set; }
    public decimal PreviousRate { get; set; }
    public string Unit { get; set; } = "/ 1g";
    public DateTime? UpdatedAt { get; set; }

    public decimal Change => CurrentRate - PreviousRate;
}

public sealed class DashboardSummary
{
    public TodayGlanceMetrics GlanceMetrics { get; set; } = new();
    public List<PendingCustomerOrderItem> PendingOrders { get; set; } = new();
    public List<CustomerCelebrationItem> Celebrations { get; set; } = new();
    public List<SchemeDueSummaryItem> SchemeDues { get; set; } = new();
    public SystemStatusInfo SystemStatus { get; set; } = new();
    public List<DashboardMetalRateItem> MetalRates { get; set; } = new();
}
