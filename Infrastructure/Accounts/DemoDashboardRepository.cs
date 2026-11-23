using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoDashboardRepository : IDashboardRepository
{
    public Task<DashboardSummary> GetDashboardSummaryAsync(
        DateTime targetDate,
        string currentUser,
        string role,
        CancellationToken cancellationToken = default)
    {
        var summary = new DashboardSummary
        {
            GlanceMetrics = new TodayGlanceMetrics
            {
                BillsCountToday = 14,
                BillsAmountToday = 1845600m,
                ActiveStaffCount = 6,
                BarcodesCreatedToday = 28,
                GoldGrossWeightSoldToday = 245.850m,
                SilverGrossWeightSoldToday = 1250.000m,
                ReceiptsCollectedToday = 350000m
            },
            PendingOrders = new List<PendingCustomerOrderItem>
            {
                new()
                {
                    OrderNo = "ORD-2026-089",
                    CustomerName = "Rajeshwari Sharma",
                    Phone = "9820154321",
                    ItemName = "Bridal Gold Choker Necklace Set",
                    Purity = "22K (916)",
                    Qty = 1,
                    AppWeight = 65.450m,
                    AppValue = 495000m,
                    AdvancePaid = 200000m,
                    OrderDate = DateTime.Today.AddDays(-12),
                    DeliveryDate = DateTime.Today,
                    KarigarName = "Ramesh Verma",
                    Status = "Ready for Hallmarking"
                },
                new()
                {
                    OrderNo = "ORD-2026-092",
                    CustomerName = "Anil Mehta",
                    Phone = "9819088776",
                    ItemName = "Diamond Solitaire Engagement Ring",
                    Purity = "18K Diamond",
                    Qty = 1,
                    AppWeight = 5.200m,
                    AppValue = 185000m,
                    AdvancePaid = 50000m,
                    OrderDate = DateTime.Today.AddDays(-6),
                    DeliveryDate = DateTime.Today,
                    KarigarName = "Suresh Master",
                    Status = "Final Polishing"
                },
                new()
                {
                    OrderNo = "ORD-2026-084",
                    CustomerName = "Pooja Hegde",
                    Phone = "9930477123",
                    ItemName = "Antique Temple Gold Bangles (Set of 4)",
                    Purity = "22K (916)",
                    Qty = 4,
                    AppWeight = 82.300m,
                    AppValue = 615000m,
                    AdvancePaid = 250000m,
                    OrderDate = DateTime.Today.AddDays(-15),
                    DeliveryDate = DateTime.Today.AddDays(-1),
                    KarigarName = "Gopal Karigar",
                    Status = "In Progress"
                },
                new()
                {
                    OrderNo = "ORD-2026-095",
                    CustomerName = "Kavita Deshmukh",
                    Phone = "9769344550",
                    ItemName = "Gold Mangalsutra 30 Inch Designer",
                    Purity = "22K (916)",
                    Qty = 1,
                    AppWeight = 24.500m,
                    AppValue = 185000m,
                    AdvancePaid = 50000m,
                    OrderDate = DateTime.Today.AddDays(-4),
                    DeliveryDate = DateTime.Today.AddDays(2),
                    KarigarName = "Ramesh Verma",
                    Status = "Casting"
                }
            },
            Celebrations = new List<CustomerCelebrationItem>
            {
                new()
                {
                    CustomerCode = "CUST-0042",
                    CustomerName = "Mrs. Sunita Singhania",
                    Phone = "9820011223",
                    Mobile = "9820011223",
                    City = "Mumbai",
                    EventType = "Birthday",
                    EventDate = DateTime.Today,
                    IsToday = true,
                    DaysRemaining = 0
                },
                new()
                {
                    CustomerCode = "CUST-0108",
                    CustomerName = "Mr. & Mrs. Vikramaditya Joshi",
                    Phone = "9819234567",
                    Mobile = "9819234567",
                    City = "Thane",
                    EventType = "Anniversary",
                    EventDate = DateTime.Today,
                    IsToday = true,
                    DaysRemaining = 0
                },
                new()
                {
                    CustomerCode = "CUST-0215",
                    CustomerName = "Priya Shah",
                    Phone = "9870654321",
                    Mobile = "9870654321",
                    City = "Navi Mumbai",
                    EventType = "Birthday",
                    EventDate = DateTime.Today.AddDays(2),
                    IsToday = false,
                    DaysRemaining = 2
                },
                new()
                {
                    CustomerCode = "CUST-0340",
                    CustomerName = "Dr. Mahesh Kulkarni",
                    Phone = "9988776655",
                    Mobile = "9988776655",
                    City = "Pune",
                    EventType = "Anniversary",
                    EventDate = DateTime.Today.AddDays(4),
                    IsToday = false,
                    DaysRemaining = 4
                }
            },
            SchemeDues = new List<SchemeDueSummaryItem>
            {
                new()
                {
                    Scode = "SCH-0012",
                    CustomerName = "Deepak Patel",
                    Phone = "9821432567",
                    InstallmentAmt = 5000m,
                    PaidCount = 4,
                    TotalMonths = 11,
                    NextDueDate = DateTime.Today.AddDays(-2),
                    DaysOffset = -2,
                    OverdueAmount = 5000m,
                    Staff = "Pooja"
                },
                new()
                {
                    Scode = "SCH-0045",
                    CustomerName = "Meenakshi Iyer",
                    Phone = "9833445566",
                    InstallmentAmt = 10000m,
                    PaidCount = 8,
                    TotalMonths = 11,
                    NextDueDate = DateTime.Today,
                    DaysOffset = 0,
                    OverdueAmount = 0m,
                    Staff = "Rohit"
                },
                new()
                {
                    Scode = "SCH-0067",
                    CustomerName = "Harshita Agarwal",
                    Phone = "9820556677",
                    InstallmentAmt = 7500m,
                    PaidCount = 2,
                    TotalMonths = 11,
                    NextDueDate = DateTime.Today.AddDays(3),
                    DaysOffset = 3,
                    OverdueAmount = 0m,
                    Staff = "Pooja"
                },
                new()
                {
                    Scode = "SCH-0089",
                    CustomerName = "Sanjay Bhansali",
                    Phone = "9811223344",
                    InstallmentAmt = 15000m,
                    PaidCount = 10,
                    TotalMonths = 11,
                    NextDueDate = DateTime.Today.AddDays(5),
                    DaysOffset = 5,
                    OverdueAmount = 0m,
                    Staff = "Pooja"
                }
            },
            SystemStatus = new SystemStatusInfo
            {
                LoggedUser = currentUser,
                UserRole = role,
                CounterName = "Counter-01 (Main Showroom)",
                MachineName = Environment.MachineName,
                IsSystemOnline = true,
                DatabaseType = "Microsoft Access (Primary Database)",
                DatabasePath = "RGRG2526.mdb",
                LastBackupTime = DateTime.Now.AddHours(-2).AddMinutes(-15),
                LastBackupFileName = "Backup_RGRG2526_20260826_152000.zip",
                LastBackupSizeBytes = 18450000,
                LastSyncTime = DateTime.Now,
                ResponseTimeMs = 1.2
            },
            MetalRates = new List<DashboardMetalRateItem>
            {
                new() { ItemHead = "Gold", PurityName = "Gold 24K (99.9% Pure)", CurrentRate = 7450m, PreviousRate = 7400m, UpdatedAt = DateTime.Today.AddHours(10) },
                new() { ItemHead = "Gold", PurityName = "Gold 22K (91.6% Hallmarked)", CurrentRate = 6825m, PreviousRate = 6780m, UpdatedAt = DateTime.Today.AddHours(10) },
                new() { ItemHead = "Gold", PurityName = "Gold 18K (75.0% Diamond)", CurrentRate = 5585m, PreviousRate = 5550m, UpdatedAt = DateTime.Today.AddHours(10) },
                new() { ItemHead = "Silver", PurityName = "Silver Fine (99.9%)", CurrentRate = 89.50m, PreviousRate = 88.00m, UpdatedAt = DateTime.Today.AddHours(10) }
            }
        };

        return Task.FromResult(summary);
    }

    public Task<TodayGlanceMetrics> GetTodayGlanceMetricsAsync(DateTime targetDate, CancellationToken cancellationToken = default)
        => Task.FromResult(new TodayGlanceMetrics
        {
            BillsCountToday = 14,
            BillsAmountToday = 1845600m,
            ActiveStaffCount = 6,
            BarcodesCreatedToday = 28,
            GoldGrossWeightSoldToday = 245.850m,
            SilverGrossWeightSoldToday = 1250.000m,
            ReceiptsCollectedToday = 350000m
        });

    public Task<IReadOnlyList<PendingCustomerOrderItem>> GetPendingOrdersAsync(DateTime targetDate, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PendingCustomerOrderItem>>(new List<PendingCustomerOrderItem>());

    public Task<IReadOnlyList<CustomerCelebrationItem>> GetCelebrationsAsync(DateTime targetDate, int lookaheadDays = 0, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CustomerCelebrationItem>>(new List<CustomerCelebrationItem>());

    public Task<IReadOnlyList<SchemeDueSummaryItem>> GetSchemeDuesWindowAsync(DateTime targetDate, int pastDays = 3, int nextDays = 7, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SchemeDueSummaryItem>>(new List<SchemeDueSummaryItem>());

    public Task<SystemStatusInfo> GetSystemStatusAsync(string currentUser, string role, CancellationToken cancellationToken = default)
        => Task.FromResult(new SystemStatusInfo { LoggedUser = currentUser, UserRole = role });

    public Task<IReadOnlyList<DashboardMetalRateItem>> GetMetalRatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DashboardMetalRateItem>>(new List<DashboardMetalRateItem>());
}
