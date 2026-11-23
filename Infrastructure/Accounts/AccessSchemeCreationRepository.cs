using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessSchemeCreationRepository : ISchemeCreationRepository
{
    private readonly string _connectionString;

    public AccessSchemeCreationRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<string> GetNextScodeAsync(CancellationToken cancellationToken = default)
    {
        var nextNos = await GetNextNosAsync(cancellationToken);
        return $"SC{nextNos:D5}";
    }

    public async Task<int> GetNextNosAsync(CancellationToken cancellationToken = default)
    {
        await AccessSchemeCreationSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM SchemeCreation";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var res = await command.ExecuteScalarAsync(cancellationToken);
        return res is null or DBNull ? 1 : Convert.ToInt32(res);
    }

    public async Task<IReadOnlyList<SchemeCreationItem>> GetAllAsync(int top = 100, CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 500);
        await AccessSchemeCreationSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        var sql = $@"
            SELECT TOP {top} *
            FROM SchemeCreation
            WHERE Status IS NULL OR Status <> 'Deleted'
            ORDER BY Nos DESC";

        var result = new List<SchemeCreationItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapSchemeCreationItem(reader));
        }

        return result;
    }

    public async Task<SchemeCreationItem?> GetByScodeAsync(string scode, CancellationToken cancellationToken = default)
    {
        await AccessSchemeCreationSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        const string sql = "SELECT * FROM SchemeCreation WHERE Scode = ?";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = scode;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapSchemeCreationItem(reader);
        }

        return null;
    }

    public async Task SaveAsync(SchemeCreationItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        await AccessSchemeCreationSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (OleDbTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!isUpdate)
            {
                if (item.Nos <= 0)
                {
                    item.Nos = await ExecuteScalarIntAsync(
                        connection,
                        transaction,
                        "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM SchemeCreation",
                        cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(item.Scode))
                {
                    item.Scode = $"SC{item.Nos:D5}";
                }

                // Guard against primary key collision if another record already took this Scode
                bool codeExists = false;
                await using (var checkCmd = connection.CreateCommand())
                {
                    checkCmd.Transaction = transaction;
                    checkCmd.CommandText = "SELECT COUNT(1) FROM SchemeCreation WHERE Scode = ?";
                    checkCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = item.Scode;
                    var countRes = await checkCmd.ExecuteScalarAsync(cancellationToken);
                    codeExists = countRes is not null && Convert.ToInt32(countRes) > 0;
                }

                if (codeExists)
                {
                    item.Nos = await ExecuteScalarIntAsync(
                        connection,
                        transaction,
                        "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM SchemeCreation",
                        cancellationToken);
                    item.Scode = $"SC{item.Nos:D5}";
                }

                const string insertSql = @"
                    INSERT INTO [SchemeCreation]
                    ([Scode], [Nos], [CustomerName], [InstallmentAmt], [Month], [TotalAmt],
                     [SchemeDiscount], [TotalValue], [SDate], [EDate], [Status], [EntryUser],
                     [EntryDate], [EntryTime], [ComputerName], [Closed], [Staff])
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                await using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = insertSql;

                AddParam(insertCmd, item.Scode, OleDbType.VarWChar);
                AddParam(insertCmd, item.Nos, OleDbType.Integer);
                AddParam(insertCmd, item.CustomerName, OleDbType.VarWChar);
                AddParam(insertCmd, item.InstallmentAmt, OleDbType.Currency);
                AddParam(insertCmd, item.Month, OleDbType.Currency);
                AddParam(insertCmd, item.TotalAmt, OleDbType.Currency);
                AddParam(insertCmd, item.SchemeDiscount, OleDbType.Currency);
                AddParam(insertCmd, item.TotalValue, OleDbType.Currency);
                AddParam(insertCmd, item.SDate, OleDbType.Date);
                AddParam(insertCmd, item.EDate, OleDbType.Date);
                AddParam(insertCmd, item.Status, OleDbType.VarWChar);
                AddParam(insertCmd, item.EntryUser, OleDbType.VarWChar);
                AddParam(insertCmd, item.EntryDate, OleDbType.Date);
                AddParam(insertCmd, item.EntryTime, OleDbType.Date);
                AddParam(insertCmd, item.ComputerName, OleDbType.VarWChar);
                AddParam(insertCmd, item.Closed, OleDbType.Boolean);
                AddParam(insertCmd, item.Staff, OleDbType.VarWChar);

                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string updateSql = @"
                    UPDATE SchemeCreation
                    SET CustomerName = ?, InstallmentAmt = ?, [Month] = ?, TotalAmt = ?,
                        SchemeDiscount = ?, TotalValue = ?, SDate = ?, EDate = ?,
                        Status = ?, Closed = ?, Staff = ?
                    WHERE Scode = ?";

                await using var updateCmd = connection.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = updateSql;

                AddParam(updateCmd, item.CustomerName, OleDbType.VarWChar);
                AddParam(updateCmd, item.InstallmentAmt, OleDbType.Currency);
                AddParam(updateCmd, item.Month, OleDbType.Currency);
                AddParam(updateCmd, item.TotalAmt, OleDbType.Currency);
                AddParam(updateCmd, item.SchemeDiscount, OleDbType.Currency);
                AddParam(updateCmd, item.TotalValue, OleDbType.Currency);
                AddParam(updateCmd, item.SDate, OleDbType.Date);
                AddParam(updateCmd, item.EDate, OleDbType.Date);
                AddParam(updateCmd, item.Status, OleDbType.VarWChar);
                AddParam(updateCmd, item.Closed, OleDbType.Boolean);
                AddParam(updateCmd, item.Staff, OleDbType.VarWChar);
                AddParam(updateCmd, item.Scode, OleDbType.VarWChar);

                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteAsync(string scode, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE SchemeCreation SET Status = 'Deleted' WHERE Scode = ?";
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = scode;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SchemeCreationItem MapSchemeCreationItem(IDataRecord r)
    {
        return new SchemeCreationItem
        {
            Scode = GetStringDirect(r, "Scode"),
            Nos = GetIntDirect(r, "Nos"),
            CustomerName = GetStringDirect(r, "CustomerName"),
            InstallmentAmt = GetDecimalDirect(r, "InstallmentAmt"),
            Month = GetDecimalDirect(r, "Month", 11m),
            TotalAmt = GetDecimalDirect(r, "TotalAmt"),
            SchemeDiscount = GetDecimalDirect(r, "SchemeDiscount"),
            TotalValue = GetDecimalDirect(r, "TotalValue"),
            SDate = GetDateTimeDirect(r, "SDate") ?? DateTime.Today,
            EDate = GetDateTimeDirect(r, "EDate") ?? DateTime.Today.AddMonths(12).AddDays(-5),
            Status = GetStringDirect(r, "Status", "Saved"),
            Closed = GetBoolDirect(r, "Closed"),
            Staff = GetStringDirect(r, "Staff"),
            EntryUser = GetStringDirect(r, "EntryUser"),
            EntryDate = GetDateTimeDirect(r, "EntryDate") ?? DateTime.Today,
            EntryTime = GetDateTimeDirect(r, "EntryTime") ?? DateTime.Now,
            ComputerName = GetStringDirect(r, "ComputerName")
        };
    }

    private static async Task<int> ExecuteScalarIntAsync(
        OleDbConnection connection,
        OleDbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 1 : Convert.ToInt32(result);
    }

    private static void AddParam(OleDbCommand command, object? value, OleDbType type)
    {
        var param = command.Parameters.Add("?", type);
        param.Value = value ?? DBNull.Value;
    }

    private static string GetStringDirect(IDataRecord r, string col, string fallback = "")
    {
        for (var i = 0; i < r.FieldCount; i++)
        {
            if (string.Equals(r.GetName(i), col, StringComparison.OrdinalIgnoreCase))
            {
                return r.IsDBNull(i) ? fallback : Convert.ToString(r.GetValue(i)) ?? fallback;
            }
        }
        return fallback;
    }

    private static int GetIntDirect(IDataRecord r, string col, int fallback = 0)
    {
        for (var i = 0; i < r.FieldCount; i++)
        {
            if (string.Equals(r.GetName(i), col, StringComparison.OrdinalIgnoreCase))
            {
                return r.IsDBNull(i) ? fallback : Convert.ToInt32(r.GetValue(i));
            }
        }
        return fallback;
    }

    private static decimal GetDecimalDirect(IDataRecord r, string col, decimal fallback = 0m)
    {
        for (var i = 0; i < r.FieldCount; i++)
        {
            if (string.Equals(r.GetName(i), col, StringComparison.OrdinalIgnoreCase))
            {
                return r.IsDBNull(i) ? fallback : Convert.ToDecimal(r.GetValue(i));
            }
        }
        return fallback;
    }

    private static bool GetBoolDirect(IDataRecord r, string col, bool fallback = false)
    {
        for (var i = 0; i < r.FieldCount; i++)
        {
            if (string.Equals(r.GetName(i), col, StringComparison.OrdinalIgnoreCase))
            {
                return !r.IsDBNull(i) && Convert.ToBoolean(r.GetValue(i));
            }
        }
        return fallback;
    }

    private static DateTime? GetDateTimeDirect(IDataRecord r, string col)
    {
        for (var i = 0; i < r.FieldCount; i++)
        {
            if (string.Equals(r.GetName(i), col, StringComparison.OrdinalIgnoreCase))
            {
                return r.IsDBNull(i) ? null : Convert.ToDateTime(r.GetValue(i));
            }
        }
        return null;
    }
}
