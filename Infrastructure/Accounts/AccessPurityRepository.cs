using System.Data.Common;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessPurityRepository : IPurityRepository
{
    private readonly string? _databasePath;
    private readonly string _connectionString;
    private readonly List<PurityMaster> _defaultPurities = new()
    {
        new() { Purity = "22K", RatePerGram = 6850m, ItemHead = "GOLD", ItemType = "JEWELLERY" },
        new() { Purity = "916", RatePerGram = 6850m, ItemHead = "GOLD", ItemType = "JEWELLERY" },
        new() { Purity = "18K", RatePerGram = 5600m, ItemHead = "GOLD", ItemType = "JEWELLERY" },
        new() { Purity = "750", RatePerGram = 5600m, ItemHead = "GOLD", ItemType = "JEWELLERY" },
        new() { Purity = "24K", RatePerGram = 7450m, ItemHead = "GOLD", ItemType = "BULLION" },
        new() { Purity = "SILVER", RatePerGram = 88m, ItemHead = "SILVER", ItemType = "ARTICLES" },
    };

    public AccessPurityRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = AccessConnectionProvider.GetConnectionString(databasePath);
    }

    public async Task<IReadOnlyList<PurityMaster>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_databasePath) || !File.Exists(_databasePath))
        {
            return _defaultPurities;
        }

        try
        {
            const string sql = "SELECT [Purity], [Rate/Gr], [ItemHead], [ItemType], [Hide] FROM [PURITY] ORDER BY [Purity]";

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var list = new List<PurityMaster>();
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(Map(reader));
            }

            return list.Count > 0 ? list : _defaultPurities;
        }
        catch
        {
            return _defaultPurities;
        }
    }

    public async Task<PurityMaster?> GetByPurityAsync(string purity, CancellationToken cancellationToken = default)
    {
        var cleanPurity = purity?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(cleanPurity)) return null;

        if (string.IsNullOrWhiteSpace(_databasePath) || !File.Exists(_databasePath))
        {
            return _defaultPurities.FirstOrDefault(p =>
                string.Equals(p.Purity, cleanPurity, StringComparison.OrdinalIgnoreCase));
        }

        try
        {
            const string sql = "SELECT TOP 1 [Purity], [Rate/Gr], [ItemHead], [ItemType], [Hide] FROM [PURITY] WHERE [Purity] = ?";

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            command.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = cleanPurity;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return Map(reader);
            }
        }
        catch
        {
            return _defaultPurities.FirstOrDefault(p =>
                string.Equals(p.Purity, cleanPurity, StringComparison.OrdinalIgnoreCase));
        }

        return _defaultPurities.FirstOrDefault(p =>
            string.Equals(p.Purity, cleanPurity, StringComparison.OrdinalIgnoreCase));
    }

    public async Task AddAsync(PurityMaster purity, CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO [PURITY] ([Purity], [Rate/Gr], [ItemHead], [ItemType], [Hide]) VALUES (?, ?, ?, ?, ?)";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = purity.Purity?.Trim() ?? string.Empty;
        command.Parameters.Add("@RatePerGr", OleDbType.Currency).Value = purity.RatePerGram;
        command.Parameters.Add("@ItemHead", OleDbType.VarWChar, 50).Value = NullIfEmpty(purity.ItemHead);
        command.Parameters.Add("@ItemType", OleDbType.VarWChar, 50).Value = NullIfEmpty(purity.ItemType);
        command.Parameters.Add("@Hide", OleDbType.Boolean).Value = purity.Hide;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(string originalPurity, PurityMaster purity, CancellationToken cancellationToken = default)
    {
        // If purity name changed, delete and insert or update with new purity if allowed
        if (!string.Equals(originalPurity, purity.Purity, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteAsync(originalPurity, cancellationToken);
            await AddAsync(purity, cancellationToken);
            return;
        }

        const string sql = "UPDATE [PURITY] SET [Rate/Gr] = ?, [ItemHead] = ?, [ItemType] = ?, [Hide] = ? WHERE [Purity] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@RatePerGr", OleDbType.Currency).Value = purity.RatePerGram;
        command.Parameters.Add("@ItemHead", OleDbType.VarWChar, 50).Value = NullIfEmpty(purity.ItemHead);
        command.Parameters.Add("@ItemType", OleDbType.VarWChar, 50).Value = NullIfEmpty(purity.ItemType);
        command.Parameters.Add("@Hide", OleDbType.Boolean).Value = purity.Hide;
        command.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = originalPurity;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string purity, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [PURITY] WHERE [Purity] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = purity;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetItemHeadsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT [ItemHead] FROM [ItemHead] WHERE ([Hide] = False OR [Hide] IS NULL) ORDER BY [ItemHead]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var list = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var head = Convert.ToString(reader[0])?.Trim();
            if (!string.IsNullOrWhiteSpace(head))
            {
                list.Add(head);
            }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(h => h).ToList();
    }

    public async Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT [ItemType] FROM [ItemType] ORDER BY [ItemType]";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var list = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemType = Convert.ToString(reader[0])?.Trim();
            if (!string.IsNullOrWhiteSpace(itemType))
            {
                list.Add(itemType);
            }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToList();
    }

    public async Task UpdateRatesBatchAsync(IEnumerable<RateUpdateItem> rateUpdates, string username, CancellationToken cancellationToken = default)
    {
        var items = rateUpdates?.Where(r => !string.IsNullOrWhiteSpace(r.Purity)).ToList();
        if (items is null || items.Count == 0) return;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string updatePuritySql = "UPDATE [PURITY] SET [Rate/Gr] = ? WHERE [Purity] = ?";
        const string insertRateStoredSql = "INSERT INTO [RateStored] ([Purity], [Rate], [Date], [Time]) VALUES (?, ?, ?, ?)";

        var today = DateTime.Today;
        var now = DateTime.Now;

        foreach (var item in items)
        {
            // 1. Update [PURITY]
            await using (var updateCmd = new OleDbCommand(updatePuritySql, connection))
            {
                updateCmd.Parameters.Add("@RatePerGr", OleDbType.Currency).Value = item.NewRate;
                updateCmd.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = item.Purity.Trim();
                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2. Insert into [RateStored] log table
            await using (var insertCmd = new OleDbCommand(insertRateStoredSql, connection))
            {
                insertCmd.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = item.Purity.Trim();
                insertCmd.Parameters.Add("@Rate", OleDbType.Currency).Value = item.NewRate;
                insertCmd.Parameters.Add("@Date", OleDbType.DBDate).Value = today;
                insertCmd.Parameters.Add("@Time", OleDbType.DBTime).Value = now.TimeOfDay;
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<RateStoredEntry>> GetRateHistoryAsync(string? purity = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var sql = string.IsNullOrWhiteSpace(purity)
            ? $"SELECT TOP {limit} [AutoNo], [Purity], [Rate], [Date], [Time] FROM [RateStored] ORDER BY [Date] DESC, [AutoNo] DESC"
            : $"SELECT TOP {limit} [AutoNo], [Purity], [Rate], [Date], [Time] FROM [RateStored] WHERE [Purity] = ? ORDER BY [Date] DESC, [AutoNo] DESC";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(purity))
        {
            command.Parameters.Add("@Purity", OleDbType.VarWChar, 50).Value = purity.Trim();
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<RateStoredEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new RateStoredEntry
            {
                AutoNo = reader["AutoNo"] is DBNull ? 0 : Convert.ToInt32(reader["AutoNo"]),
                Purity = Convert.ToString(reader["Purity"])?.Trim() ?? string.Empty,
                Rate = reader["Rate"] is DBNull ? 0m : Convert.ToDecimal(reader["Rate"]),
                Date = reader["Date"] is DBNull ? DateTime.MinValue : Convert.ToDateTime(reader["Date"]),
                Time = reader["Time"] is DBNull ? DateTime.MinValue : Convert.ToDateTime(reader["Time"])
            });
        }

        return list;
    }

    private static PurityMaster Map(DbDataReader reader)
    {
        return new PurityMaster
        {
            Purity = Convert.ToString(reader["Purity"])?.Trim() ?? string.Empty,
            RatePerGram = reader["Rate/Gr"] is DBNull ? 0m : Convert.ToDecimal(reader["Rate/Gr"]),
            ItemHead = Convert.ToString(reader["ItemHead"])?.Trim() ?? string.Empty,
            ItemType = Convert.ToString(reader["ItemType"])?.Trim() ?? string.Empty,
            Hide = reader["Hide"] is not DBNull && Convert.ToBoolean(reader["Hide"])
        };
    }

    private static object NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}
