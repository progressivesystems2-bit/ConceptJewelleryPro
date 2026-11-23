using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessItemRepository : IItemRepository
{
    private readonly string _connectionString;
    private HashSet<string>? _cachedColumns;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    public AccessItemRepository(string databasePath)
    {
        _connectionString = AccessConnectionProvider.GetConnectionString(databasePath);
    }

    private async Task<HashSet<string>> GetColumnsAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        if (_cachedColumns != null) return _cachedColumns;
        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedColumns != null) return _cachedColumns;
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var cmd = new OleDbCommand("SELECT TOP 1 * FROM [ItemCreation]", connection);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly, cancellationToken);
            var schema = reader.GetSchemaTable();
            if (schema != null)
            {
                foreach (DataRow row in schema.Rows)
                {
                    var colName = row["ColumnName"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(colName))
                    {
                        cols.Add(colName);
                    }
                }
            }
            _cachedColumns = cols;
            return cols;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    public async Task AddAsync(Item item, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var columns = await GetColumnsAsync(connection, cancellationToken);

        var insertColumns = new List<string>();
        var valuePlaceholders = new List<string>();
        var parameters = new List<(string Name, OleDbType Type, int Size, object Value)>();

        void TryAdd(string columnName, OleDbType type, int size, object? value)
        {
            if (columns.Contains(columnName))
            {
                insertColumns.Add($"[{columnName}]");
                valuePlaceholders.Add("?");
                parameters.Add((columnName, type, size, value ?? DBNull.Value));
            }
        }

        TryAdd("ItemCode", OleDbType.VarWChar, 50, Truncate(item.ItemCode, 50));
        TryAdd("ItemName", OleDbType.VarWChar, 50, Truncate(item.ItemName, 50));
        TryAdd("PrintName", OleDbType.VarWChar, 50, Truncate(!string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName : string.Empty, 50));

        var headGroup = !string.IsNullOrWhiteSpace(item.HeadGroup) ? item.HeadGroup : item.Group;
        var group = !string.IsNullOrWhiteSpace(item.Group) ? item.Group : item.HeadGroup;
        TryAdd("HeadGroup", OleDbType.VarWChar, 50, Truncate(headGroup, 50));
        TryAdd("Group", OleDbType.VarWChar, 100, Truncate(group, 100));

        var metalType = !string.IsNullOrWhiteSpace(item.MetalType) ? item.MetalType : headGroup;
        TryAdd("Section", OleDbType.VarWChar, 50, Truncate(metalType, 50));
        TryAdd("MetalType", OleDbType.VarWChar, 50, Truncate(metalType, 50));

        TryAdd("SubGroup", OleDbType.VarWChar, 50, Truncate(item.SubGroup, 50));
        TryAdd("UNIT", OleDbType.VarWChar, 50, Truncate(item.Unit, 50));
        if (!columns.Contains("UNIT"))
        {
            TryAdd("Unit", OleDbType.VarWChar, 20, Truncate(item.Unit, 20));
        }

        var purityStr = !string.IsNullOrWhiteSpace(item.RawPurity)
            ? item.RawPurity
            : (item.Purity > 0 ? item.Purity.ToString("0.##") : string.Empty);
        TryAdd("Purity", OleDbType.VarWChar, 50, Truncate(purityStr, 50));

        var taxVal = item.Tax > 0 ? item.Tax : (item.GSTRate > 0 ? item.GSTRate : 3.0m);
        TryAdd("Tax", OleDbType.Currency, 0, taxVal);
        TryAdd("Tax2", OleDbType.Numeric, 0, 0m);
        TryAdd("GSTRate", OleDbType.Decimal, 0, taxVal);
        TryAdd("Disper", OleDbType.Currency, 0, item.Disper);
        TryAdd("Opening", OleDbType.Currency, 0, item.OpeningStock);
        TryAdd("OpeningStock", OleDbType.Decimal, 0, item.OpeningStock);

        var nos = int.TryParse(item.ItemCode, out var n) ? n : 0;
        TryAdd("Nos", OleDbType.Integer, 0, nos);
        var isPcs = string.Equals(item.Unit, "PCS", StringComparison.OrdinalIgnoreCase);
        TryAdd("PC", OleDbType.Boolean, 0, isPcs);
        TryAdd("Rec", OleDbType.Boolean, 0, false);
        TryAdd("Allowed", OleDbType.Boolean, 0, item.Allowed);
        if (!columns.Contains("Allowed"))
        {
            TryAdd("Allow", OleDbType.Boolean, 0, item.Allowed);
        }
        TryAdd("Status", OleDbType.VarWChar, 15, Truncate(!string.IsNullOrWhiteSpace(item.Status) ? item.Status : "SAVED", 15));
        TryAdd("ComputerName", OleDbType.VarWChar, 50, Truncate(Environment.MachineName, 50));
        TryAdd("StaffCommisionPC", OleDbType.Currency, 0, 0m);
        TryAdd("StaffCommisionPercent", OleDbType.Currency, 0, 0m);

        var user = !string.IsNullOrWhiteSpace(item.User) ? item.User : "admin";
        TryAdd("EntryUser", OleDbType.VarWChar, 50, Truncate(user, 50));
        TryAdd("User", OleDbType.VarWChar, 50, Truncate(user, 50));

        var entryDate = item.Date != default ? item.Date : DateTime.Today;
        TryAdd("EntryDate", OleDbType.Date, 0, entryDate);
        TryAdd("Date", OleDbType.Date, 0, entryDate);

        var entryTime = new DateTime(1899, 12, 30).Add(item.Time != default ? item.Time : DateTime.Now.TimeOfDay);
        TryAdd("EntryTime", OleDbType.Date, 0, entryTime);
        TryAdd("Time", OleDbType.Date, 0, entryTime);

        TryAdd("HSNCODE", OleDbType.VarWChar, 50, Truncate(item.HSNCode, 50));
        if (!columns.Contains("HSNCODE"))
        {
            TryAdd("HSNCode", OleDbType.VarWChar, 20, Truncate(item.HSNCode, 20));
        }

        TryAdd("StandardWeight", OleDbType.Decimal, 0, item.StandardWeight);
        TryAdd("MinWeight", OleDbType.Decimal, 0, item.MinWeight);
        TryAdd("MaxWeight", OleDbType.Decimal, 0, item.MaxWeight);
        TryAdd("MRP", OleDbType.Decimal, 0, item.MRP);
        TryAdd("SaleRate", OleDbType.Decimal, 0, item.SaleRate);
        TryAdd("PurchaseRate", OleDbType.Decimal, 0, item.PurchaseRate);
        TryAdd("ReorderLevel", OleDbType.Decimal, 0, item.ReorderLevel);
        TryAdd("Description", OleDbType.VarWChar, 500, Truncate(item.Description, 500));
        TryAdd("IsActive", OleDbType.Boolean, 0, item.IsActive);
        TryAdd("Saved", OleDbType.VarWChar, 10, Truncate(item.Saved ?? "Yes", 10));

        var sql = $"INSERT INTO [ItemCreation] ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", valuePlaceholders)})";

        await using var command = new OleDbCommand(sql, connection);
        foreach (var p in parameters)
        {
            if (p.Size > 0)
            {
                command.Parameters.Add(p.Name, p.Type, p.Size).Value = p.Value;
            }
            else
            {
                command.Parameters.Add(p.Name, p.Type).Value = p.Value;
            }
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Item item, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var columns = await GetColumnsAsync(connection, cancellationToken);

        var setClauses = new List<string>();
        var parameters = new List<(string Name, OleDbType Type, int Size, object Value)>();

        void TrySet(string columnName, OleDbType type, int size, object? value)
        {
            if (columns.Contains(columnName))
            {
                setClauses.Add($"[{columnName}] = ?");
                parameters.Add((columnName, type, size, value ?? DBNull.Value));
            }
        }

        TrySet("ItemName", OleDbType.VarWChar, 50, Truncate(item.ItemName, 50));
        TrySet("PrintName", OleDbType.VarWChar, 50, Truncate(!string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName : string.Empty, 50));

        var headGroup = !string.IsNullOrWhiteSpace(item.HeadGroup) ? item.HeadGroup : item.Group;
        var group = !string.IsNullOrWhiteSpace(item.Group) ? item.Group : item.HeadGroup;
        TrySet("HeadGroup", OleDbType.VarWChar, 50, Truncate(headGroup, 50));
        TrySet("Group", OleDbType.VarWChar, 100, Truncate(group, 100));

        var metalType = !string.IsNullOrWhiteSpace(item.MetalType) ? item.MetalType : headGroup;
        TrySet("Section", OleDbType.VarWChar, 50, Truncate(metalType, 50));
        TrySet("MetalType", OleDbType.VarWChar, 50, Truncate(metalType, 50));

        TrySet("SubGroup", OleDbType.VarWChar, 50, Truncate(item.SubGroup, 50));
        TrySet("UNIT", OleDbType.VarWChar, 50, Truncate(item.Unit, 50));
        if (!columns.Contains("UNIT"))
        {
            TrySet("Unit", OleDbType.VarWChar, 20, Truncate(item.Unit, 20));
        }

        var purityStr = !string.IsNullOrWhiteSpace(item.RawPurity)
            ? item.RawPurity
            : (item.Purity > 0 ? item.Purity.ToString("0.##") : string.Empty);
        TrySet("Purity", OleDbType.VarWChar, 50, Truncate(purityStr, 50));

        var taxVal = item.Tax > 0 ? item.Tax : (item.GSTRate > 0 ? item.GSTRate : 3.0m);
        TrySet("Tax", OleDbType.Currency, 0, taxVal);
        TrySet("Tax2", OleDbType.Numeric, 0, 0m);
        TrySet("GSTRate", OleDbType.Decimal, 0, taxVal);
        TrySet("Disper", OleDbType.Currency, 0, item.Disper);
        TrySet("Opening", OleDbType.Currency, 0, item.OpeningStock);
        TrySet("OpeningStock", OleDbType.Decimal, 0, item.OpeningStock);

        var isPcs = string.Equals(item.Unit, "PCS", StringComparison.OrdinalIgnoreCase);
        TrySet("PC", OleDbType.Boolean, 0, isPcs);
        TrySet("Allowed", OleDbType.Boolean, 0, item.Allowed);
        if (!columns.Contains("Allowed"))
        {
            TrySet("Allow", OleDbType.Boolean, 0, item.Allowed);
        }
        TrySet("Status", OleDbType.VarWChar, 15, Truncate(!string.IsNullOrWhiteSpace(item.Status) ? item.Status : "SAVED", 15));
        TrySet("ComputerName", OleDbType.VarWChar, 50, Truncate(Environment.MachineName, 50));

        var user = !string.IsNullOrWhiteSpace(item.User) ? item.User : "admin";
        TrySet("EntryUser", OleDbType.VarWChar, 50, Truncate(user, 50));
        TrySet("User", OleDbType.VarWChar, 50, Truncate(user, 50));

        var entryDate = item.Date != default ? item.Date : DateTime.Today;
        TrySet("EntryDate", OleDbType.Date, 0, entryDate);
        TrySet("Date", OleDbType.Date, 0, entryDate);

        var entryTime = new DateTime(1899, 12, 30).Add(item.Time != default ? item.Time : DateTime.Now.TimeOfDay);
        TrySet("EntryTime", OleDbType.Date, 0, entryTime);
        TrySet("Time", OleDbType.Date, 0, entryTime);

        TrySet("HSNCODE", OleDbType.VarWChar, 50, Truncate(item.HSNCode, 50));
        if (!columns.Contains("HSNCODE"))
        {
            TrySet("HSNCode", OleDbType.VarWChar, 20, Truncate(item.HSNCode, 20));
        }

        TrySet("StandardWeight", OleDbType.Decimal, 0, item.StandardWeight);
        TrySet("MinWeight", OleDbType.Decimal, 0, item.MinWeight);
        TrySet("MaxWeight", OleDbType.Decimal, 0, item.MaxWeight);
        TrySet("MRP", OleDbType.Decimal, 0, item.MRP);
        TrySet("SaleRate", OleDbType.Decimal, 0, item.SaleRate);
        TrySet("PurchaseRate", OleDbType.Decimal, 0, item.PurchaseRate);
        TrySet("ReorderLevel", OleDbType.Decimal, 0, item.ReorderLevel);
        TrySet("Description", OleDbType.VarWChar, 500, Truncate(item.Description, 500));
        TrySet("IsActive", OleDbType.Boolean, 0, item.IsActive);
        TrySet("Saved", OleDbType.VarWChar, 10, Truncate(item.Saved ?? "Yes", 10));

        var sql = $"UPDATE [ItemCreation] SET {string.Join(", ", setClauses)} WHERE [ItemCode] = ?";

        await using var command = new OleDbCommand(sql, connection);
        foreach (var p in parameters)
        {
            if (p.Size > 0)
            {
                command.Parameters.Add(p.Name, p.Type, p.Size).Value = p.Value;
            }
            else
            {
                command.Parameters.Add(p.Name, p.Type).Value = p.Value;
            }
        }
        command.Parameters.Add("WhereCode", OleDbType.VarWChar, 50).Value = item.ItemCode;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [ItemCreation] WHERE [ItemCode] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = itemCode;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Item?> FindByCodeAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TOP 1 * FROM [ItemCreation] WHERE [ItemCode] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = itemCode;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM [ItemCreation] ORDER BY [ItemName]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<Item>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    private static Item Map(DbDataReader reader)
    {
        var group = HasColumn(reader, "Group") ? GetString(reader, "Group") : string.Empty;
        var headGroup = HasColumn(reader, "HeadGroup") ? GetString(reader, "HeadGroup") : string.Empty;
        if (string.IsNullOrWhiteSpace(headGroup))
        {
            headGroup = group;
        }
        if (string.IsNullOrWhiteSpace(group))
        {
            group = headGroup;
        }

        var metalType = HasColumn(reader, "MetalType")
            ? GetString(reader, "MetalType")
            : (HasColumn(reader, "Section") ? GetString(reader, "Section") : headGroup);

        var tax = HasColumn(reader, "Tax") ? GetDecimal(reader, "Tax") : (HasColumn(reader, "GSTRate") ? GetDecimal(reader, "GSTRate") : 0m);
        var disper = HasColumn(reader, "Disper") ? GetDecimal(reader, "Disper") : 0m;

        var allowed = true;
        if (HasColumn(reader, "Allowed"))
        {
            allowed = GetBool(reader, "Allowed");
        }
        else if (HasColumn(reader, "Allow"))
        {
            allowed = GetBool(reader, "Allow");
        }

        var unit = HasColumn(reader, "UNIT") ? GetString(reader, "UNIT") : (HasColumn(reader, "Unit") ? GetString(reader, "Unit") : string.Empty);
        var hsn = HasColumn(reader, "HSNCODE") ? GetString(reader, "HSNCODE") : (HasColumn(reader, "HSNCode") ? GetString(reader, "HSNCode") : string.Empty);
        var opening = HasColumn(reader, "Opening") ? GetDecimal(reader, "Opening") : (HasColumn(reader, "OpeningStock") ? GetDecimal(reader, "OpeningStock") : 0m);

        return new Item
        {
            ItemCode = HasColumn(reader, "ItemCode") ? GetString(reader, "ItemCode") : string.Empty,
            ItemName = HasColumn(reader, "ItemName") ? GetString(reader, "ItemName") : string.Empty,
            Group = group,
            SubGroup = HasColumn(reader, "SubGroup") ? GetString(reader, "SubGroup") : string.Empty,
            HeadGroup = headGroup,
            Tax = tax,
            Disper = disper,
            Unit = unit,
            MetalType = metalType,
            Purity = HasColumn(reader, "Purity") ? GetPurityDecimal(reader, "Purity") : 0m,
            RawPurity = HasColumn(reader, "Purity") ? GetString(reader, "Purity") : string.Empty,
            StandardWeight = HasColumn(reader, "StandardWeight") ? GetDecimal(reader, "StandardWeight") : 0m,
            MinWeight = HasColumn(reader, "MinWeight") ? GetDecimal(reader, "MinWeight") : 0m,
            MaxWeight = HasColumn(reader, "MaxWeight") ? GetDecimal(reader, "MaxWeight") : 0m,
            MRP = HasColumn(reader, "MRP") ? GetDecimal(reader, "MRP") : 0m,
            SaleRate = HasColumn(reader, "SaleRate") ? GetDecimal(reader, "SaleRate") : 0m,
            PurchaseRate = HasColumn(reader, "PurchaseRate") ? GetDecimal(reader, "PurchaseRate") : 0m,
            OpeningStock = opening,
            ReorderLevel = HasColumn(reader, "ReorderLevel") ? GetDecimal(reader, "ReorderLevel") : 0m,
            HSNCode = hsn,
            GSTRate = tax,
            Description = HasColumn(reader, "Description") ? GetString(reader, "Description") : string.Empty,
            IsActive = HasColumn(reader, "IsActive") ? GetBool(reader, "IsActive") : true,
            Allowed = allowed,
            Status = HasColumn(reader, "Status") ? GetString(reader, "Status") : "Saved",
            Saved = HasColumn(reader, "Saved") ? GetString(reader, "Saved") : "Yes",
            User = HasColumn(reader, "EntryUser") ? GetString(reader, "EntryUser") : (HasColumn(reader, "User") ? GetString(reader, "User") : string.Empty),
            Date = HasColumn(reader, "EntryDate") ? GetDateTime(reader, "EntryDate") : (HasColumn(reader, "Date") ? GetDateTime(reader, "Date") : DateTime.Today),
            Time = HasColumn(reader, "EntryTime") ? GetTimeOfDay(reader, "EntryTime") : (HasColumn(reader, "Time") ? GetTimeOfDay(reader, "Time") : TimeSpan.Zero)
        };
    }

    private static bool HasColumn(DbDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public async Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<string>();
        try
        {
            const string sql = "SELECT DISTINCT [ItemType] FROM [ItemType] WHERE [ItemType] IS NOT NULL";
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    result.Add(val);
                }
            }
        }
        catch
        {
            // Table or column might be named differently
        }

        if (result.Count == 0)
        {
            try
            {
                const string sql2 = "SELECT DISTINCT [HeadGroup] FROM [ItemCreation] WHERE [HeadGroup] IS NOT NULL AND [HeadGroup] <> ''";
                await using var connection = new OleDbConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new OleDbCommand(sql2, connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var val = reader[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && !result.Contains(val, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(val);
                    }
                }
            }
            catch { }
        }

        if (result.Count == 0)
        {
            result = new List<string> { "GOLD", "SILVER", "DIAMOND", "PLATINUM" };
        }

        return result;
    }

    private static string GetString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? string.Empty : Convert.ToString(reader[column]) ?? string.Empty;

    private static decimal GetDecimal(DbDataReader reader, string column)
    {
        if (reader[column] is DBNull) return 0m;
        var val = reader[column];
        if (val is decimal d) return d;
        if (val is double db) return (decimal)db;
        if (val is float f) return (decimal)f;
        if (val is int i) return i;
        if (val is long l) return l;
        if (decimal.TryParse(Convert.ToString(val), out var parsed)) return parsed;
        return 0m;
    }

    private static decimal GetPurityDecimal(DbDataReader reader, string column)
    {
        if (reader[column] is DBNull) return 0m;
        var val = reader[column];
        if (val is decimal d) return d;
        if (val is double db) return (decimal)db;
        if (val is float f) return (decimal)f;
        if (val is int i) return i;
        if (val is long l) return l;

        var str = Convert.ToString(val)?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return 0m;

        if (decimal.TryParse(str, out var parsed)) return parsed;

        var digitsOnly = new string(str.Where(c => char.IsDigit(c) || c == '.').ToArray());
        if (decimal.TryParse(digitsOnly, out var parsedDigits))
        {
            return parsedDigits;
        }

        return 0m;
    }

    private static bool GetBool(DbDataReader reader, string column) =>
        reader[column] is DBNull ? false : Convert.ToBoolean(reader[column]);

    private static DateTime GetDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? DateTime.Today : Convert.ToDateTime(reader[column]);

    private static TimeSpan GetTimeOfDay(DbDataReader reader, string column) =>
        reader[column] is DBNull ? TimeSpan.Zero : Convert.ToDateTime(reader[column]).TimeOfDay;

    private static object NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static object? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, maxLength);
    }
}