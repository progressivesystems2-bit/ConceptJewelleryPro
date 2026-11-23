using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessStaffRepository : IStaffRepository
{
    private readonly string _connectionString;

    public AccessStaffRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await AccessStaffCommissionLedgerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        const string sql = @"
            SELECT Staff, ComputerName, EntryDate, EntryUser, EntryTime, Status
            FROM Staff
            WHERE Status IS NULL OR Status <> 'Deleted'
            ORDER BY Staff";

        var list = new List<StaffMember>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = new OleDbCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new StaffMember
            {
                Staff = GetStringDirect(reader, "Staff"),
                ComputerName = GetStringDirect(reader, "ComputerName"),
                EntryDate = GetDateTimeDirect(reader, "EntryDate") ?? DateTime.Today,
                EntryUser = GetStringDirect(reader, "EntryUser"),
                EntryTime = GetDateTimeDirect(reader, "EntryTime") ?? DateTime.Now,
                Status = GetStringDirect(reader, "Status", "Saved")
            });
        }

        return list;
    }

    public async Task<StaffMember?> GetByNameAsync(string staffName, CancellationToken cancellationToken = default)
    {
        await AccessStaffCommissionLedgerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        const string sql = "SELECT * FROM Staff WHERE Staff = ?";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = staffName;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new StaffMember
            {
                Staff = GetStringDirect(reader, "Staff"),
                ComputerName = GetStringDirect(reader, "ComputerName"),
                EntryDate = GetDateTimeDirect(reader, "EntryDate") ?? DateTime.Today,
                EntryUser = GetStringDirect(reader, "EntryUser"),
                EntryTime = GetDateTimeDirect(reader, "EntryTime") ?? DateTime.Now,
                Status = GetStringDirect(reader, "Status", "Saved")
            };
        }

        return null;
    }

    public async Task SaveAsync(StaffMember member, bool isUpdate = false, string? oldStaffName = null, CancellationToken cancellationToken = default)
    {
        await AccessStaffCommissionLedgerSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (OleDbTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (isUpdate && !string.IsNullOrWhiteSpace(oldStaffName))
            {
                const string updateSql = @"
                    UPDATE [Staff]
                    SET [Staff] = ?, [Status] = ?
                    WHERE [Staff] = ?";

                await using var updateCmd = connection.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = updateSql;
                updateCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = member.Staff.Trim();
                updateCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = member.Status.Trim();
                updateCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = oldStaffName.Trim();

                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string insertSql = @"
                    INSERT INTO [Staff] ([Staff], [ComputerName], [EntryDate], [EntryUser], [EntryTime], [Status])
                    VALUES (?, ?, ?, ?, ?, ?)";

                await using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = insertSql;
                insertCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = member.Staff.Trim();
                insertCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = member.ComputerName;
                insertCmd.Parameters.Add("?", OleDbType.Date).Value = member.EntryDate;
                insertCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = member.EntryUser;
                insertCmd.Parameters.Add("?", OleDbType.Date).Value = member.EntryTime;
                insertCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = member.Status;

                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteAsync(string staffName, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE [Staff] SET [Status] = 'Deleted' WHERE [Staff] = ?";
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = staffName.Trim();
        await cmd.ExecuteNonQueryAsync(cancellationToken);
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
