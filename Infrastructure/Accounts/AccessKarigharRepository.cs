using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessKarigharRepository : IKarigharRepository
{
    private readonly string _connectionString;

    public AccessKarigharRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    #region Metal Issued (Karighar Issue)

    public async Task<string> GetNextIssueNoAsync(CancellationToken cancellationToken = default)
    {
        var nextNos = await GetNextIssueNosAsync(cancellationToken);
        return $"IS{nextNos:D5}";
    }

    public async Task<int> GetNextIssueNosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM MetalIssued";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var res = await command.ExecuteScalarAsync(cancellationToken);
        return res is null or DBNull ? 1 : Convert.ToInt32(res);
    }

    public async Task<IReadOnlyList<MetalIssuedItem>> GetAllMetalIssuedAsync(int top = 100, CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 500);
        var sql = $@"
            SELECT TOP {top}
                   IssueNo, Nos, IssueDate, ArtisianName, ItemType, WeightIssued,
                   Purity, [Note], EntryUser, EntryDate, EntryTime, ComputerName, [Status]
            FROM MetalIssued
            WHERE [Status] IS NULL OR [Status] <> 'Deleted'
            ORDER BY IssueDate DESC, Nos DESC";

        var result = new List<MetalIssuedItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapMetalIssuedItem(reader));
        }

        return result;
    }

    public async Task<MetalIssuedItem?> GetMetalIssuedByIdAsync(string issueNo, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM MetalIssued WHERE IssueNo = ?";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = issueNo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapMetalIssuedItem(reader);
        }

        return null;
    }

    public async Task SaveMetalIssuedAsync(MetalIssuedItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
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
                        "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM MetalIssued",
                        cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(item.IssueNo))
                {
                    item.IssueNo = $"IS{item.Nos:D5}";
                }

                const string insertSql = @"
                    INSERT INTO [MetalIssued]
                    ([IssueNo], [Nos], [IssueDate], [ArtisianName], [ItemType], [WeightIssued], [Purity], [Note],
                     [EntryUser], [EntryDate], [EntryTime], [ComputerName], [Status])
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                await using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = insertSql;

                AddParam(insertCmd, item.IssueNo, OleDbType.VarWChar);
                AddParam(insertCmd, item.Nos, OleDbType.Integer);
                AddParam(insertCmd, item.IssueDate, OleDbType.Date);
                AddParam(insertCmd, item.ArtisianName, OleDbType.VarWChar);
                AddParam(insertCmd, item.ItemType, OleDbType.VarWChar);
                AddParam(insertCmd, item.WeightIssued, OleDbType.Currency);
                AddParam(insertCmd, item.Purity, OleDbType.VarWChar);
                AddParam(insertCmd, item.Note, OleDbType.VarWChar);
                AddParam(insertCmd, item.EntryUser, OleDbType.VarWChar);
                AddParam(insertCmd, item.EntryDate, OleDbType.Date);
                AddParam(insertCmd, item.EntryTime, OleDbType.Date);
                AddParam(insertCmd, item.ComputerName, OleDbType.VarWChar);
                AddParam(insertCmd, item.Status, OleDbType.VarWChar);

                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string updateSql = @"
                    UPDATE MetalIssued
                    SET IssueDate = ?, ArtisianName = ?, ItemType = ?, WeightIssued = ?,
                        Purity = ?, [Note] = ?, [Status] = ?
                    WHERE IssueNo = ?";

                await using var updateCmd = connection.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = updateSql;

                AddParam(updateCmd, item.IssueDate, OleDbType.Date);
                AddParam(updateCmd, item.ArtisianName, OleDbType.VarWChar);
                AddParam(updateCmd, item.ItemType, OleDbType.VarWChar);
                AddParam(updateCmd, item.WeightIssued, OleDbType.Currency);
                AddParam(updateCmd, item.Purity, OleDbType.VarWChar);
                AddParam(updateCmd, item.Note, OleDbType.VarWChar);
                AddParam(updateCmd, item.Status, OleDbType.VarWChar);
                AddParam(updateCmd, item.IssueNo, OleDbType.VarWChar);

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

    public async Task DeleteMetalIssuedAsync(string issueNo, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE MetalIssued SET [Status] = 'Deleted' WHERE IssueNo = ?";
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = issueNo;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    #endregion

    #region Metal Received (Karighar Receive)

    public async Task<string> GetNextRecNoAsync(CancellationToken cancellationToken = default)
    {
        var nextNos = await GetNextRecNosAsync(cancellationToken);
        return $"RE{nextNos:D5}";
    }

    public async Task<int> GetNextRecNosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM MetalReceived";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var res = await command.ExecuteScalarAsync(cancellationToken);
        return res is null or DBNull ? 1 : Convert.ToInt32(res);
    }

    public async Task<IReadOnlyList<MetalReceivedItem>> GetAllMetalReceivedAsync(int top = 100, CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 500);
        var sql = $@"
            SELECT TOP {top}
                   RecNo, Nos, RecDate, PartyName, ItemType, WeightRecd, MakingCharges,
                   [Note], EntryUser, EntryDate, EntryTime, ComputerName, [Status]
            FROM MetalReceived
            WHERE [Status] IS NULL OR [Status] <> 'Deleted'
            ORDER BY RecDate DESC, Nos DESC";

        var result = new List<MetalReceivedItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapMetalReceivedItem(reader));
        }

        return result;
    }

    public async Task<MetalReceivedItem?> GetMetalReceivedByIdAsync(string recNo, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM MetalReceived WHERE RecNo = ?";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = recNo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapMetalReceivedItem(reader);
        }

        return null;
    }

    public async Task SaveMetalReceivedAsync(MetalReceivedItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
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
                        "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM MetalReceived",
                        cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(item.RecNo))
                {
                    item.RecNo = $"RE{item.Nos:D5}";
                }

                const string insertSql = @"
                    INSERT INTO [MetalReceived]
                    ([RecNo], [Nos], [RecDate], [PartyName], [ItemType], [WeightRecd], [MakingCharges], [Note],
                     [EntryUser], [EntryDate], [EntryTime], [ComputerName], [Status])
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                await using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = insertSql;

                AddParam(insertCmd, item.RecNo, OleDbType.VarWChar);
                AddParam(insertCmd, item.Nos, OleDbType.Integer);
                AddParam(insertCmd, item.RecDate, OleDbType.Date);
                AddParam(insertCmd, item.PartyName, OleDbType.VarWChar);
                AddParam(insertCmd, item.ItemType, OleDbType.VarWChar);
                AddParam(insertCmd, item.WeightRecd, OleDbType.Currency);
                AddParam(insertCmd, item.MakingCharges, OleDbType.Currency);
                AddParam(insertCmd, item.Note, OleDbType.VarWChar);
                AddParam(insertCmd, item.EntryUser, OleDbType.VarWChar);
                AddParam(insertCmd, item.EntryDate, OleDbType.Date);
                AddParam(insertCmd, item.EntryTime, OleDbType.Date);
                AddParam(insertCmd, item.ComputerName, OleDbType.VarWChar);
                AddParam(insertCmd, item.Status, OleDbType.VarWChar);

                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string updateSql = @"
                    UPDATE MetalReceived
                    SET RecDate = ?, PartyName = ?, ItemType = ?, WeightRecd = ?,
                        MakingCharges = ?, [Note] = ?, [Status] = ?
                    WHERE RecNo = ?";

                await using var updateCmd = connection.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = updateSql;

                AddParam(updateCmd, item.RecDate, OleDbType.Date);
                AddParam(updateCmd, item.PartyName, OleDbType.VarWChar);
                AddParam(updateCmd, item.ItemType, OleDbType.VarWChar);
                AddParam(updateCmd, item.WeightRecd, OleDbType.Currency);
                AddParam(updateCmd, item.MakingCharges, OleDbType.Currency);
                AddParam(updateCmd, item.Note, OleDbType.VarWChar);
                AddParam(updateCmd, item.Status, OleDbType.VarWChar);
                AddParam(updateCmd, item.RecNo, OleDbType.VarWChar);

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

    public async Task DeleteMetalReceivedAsync(string recNo, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE MetalReceived SET [Status] = 'Deleted' WHERE RecNo = ?";
        cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = recNo;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    #endregion

    #region Lookups

    public async Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var cmd = new OleDbCommand("SELECT ItemType FROM ItemType ORDER BY ItemType", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var s = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0));
                if (!string.IsNullOrWhiteSpace(s) && !list.Contains(s))
                {
                    list.Add(s.Trim());
                }
            }
        }
        catch
        {
            list.AddRange(new[]
            {
                "GOLD PRIMARY",
                "GOLD NEW ORNAMENTS",
                "GOLD OLD",
                "GOLD 18CT",
                "GOLD STANDARD",
                "SILVER PRIMARY",
                "SILVER NEW ITEMS",
                "SILVER OLD",
                "SILVER STANDARD",
                "DIAMOND",
                "CZ OLD"
            });
        }

        if (list.Count == 0)
        {
            list.AddRange(new[]
            {
                "GOLD PRIMARY",
                "GOLD NEW ORNAMENTS",
                "GOLD OLD",
                "GOLD 18CT",
                "GOLD STANDARD",
                "SILVER PRIMARY",
                "SILVER NEW ITEMS",
                "SILVER OLD",
                "SILVER STANDARD",
                "DIAMOND",
                "CZ OLD"
            });
        }

        return list;
    }

    #endregion

    #region Mapping Helpers

    private static MetalIssuedItem MapMetalIssuedItem(IDataRecord r)
    {
        return new MetalIssuedItem
        {
            IssueNo = GetStringDirect(r, "IssueNo"),
            Nos = GetIntDirect(r, "Nos"),
            IssueDate = GetDateTimeDirect(r, "IssueDate") ?? DateTime.Today,
            ArtisianName = GetStringDirect(r, "ArtisianName"),
            ItemType = GetStringDirect(r, "ItemType"),
            WeightIssued = GetDecimalDirect(r, "WeightIssued"),
            Purity = GetStringDirect(r, "Purity"),
            Note = GetStringDirect(r, "Note"),
            EntryUser = GetStringDirect(r, "EntryUser"),
            EntryDate = GetDateTimeDirect(r, "EntryDate") ?? DateTime.Today,
            EntryTime = GetDateTimeDirect(r, "EntryTime") ?? DateTime.Now,
            ComputerName = GetStringDirect(r, "ComputerName"),
            Status = GetStringDirect(r, "Status", "Saved")
        };
    }

    private static MetalReceivedItem MapMetalReceivedItem(IDataRecord r)
    {
        return new MetalReceivedItem
        {
            RecNo = GetStringDirect(r, "RecNo"),
            Nos = GetIntDirect(r, "Nos"),
            RecDate = GetDateTimeDirect(r, "RecDate") ?? DateTime.Today,
            PartyName = GetStringDirect(r, "PartyName"),
            ItemType = GetStringDirect(r, "ItemType"),
            WeightRecd = GetDecimalDirect(r, "WeightRecd"),
            MakingCharges = GetDecimalDirect(r, "MakingCharges"),
            Note = GetStringDirect(r, "Note"),
            EntryUser = GetStringDirect(r, "EntryUser"),
            EntryDate = GetDateTimeDirect(r, "EntryDate") ?? DateTime.Today,
            EntryTime = GetDateTimeDirect(r, "EntryTime") ?? DateTime.Now,
            ComputerName = GetStringDirect(r, "ComputerName"),
            Status = GetStringDirect(r, "Status", "Saved")
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

    #endregion
}
