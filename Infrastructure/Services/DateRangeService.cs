using System.Data.Common;
using System.Data.OleDb;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Settings;

namespace ConceptJewelleryPro.Infrastructure.Services;

public static class DateRangeService
{
    private static readonly object _lock = new();
    private static DateRangeCompanyInfo? _cachedInfo;
    private static string? _explicitDatabasePath;

    public static void SetDatabasePath(string? databasePath)
    {
        lock (_lock)
        {
            if (!string.Equals(_explicitDatabasePath, databasePath, StringComparison.OrdinalIgnoreCase))
            {
                _explicitDatabasePath = databasePath;
                _cachedInfo = null; // Invalidate cache when database changes
            }
        }
    }

    public static void InvalidateCache()
    {
        lock (_lock)
        {
            _cachedInfo = null;
        }
    }

    public static string ExtractGstin(string? tinValue)
    {
        if (string.IsNullOrWhiteSpace(tinValue)) return string.Empty;
        var trimmed = tinValue.Trim();

        // Check for standard 15-character GSTIN pattern
        var match = Regex.Match(trimmed, @"[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Value.ToUpperInvariant();
        }

        // Clean common prefixes
        var cleaned = Regex.Replace(trimmed, @"^(TIN\s*:\s*|GSTIN\s*:\s*|TIN\s+|GSTIN\s+)+", "", RegexOptions.IgnoreCase).Trim();
        return cleaned;
    }

    public static string ResolveDatabasePath()
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(_explicitDatabasePath) && File.Exists(_explicitDatabasePath))
            {
                return _explicitDatabasePath;
            }
        }

        try
        {
            var store = new DatabaseSettingsStore(DatabaseSettingsStore.DefaultFilePath);
            var saved = store.Load();
            if (!string.IsNullOrWhiteSpace(saved.AccessDatabasePath) && File.Exists(saved.AccessDatabasePath))
            {
                return saved.AccessDatabasePath;
            }
        }
        catch
        {
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "RGRG2526.mdb");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Also check for other .mdb files in the directory
            var mdbFiles = directory.GetFiles("*.mdb");
            if (mdbFiles.Length > 0)
            {
                return mdbFiles[0].FullName;
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }

    public static DateRangeCompanyInfo GetCompanyDetails(bool forceRefresh = false)
    {
        lock (_lock)
        {
            if (!forceRefresh && _cachedInfo != null)
            {
                return _cachedInfo;
            }
        }

        var dbPath = ResolveDatabasePath();
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            return new DateRangeCompanyInfo();
        }

        var connStr = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath};Persist Security Info=False;";
        var info = LoadFromConnectionString(connStr);

        lock (_lock)
        {
            _cachedInfo = info;
        }

        return info;
    }

    public static async Task<DateRangeCompanyInfo> GetCompanyDetailsAsync(CancellationToken cancellationToken = default, bool forceRefresh = false)
    {
        lock (_lock)
        {
            if (!forceRefresh && _cachedInfo != null)
            {
                return _cachedInfo;
            }
        }

        var dbPath = ResolveDatabasePath();
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            return new DateRangeCompanyInfo();
        }

        var connStr = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath};Persist Security Info=False;";
        var info = await LoadFromConnectionStringAsync(connStr, cancellationToken);

        lock (_lock)
        {
            _cachedInfo = info;
        }

        return info;
    }

    public static DateRangeCompanyInfo LoadFromConnectionString(string connectionString)
    {
        var info = new DateRangeCompanyInfo();
        try
        {
            using var connection = new OleDbConnection(connectionString);
            connection.Open();

            const string sql = "SELECT TOP 1 [Company], [Address], [Address2], [City], [Phone], [TIN], [FromDate], [ToDate] FROM [DateRange]";
            using var command = new OleDbCommand(sql, connection);
            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                PopulateInfo(info, reader);
            }
        }
        catch
        {
            // Table or column might have alternative structure, fallback to select *
            try
            {
                using var connection = new OleDbConnection(connectionString);
                connection.Open();
                using var command = new OleDbCommand("SELECT TOP 1 * FROM [DateRange]", connection);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    PopulateInfo(info, reader);
                }
            }
            catch
            {
                // DateRange table might not exist
            }
        }

        return info;
    }

    public static async Task<DateRangeCompanyInfo> LoadFromConnectionStringAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var info = new DateRangeCompanyInfo();
        try
        {
            await using var connection = new OleDbConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT TOP 1 [Company], [Address], [Address2], [City], [Phone], [TIN], [FromDate], [ToDate] FROM [DateRange]";
            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                PopulateInfo(info, reader);
            }
        }
        catch
        {
            try
            {
                await using var connection = new OleDbConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new OleDbCommand("SELECT TOP 1 * FROM [DateRange]", connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    PopulateInfo(info, reader);
                }
            }
            catch
            {
            }
        }

        return info;
    }

    private static void PopulateInfo(DateRangeCompanyInfo info, DbDataReader reader)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            cols.Add(reader.GetName(i));
        }

        string Get(string col) => cols.Contains(col) && reader[col] != DBNull.Value
            ? reader[col]?.ToString()?.Trim() ?? string.Empty
            : string.Empty;

        info.CompanyName = Get("Company");
        info.Address = Get("Address");
        info.Address2 = Get("Address2");
        info.City = Get("City");
        info.Phone = Get("Phone");
        info.TinRaw = Get("TIN");
        info.Gstin = ExtractGstin(info.TinRaw);

        if (!string.IsNullOrWhiteSpace(info.Gstin) && info.Gstin.Length >= 2 && char.IsDigit(info.Gstin[0]) && char.IsDigit(info.Gstin[1]))
        {
            info.StateCode = info.Gstin.Substring(0, 2);
        }

        // Address fallback: if Address is empty, fallback to City
        if (string.IsNullOrWhiteSpace(info.Address) && !string.IsNullOrWhiteSpace(info.City))
        {
            info.Address = info.City;
        }

        if (cols.Contains("FromDate") && reader["FromDate"] != DBNull.Value && DateTime.TryParse(reader["FromDate"]?.ToString(), out var fd))
        {
            info.FromDate = fd;
        }

        if (cols.Contains("ToDate") && reader["ToDate"] != DBNull.Value && DateTime.TryParse(reader["ToDate"]?.ToString(), out var td))
        {
            info.ToDate = td;
        }
    }
}
