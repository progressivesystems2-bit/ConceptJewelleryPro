using System.Data;
using System.Data.OleDb;
using System.Text;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessAuditRepository : IAuditRepository
{
    private readonly string _connectionString;

    public AccessAuditRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};Persist Security Info=False;";
    }

    public async Task LogAuditAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await LogAuditBatchAsync(new[] { entry }, cancellationToken);
    }

    public async Task LogAuditBatchAsync(IEnumerable<AuditLogEntry> entries, CancellationToken cancellationToken = default)
    {
        var list = entries.ToList();
        if (list.Count == 0) return;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [AuditTableFinal]
                ([User], [EntryType], [VouNo], [OldValue], [NewValue], [FieldName], [Date], [Time], [Checked])
            VALUES
                (@User, @EntryType, @VouNo, @OldValue, @NewValue, @FieldName, @Date, @Time, @Checked)";

        foreach (var item in list)
        {
            await using var cmd = new OleDbCommand(sql, connection);
            cmd.Parameters.Add("@User", OleDbType.VarWChar, 50).Value = Truncate(item.User, 50);
            cmd.Parameters.Add("@EntryType", OleDbType.VarWChar, 50).Value = Truncate(item.EntryType, 50);
            cmd.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = Truncate(item.VouNo, 50);
            cmd.Parameters.Add("@OldValue", OleDbType.VarWChar, 150).Value = Truncate(item.OldValue, 150);
            cmd.Parameters.Add("@NewValue", OleDbType.VarWChar, 150).Value = Truncate(item.NewValue, 150);
            cmd.Parameters.Add("@FieldName", OleDbType.VarWChar, 50).Value = Truncate(item.FieldName, 50);
            cmd.Parameters.Add("@Date", OleDbType.Date).Value = item.Date.Date;
            
            // Format time as date + time or 1899 time
            var timeValue = item.Time != DateTime.MinValue ? item.Time : DateTime.Now;
            cmd.Parameters.Add("@Time", OleDbType.Date).Value = new DateTime(1899, 12, 30, timeValue.Hour, timeValue.Minute, timeValue.Second);
            
            cmd.Parameters.Add("@Checked", OleDbType.Boolean).Value = item.Checked;

            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // In case AuditTableFinal doesn't exist, create it and retry once
                if (ex.Message.IndexOf("AuditTableFinal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ex.Message.IndexOf("not find", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    await EnsureTableExistsAsync(connection, cancellationToken);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    throw;
                }
            }
        }
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(AuditReportFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sb = new StringBuilder("SELECT [IdNo], [User], [EntryType], [VouNo], [OldValue], [NewValue], [FieldName], [Date], [Time], [Checked] FROM [AuditTableFinal] WHERE 1=1 ");
        var parameters = new List<OleDbParameter>();

        if (filter.FromDate.HasValue)
        {
            sb.Append(" AND [Date] >= ?");
            parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = filter.FromDate.Value.Date });
        }

        if (filter.ToDate.HasValue)
        {
            sb.Append(" AND [Date] <= ?");
            parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = filter.ToDate.Value.Date });
        }

        if (!string.IsNullOrWhiteSpace(filter.User) && !string.Equals(filter.User, "All", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(" AND [User] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = filter.User.Trim() });
        }

        if (!string.IsNullOrWhiteSpace(filter.EntryType) && !string.Equals(filter.EntryType, "All", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(" AND [EntryType] = ?");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 50) { Value = filter.EntryType.Trim() });
        }

        if (filter.Status == AuditStatusFilter.UncheckedOnly)
        {
            sb.Append(" AND ([Checked] = False OR [Checked] IS NULL)");
        }
        else if (filter.Status == AuditStatusFilter.CheckedOnly)
        {
            sb.Append(" AND [Checked] = True");
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = "%" + filter.SearchText.Trim() + "%";
            sb.Append(" AND ([VouNo] LIKE ? OR [FieldName] LIKE ? OR [OldValue] LIKE ? OR [NewValue] LIKE ?)");
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = search });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = search });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = search });
            parameters.Add(new OleDbParameter("?", OleDbType.VarWChar, 100) { Value = search });
        }

        sb.Append(" ORDER BY [Date] DESC, [Time] DESC, [IdNo] DESC");

        var results = new List<AuditLogEntry>();

        try
        {
            await using var cmd = new OleDbCommand(sb.ToString(), connection);
            foreach (var p in parameters)
            {
                cmd.Parameters.Add(p);
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapEntry(reader));
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.IndexOf("AuditTableFinal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                await EnsureTableExistsAsync(connection, cancellationToken);
                return results;
            }
            throw;
        }

        return results;
    }

    public async Task<IReadOnlyList<string>> GetDistinctUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT DISTINCT [User] FROM [AuditTableFinal] WHERE [User] IS NOT NULL AND [User] <> '' ORDER BY [User]";
        var list = new List<string>();

        try
        {
            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    list.Add(reader.GetString(0));
                }
            }
        }
        catch
        {
            // Table may not exist yet
        }

        return list;
    }

    public async Task<IReadOnlyList<string>> GetDistinctEntryTypesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT DISTINCT [EntryType] FROM [AuditTableFinal] WHERE [EntryType] IS NOT NULL AND [EntryType] <> '' ORDER BY [EntryType]";
        var list = new List<string>();

        try
        {
            await using var cmd = new OleDbCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    list.Add(reader.GetString(0));
                }
            }
        }
        catch
        {
            // Table may not exist yet
        }

        return list;
    }

    public async Task MarkCheckedAsync(IEnumerable<int> idNos, bool isChecked, CancellationToken cancellationToken = default)
    {
        var ids = idNos.ToList();
        if (ids.Count == 0) return;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Update in batches of 50
        const int batchSize = 50;
        for (int i = 0; i < ids.Count; i += batchSize)
        {
            var chunk = ids.Skip(i).Take(batchSize).ToList();
            var inClause = string.Join(",", chunk);
            var sql = $"UPDATE [AuditTableFinal] SET [Checked] = @Checked WHERE [IdNo] IN ({inClause})";

            await using var cmd = new OleDbCommand(sql, connection);
            cmd.Parameters.Add("@Checked", OleDbType.Boolean).Value = isChecked;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task DeleteAuditLogsAsync(IEnumerable<int> idNos, CancellationToken cancellationToken = default)
    {
        var ids = idNos.ToList();
        if (ids.Count == 0) return;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const int batchSize = 50;
        for (int i = 0; i < ids.Count; i += batchSize)
        {
            var chunk = ids.Skip(i).Take(batchSize).ToList();
            var inClause = string.Join(",", chunk);
            var sql = $"DELETE FROM [AuditTableFinal] WHERE [IdNo] IN ({inClause})";

            await using var cmd = new OleDbCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static AuditLogEntry MapEntry(IDataRecord record)
    {
        var entry = new AuditLogEntry
        {
            IdNo = record.GetInt32(record.GetOrdinal("IdNo")),
            User = record.IsDBNull(record.GetOrdinal("User")) ? string.Empty : record.GetString(record.GetOrdinal("User")),
            EntryType = record.IsDBNull(record.GetOrdinal("EntryType")) ? string.Empty : record.GetString(record.GetOrdinal("EntryType")),
            VouNo = record.IsDBNull(record.GetOrdinal("VouNo")) ? string.Empty : record.GetString(record.GetOrdinal("VouNo")),
            OldValue = record.IsDBNull(record.GetOrdinal("OldValue")) ? string.Empty : record.GetString(record.GetOrdinal("OldValue")),
            NewValue = record.IsDBNull(record.GetOrdinal("NewValue")) ? string.Empty : record.GetString(record.GetOrdinal("NewValue")),
            FieldName = record.IsDBNull(record.GetOrdinal("FieldName")) ? string.Empty : record.GetString(record.GetOrdinal("FieldName")),
            Checked = !record.IsDBNull(record.GetOrdinal("Checked")) && record.GetBoolean(record.GetOrdinal("Checked"))
        };

        if (!record.IsDBNull(record.GetOrdinal("Date")))
        {
            entry.Date = record.GetDateTime(record.GetOrdinal("Date"));
        }

        if (!record.IsDBNull(record.GetOrdinal("Time")))
        {
            entry.Time = record.GetDateTime(record.GetOrdinal("Time"));
        }

        return entry;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    private static async Task EnsureTableExistsAsync(OleDbConnection connection, CancellationToken ct)
    {
        const string sql = @"
            CREATE TABLE [AuditTableFinal] (
                [IdNo] AUTOINCREMENT PRIMARY KEY,
                [User] VARCHAR(50),
                [EntryType] VARCHAR(50),
                [VouNo] VARCHAR(50),
                [OldValue] VARCHAR(150),
                [NewValue] VARCHAR(150),
                [FieldName] VARCHAR(50),
                [Time] DATETIME,
                [Date] DATETIME,
                [Checked] YESNO
            )";

        try
        {
            await using var cmd = new OleDbCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Table may already exist
        }
    }
}
