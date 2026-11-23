using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Application.Services;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Infrastructure.Settings;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessDashboardRepository : IDashboardRepository
{
    private readonly Func<string> _databasePathProvider;
    private readonly IDatabaseBackupService? _backupService;
    private readonly DatabaseSettingsStore? _settingsStore;

    public AccessDashboardRepository(
        Func<string> databasePathProvider,
        IDatabaseBackupService? backupService = null,
        DatabaseSettingsStore? settingsStore = null)
    {
        _databasePathProvider = databasePathProvider;
        _backupService = backupService;
        _settingsStore = settingsStore;
    }

    private string ConnectionString
    {
        get
        {
            var path = _databasePathProvider();
            return $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={path};Persist Security Info=False;";
        }
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(
        DateTime targetDate,
        string currentUser,
        string role,
        CancellationToken cancellationToken = default)
    {
        var glanceTask = GetTodayGlanceMetricsAsync(targetDate, cancellationToken);
        var ordersTask = GetPendingOrdersAsync(targetDate, cancellationToken);
        var celebrationsTask = GetCelebrationsAsync(targetDate, 7, cancellationToken);
        var schemesTask = GetSchemeDuesWindowAsync(targetDate, 3, 7, cancellationToken);
        var statusTask = GetSystemStatusAsync(currentUser, role, cancellationToken);
        var ratesTask = GetMetalRatesAsync(cancellationToken);

        await Task.WhenAll(glanceTask, ordersTask, celebrationsTask, schemesTask, statusTask, ratesTask);

        return new DashboardSummary
        {
            GlanceMetrics = await glanceTask,
            PendingOrders = (await ordersTask).ToList(),
            Celebrations = (await celebrationsTask).ToList(),
            SchemeDues = (await schemesTask).ToList(),
            SystemStatus = await statusTask,
            MetalRates = (await ratesTask).ToList()
        };
    }

    public async Task<TodayGlanceMetrics> GetTodayGlanceMetricsAsync(DateTime targetDate, CancellationToken cancellationToken = default)
    {
        var metrics = new TodayGlanceMetrics();
        var dateOnly = targetDate.Date;

        try
        {
            await using var connection = new OleDbConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // 1. Bills Count & Value Today
            try
            {
                const string billSql = @"
                    SELECT COUNT(*) AS BillCount,
                           SUM(IIF(GrandTotal IS NULL, 0, GrandTotal)) AS TotalAmt,
                           SUM(IIF(GrossWt IS NULL, 0, GrossWt)) AS TotalGrossWt
                    FROM Bill
                    WHERE [Date] >= @FromDate AND [Date] < @ToDate";

                await using var cmd = new OleDbCommand(billSql, connection);
                cmd.Parameters.Add("@FromDate", OleDbType.Date).Value = dateOnly;
                cmd.Parameters.Add("@ToDate", OleDbType.Date).Value = dateOnly.AddDays(1);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    metrics.BillsCountToday = reader["BillCount"] != DBNull.Value ? Convert.ToInt32(reader["BillCount"]) : 0;
                    metrics.BillsAmountToday = reader["TotalAmt"] != DBNull.Value ? Convert.ToDecimal(reader["TotalAmt"]) : 0m;
                    metrics.GoldGrossWeightSoldToday = reader["TotalGrossWt"] != DBNull.Value ? Convert.ToDecimal(reader["TotalGrossWt"]) : 0m;
                }
            }
            catch
            {
                // Fallback check
            }

            // 2. Active Staff Count
            try
            {
                const string staffSql = "SELECT COUNT(*) FROM Staff WHERE [Active] = True OR [Active] IS NULL";
                await using var staffCmd = new OleDbCommand(staffSql, connection);
                var staffCount = await staffCmd.ExecuteScalarAsync(cancellationToken);
                if (staffCount != null && staffCount != DBNull.Value)
                {
                    metrics.ActiveStaffCount = Convert.ToInt32(staffCount);
                }
            }
            catch
            {
                // Try accmast if Staff table does not exist
                try
                {
                    const string accStaffSql = "SELECT COUNT(*) FROM accmast WHERE (groupid = 'Staff' OR nature = 'Staff') AND (del = False OR del IS NULL)";
                    await using var accStaffCmd = new OleDbCommand(accStaffSql, connection);
                    var accCount = await accStaffCmd.ExecuteScalarAsync(cancellationToken);
                    if (accCount != null && accCount != DBNull.Value)
                    {
                        metrics.ActiveStaffCount = Convert.ToInt32(accCount);
                    }
                }
                catch { metrics.ActiveStaffCount = 1; }
            }

            // 3. Barcodes Created Today
            try
            {
                const string barcodeSql = @"
                    SELECT COUNT(*)
                    FROM BarcodeSubform
                    WHERE [EntryDate] >= @FromDate AND [EntryDate] < @ToDate";

                await using var barCmd = new OleDbCommand(barcodeSql, connection);
                barCmd.Parameters.Add("@FromDate", OleDbType.Date).Value = dateOnly;
                barCmd.Parameters.Add("@ToDate", OleDbType.Date).Value = dateOnly.AddDays(1);

                var barCount = await barCmd.ExecuteScalarAsync(cancellationToken);
                if (barCount != null && barCount != DBNull.Value)
                {
                    metrics.BarcodesCreatedToday = Convert.ToInt32(barCount);
                }
            }
            catch
            {
                // Fallback
            }

            // 4. Receipts Collected Today
            try
            {
                const string vouSql = @"
                    SELECT SUM(IIF(Amount IS NULL, 0, Amount))
                    FROM Vouchers
                    WHERE VouType = 'Receipt' AND [Date] >= @FromDate AND [Date] < @ToDate";

                await using var vouCmd = new OleDbCommand(vouSql, connection);
                vouCmd.Parameters.Add("@FromDate", OleDbType.Date).Value = dateOnly;
                vouCmd.Parameters.Add("@ToDate", OleDbType.Date).Value = dateOnly.AddDays(1);

                var sum = await vouCmd.ExecuteScalarAsync(cancellationToken);
                if (sum != null && sum != DBNull.Value)
                {
                    metrics.ReceiptsCollectedToday = Convert.ToDecimal(sum);
                }
            }
            catch
            {
                // Non-fatal
            }
        }
        catch
        {
            // Non-fatal
        }

        return metrics;
    }

    public async Task<IReadOnlyList<PendingCustomerOrderItem>> GetPendingOrdersAsync(DateTime targetDate, CancellationToken cancellationToken = default)
    {
        var list = new List<PendingCustomerOrderItem>();

        try
        {
            await using var connection = new OleDbConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT OrderNo, CustomerName, Phone, ItemName, Purity, Qty, AppWeight,
                       AppValue, AdvancePaid, OrderDate, DeliveryDate, KarigarName, Status, Closed
                FROM [Order]
                WHERE (Closed = False OR Closed IS NULL)
                  AND (Status IS NULL OR (Status <> 'Delivered' AND Status <> 'Cancelled'))
                ORDER BY DeliveryDate ASC, OrderDate DESC";

            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var item = new PendingCustomerOrderItem
                {
                    OrderNo = reader["OrderNo"]?.ToString()?.Trim() ?? string.Empty,
                    CustomerName = reader["CustomerName"]?.ToString()?.Trim() ?? string.Empty,
                    Phone = reader["Phone"]?.ToString()?.Trim() ?? string.Empty,
                    ItemName = reader["ItemName"]?.ToString()?.Trim() ?? string.Empty,
                    Purity = reader["Purity"]?.ToString()?.Trim() ?? string.Empty,
                    Qty = reader["Qty"] != DBNull.Value ? Convert.ToDecimal(reader["Qty"]) : 1m,
                    AppWeight = reader["AppWeight"] != DBNull.Value ? Convert.ToDecimal(reader["AppWeight"]) : 0m,
                    AppValue = reader["AppValue"] != DBNull.Value ? Convert.ToDecimal(reader["AppValue"]) : 0m,
                    AdvancePaid = reader["AdvancePaid"] != DBNull.Value ? Convert.ToDecimal(reader["AdvancePaid"]) : 0m,
                    KarigarName = reader["KarigarName"]?.ToString()?.Trim() ?? string.Empty,
                    Status = reader["Status"]?.ToString()?.Trim() ?? "Pending"
                };

                if (reader["OrderDate"] != DBNull.Value) item.OrderDate = Convert.ToDateTime(reader["OrderDate"]);
                if (reader["DeliveryDate"] != DBNull.Value) item.DeliveryDate = Convert.ToDateTime(reader["DeliveryDate"]);

                list.Add(item);
            }
        }
        catch
        {
            // Non-fatal
        }

        return list;
    }

    public async Task<IReadOnlyList<CustomerCelebrationItem>> GetCelebrationsAsync(
        DateTime targetDate,
        int lookaheadDays = 7,
        CancellationToken cancellationToken = default)
    {
        var result = new List<CustomerCelebrationItem>();
        var today = targetDate.Date;

        try
        {
            await using var connection = new OleDbConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT Code, Initial, CompanyName, Phone, Mobile, City, DOB, DOA
                FROM Customers
                WHERE DOB IS NOT NULL OR DOA IS NOT NULL";

            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader["Code"]?.ToString()?.Trim() ?? string.Empty;
                var initial = reader["Initial"]?.ToString()?.Trim() ?? string.Empty;
                var cName = reader["CompanyName"]?.ToString()?.Trim() ?? string.Empty;
                var fullName = string.IsNullOrWhiteSpace(initial) ? cName : $"{initial} {cName}";
                var phone = reader["Phone"]?.ToString()?.Trim() ?? string.Empty;
                var mobile = reader["Mobile"]?.ToString()?.Trim() ?? string.Empty;
                var city = reader["City"]?.ToString()?.Trim() ?? string.Empty;

                // Check Birthday
                if (reader["DOB"] != DBNull.Value && DateTime.TryParse(reader["DOB"].ToString(), out var dob))
                {
                    var thisYearBirthday = new DateTime(today.Year, dob.Month, Math.Min(dob.Day, DateTime.DaysInMonth(today.Year, dob.Month)));
                    var diffDays = (thisYearBirthday - today).Days;

                    // If birthday already passed this year, check next year's for lookahead
                    if (diffDays < 0 && Math.Abs(diffDays) > 300)
                    {
                        var nextYearBday = new DateTime(today.Year + 1, dob.Month, Math.Min(dob.Day, DateTime.DaysInMonth(today.Year + 1, dob.Month)));
                        diffDays = (nextYearBday - today).Days;
                    }

                    if (diffDays >= 0 && diffDays <= lookaheadDays)
                    {
                        result.Add(new CustomerCelebrationItem
                        {
                            CustomerCode = code,
                            CustomerName = fullName,
                            Phone = phone,
                            Mobile = mobile,
                            City = city,
                            EventType = "Birthday",
                            EventDate = dob,
                            IsToday = diffDays == 0,
                            DaysRemaining = diffDays
                        });
                    }
                }

                // Check Anniversary
                if (reader["DOA"] != DBNull.Value && DateTime.TryParse(reader["DOA"].ToString(), out var doa))
                {
                    var thisYearAnniv = new DateTime(today.Year, doa.Month, Math.Min(doa.Day, DateTime.DaysInMonth(today.Year, doa.Month)));
                    var diffDays = (thisYearAnniv - today).Days;

                    if (diffDays < 0 && Math.Abs(diffDays) > 300)
                    {
                        var nextYearAnniv = new DateTime(today.Year + 1, doa.Month, Math.Min(doa.Day, DateTime.DaysInMonth(today.Year + 1, doa.Month)));
                        diffDays = (nextYearAnniv - today).Days;
                    }

                    if (diffDays >= 0 && diffDays <= lookaheadDays)
                    {
                        result.Add(new CustomerCelebrationItem
                        {
                            CustomerCode = code,
                            CustomerName = fullName,
                            Phone = phone,
                            Mobile = mobile,
                            City = city,
                            EventType = "Anniversary",
                            EventDate = doa,
                            IsToday = diffDays == 0,
                            DaysRemaining = diffDays
                        });
                    }
                }
            }
        }
        catch
        {
            // Non-fatal
        }

        // Sort by IsToday first, then DaysRemaining ASC
        return result.OrderByDescending(c => c.IsToday).ThenBy(c => c.DaysRemaining).ToList();
    }

    public async Task<IReadOnlyList<SchemeDueSummaryItem>> GetSchemeDuesWindowAsync(
        DateTime targetDate,
        int pastDays = 3,
        int nextDays = 7,
        CancellationToken cancellationToken = default)
    {
        var result = new List<SchemeDueSummaryItem>();
        var today = targetDate.Date;

        try
        {
            var dueRepo = new AccessSchemeDueRepository(_databasePathProvider());
            var fullList = await dueRepo.GetSchemeDueListAsync(targetDate, cancellationToken);

            foreach (var item in fullList)
            {
                if (item.Closed || item.PaidCount >= item.TotalMonths || !item.NextDueDate.HasValue)
                {
                    continue;
                }

                var nextDue = item.NextDueDate.Value.Date;
                var daysDiff = (nextDue - today).Days;

                // Match in window: pastDays (e.g. -3 to 0) and nextDays (0 to 7)
                if (daysDiff >= -pastDays && daysDiff <= nextDays)
                {
                    result.Add(new SchemeDueSummaryItem
                    {
                        Scode = item.Scode,
                        CustomerName = item.CustomerName,
                        Phone = item.Phone,
                        InstallmentAmt = item.InstallmentAmt,
                        PaidCount = item.PaidCount,
                        TotalMonths = item.TotalMonths,
                        NextDueDate = item.NextDueDate,
                        DaysOffset = daysDiff,
                        OverdueAmount = item.OverdueAmount > 0 ? item.OverdueAmount : (daysDiff < 0 ? item.InstallmentAmt : 0m),
                        Staff = item.Staff
                    });
                }
            }
        }
        catch
        {
            // Non-fatal
        }

        return result.OrderBy(s => s.DaysOffset).ThenBy(s => s.CustomerName).ToList();
    }

    public async Task<SystemStatusInfo> GetSystemStatusAsync(string currentUser, string role, CancellationToken cancellationToken = default)
    {
        var dbPath = _databasePathProvider();
        var status = new SystemStatusInfo
        {
            LoggedUser = currentUser,
            UserRole = role,
            DatabasePath = dbPath,
            DatabaseType = dbPath.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase) || dbPath.EndsWith(".accdb", StringComparison.OrdinalIgnoreCase)
                ? "Microsoft Access" : "Database Server",
            CounterName = Environment.MachineName + "-C1"
        };

        // Test DB connection and measure latency
        var sw = Stopwatch.StartNew();
        try
        {
            await using var connection = new OleDbConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);
            sw.Stop();
            status.IsSystemOnline = true;
            status.ResponseTimeMs = sw.Elapsed.TotalMilliseconds;
        }
        catch
        {
            status.IsSystemOnline = false;
            status.ResponseTimeMs = 0;
        }

        // Get Last Backup metadata
        try
        {
            var targetBackupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
            if (_backupService != null)
            {
                var history = await _backupService.GetBackupHistoryAsync(targetBackupFolder, cancellationToken);
                if (history.Count > 0)
                {
                    var latest = history[0];
                    status.LastBackupTime = latest.CreatedAt;
                    status.LastBackupFileName = latest.FileName;
                    status.LastBackupSizeBytes = latest.FileSizeBytes;
                }
            }
            else if (Directory.Exists(targetBackupFolder))
            {
                var files = new DirectoryInfo(targetBackupFolder)
                    .GetFiles("*.zip")
                    .Concat(new DirectoryInfo(targetBackupFolder).GetFiles("*.mdb"))
                    .Concat(new DirectoryInfo(targetBackupFolder).GetFiles("*.bak"))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                if (files.Count > 0)
                {
                    var latest = files[0];
                    status.LastBackupTime = latest.LastWriteTime;
                    status.LastBackupFileName = latest.Name;
                    status.LastBackupSizeBytes = latest.Length;
                }
            }
        }
        catch
        {
            // Non-fatal
        }

        return status;
    }

    public async Task<IReadOnlyList<DashboardMetalRateItem>> GetMetalRatesAsync(CancellationToken cancellationToken = default)
    {
        var rates = new List<DashboardMetalRateItem>();

        try
        {
            await using var connection = new OleDbConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // 1. Fetch defined purities from [PURITY]
            try
            {
                const string sql = @"
                    SELECT [Purity], [Rate/Gr], [ItemHead], [ItemType], [Hide]
                    FROM [PURITY]
                    ORDER BY [Purity]";

                await using var cmd = new OleDbCommand(sql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var isHidden = reader["Hide"] is not DBNull && Convert.ToBoolean(reader["Hide"]);
                    if (isHidden) continue;

                    var purity = Convert.ToString(reader["Purity"])?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(purity)) continue;

                    var head = Convert.ToString(reader["ItemHead"])?.Trim();
                    var currentRate = reader["Rate/Gr"] is not DBNull ? Convert.ToDecimal(reader["Rate/Gr"]) : 0m;

                    rates.Add(new DashboardMetalRateItem
                    {
                        ItemHead = string.IsNullOrWhiteSpace(head) ? "Metal" : head,
                        PurityName = purity,
                        CurrentRate = currentRate,
                        PreviousRate = currentRate,
                        UpdatedAt = DateTime.Now
                    });
                }
            }
            catch
            {
                // Fallback query if [Hide] column does not exist
                const string fallbackSql = @"
                    SELECT [Purity], [Rate/Gr], [ItemHead]
                    FROM [PURITY]
                    ORDER BY [Purity]";

                await using var cmd = new OleDbCommand(fallbackSql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var purity = Convert.ToString(reader["Purity"])?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(purity)) continue;

                    var head = Convert.ToString(reader["ItemHead"])?.Trim();
                    var currentRate = reader["Rate/Gr"] is not DBNull ? Convert.ToDecimal(reader["Rate/Gr"]) : 0m;

                    rates.Add(new DashboardMetalRateItem
                    {
                        ItemHead = string.IsNullOrWhiteSpace(head) ? "Metal" : head,
                        PurityName = purity,
                        CurrentRate = currentRate,
                        PreviousRate = currentRate,
                        UpdatedAt = DateTime.Now
                    });
                }
            }

            // 2. Fetch latest rate history from [RateStored] if table exists
            if (rates.Count > 0)
            {
                try
                {
                    foreach (var rate in rates)
                    {
                        const string historySql = @"
                            SELECT TOP 2 [Rate], [Date], [Time]
                            FROM [RateStored]
                            WHERE [Purity] = ?
                            ORDER BY [Date] DESC, [AutoNo] DESC";

                        await using var histCmd = new OleDbCommand(historySql, connection);
                        histCmd.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = rate.PurityName;
                        await using var histReader = await histCmd.ExecuteReaderAsync(cancellationToken);

                        if (await histReader.ReadAsync(cancellationToken))
                        {
                            var latestRate = histReader["Rate"] is not DBNull ? Convert.ToDecimal(histReader["Rate"]) : rate.CurrentRate;
                            var d = histReader["Date"] is not DBNull ? Convert.ToDateTime(histReader["Date"]) : DateTime.Today;
                            rate.UpdatedAt = d;

                            if (await histReader.ReadAsync(cancellationToken))
                            {
                                rate.PreviousRate = histReader["Rate"] is not DBNull ? Convert.ToDecimal(histReader["Rate"]) : latestRate;
                            }
                        }
                    }
                }
                catch
                {
                    // [RateStored] is optional; non-fatal
                }
            }
        }
        catch
        {
            // Non-fatal
        }

        return rates;
    }
}
