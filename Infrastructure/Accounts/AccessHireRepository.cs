using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessHireRepository : IHireRepository
{
    private readonly string _connectionString;

    public AccessHireRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    #region Hire Issue

    public async Task<int> GetNextIssueNosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM HireIssued";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 1 : Convert.ToInt32(result);
    }

    public async Task<string> GetNextIssueNoAsync(CancellationToken cancellationToken = default)
    {
        var nos = await GetNextIssueNosAsync(cancellationToken);
        return $"HI{nos:D5}";
    }

    public async Task<IReadOnlyList<HireIssuedItem>> GetRecentIssuedAsync(int top = 50, CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 500);
        var sql = $@"
            SELECT TOP {top}
                   IssueNo, Nos, IssueDate, ArtisianName, ItemType, WeightIssued,
                   Note, EntryUser, EntryDate, EntryTime, ComputerName, Status
            FROM HireIssued
            WHERE (Status IS NULL OR Status <> 'Deleted')
            ORDER BY Nos DESC, IssueDate DESC";

        var list = new List<HireIssuedItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapHireIssuedItem(reader));
        }

        return list;
    }

    public async Task<HireIssuedItem?> GetIssuedByNoAsync(string issueNo, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP 1
                   IssueNo, Nos, IssueDate, ArtisianName, ItemType, WeightIssued,
                   Note, EntryUser, EntryDate, EntryTime, ComputerName, Status
            FROM HireIssued
            WHERE IssueNo = @IssueNo AND (Status IS NULL OR Status <> 'Deleted')";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@IssueNo", OleDbType.VarWChar, 50).Value = issueNo.Trim();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapHireIssuedItem(reader);
        }

        return null;
    }

    public async Task SaveIssuedAsync(HireIssuedItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.IssueNo))
        {
            item.IssueNo = await GetNextIssueNoAsync(cancellationToken);
            item.Nos = await GetNextIssueNosAsync(cancellationToken);
        }

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (isUpdate)
        {
            const string updateSql = @"
                UPDATE HireIssued
                SET IssueDate = @IssueDate,
                    ArtisianName = @ArtisianName,
                    ItemType = @ItemType,
                    WeightIssued = @WeightIssued,
                    Note = @Note,
                    EntryUser = @EntryUser,
                    EntryDate = @EntryDate,
                    EntryTime = @EntryTime,
                    ComputerName = @ComputerName,
                    Status = @Status
                WHERE IssueNo = @WhereIssueNo";

            await using var cmd = new OleDbCommand(updateSql, connection);
            cmd.Parameters.Add("@IssueDate", OleDbType.Date).Value = item.IssueDate;
            cmd.Parameters.Add("@ArtisianName", OleDbType.VarWChar, 50).Value = item.ArtisianName ?? string.Empty;
            cmd.Parameters.Add("@ItemType", OleDbType.VarWChar, 50).Value = item.ItemType ?? string.Empty;
            cmd.Parameters.Add("@WeightIssued", OleDbType.Currency).Value = item.WeightIssued;
            cmd.Parameters.Add("@Note", OleDbType.VarWChar, 50).Value = item.Note ?? string.Empty;
            cmd.Parameters.Add("@EntryUser", OleDbType.VarWChar, 50).Value = item.EntryUser ?? string.Empty;
            cmd.Parameters.Add("@EntryDate", OleDbType.Date).Value = item.EntryDate;
            cmd.Parameters.Add("@EntryTime", OleDbType.Date).Value = item.EntryTime;
            cmd.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = item.ComputerName ?? string.Empty;
            cmd.Parameters.Add("@Status", OleDbType.VarWChar, 50).Value = item.Status ?? "Saved";
            cmd.Parameters.Add("@WhereIssueNo", OleDbType.VarWChar, 50).Value = item.IssueNo;

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string insertSql = @"
                INSERT INTO [HireIssued]
                    ([IssueNo], [Nos], [IssueDate], [ArtisianName], [ItemType], [WeightIssued],
                     [Note], [EntryUser], [EntryDate], [EntryTime], [ComputerName], [Status])
                VALUES
                    (@IssueNo, @Nos, @IssueDate, @ArtisianName, @ItemType, @WeightIssued,
                     @Note, @EntryUser, @EntryDate, @EntryTime, @ComputerName, @Status)";

            await using var cmd = new OleDbCommand(insertSql, connection);
            cmd.Parameters.Add("@IssueNo", OleDbType.VarWChar, 50).Value = item.IssueNo;
            cmd.Parameters.Add("@Nos", OleDbType.Integer).Value = item.Nos;
            cmd.Parameters.Add("@IssueDate", OleDbType.Date).Value = item.IssueDate;
            cmd.Parameters.Add("@ArtisianName", OleDbType.VarWChar, 50).Value = item.ArtisianName ?? string.Empty;
            cmd.Parameters.Add("@ItemType", OleDbType.VarWChar, 50).Value = item.ItemType ?? string.Empty;
            cmd.Parameters.Add("@WeightIssued", OleDbType.Currency).Value = item.WeightIssued;
            cmd.Parameters.Add("@Note", OleDbType.VarWChar, 50).Value = item.Note ?? string.Empty;
            cmd.Parameters.Add("@EntryUser", OleDbType.VarWChar, 50).Value = item.EntryUser ?? string.Empty;
            cmd.Parameters.Add("@EntryDate", OleDbType.Date).Value = item.EntryDate;
            cmd.Parameters.Add("@EntryTime", OleDbType.Date).Value = item.EntryTime;
            cmd.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = item.ComputerName ?? string.Empty;
            cmd.Parameters.Add("@Status", OleDbType.VarWChar, 50).Value = item.Status ?? "Saved";

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task DeleteIssuedAsync(string issueNo, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM HireIssued WHERE IssueNo = @IssueNo";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@IssueNo", OleDbType.VarWChar, 50).Value = issueNo.Trim();
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    #endregion

    #region Hire Receive

    public async Task<int> GetNextRecNosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM HireReceived";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 1 : Convert.ToInt32(result);
    }

    public async Task<string> GetNextRecNoAsync(CancellationToken cancellationToken = default)
    {
        var nos = await GetNextRecNosAsync(cancellationToken);
        return $"HR{nos:D5}";
    }

    public async Task<IReadOnlyList<HireReceivedItem>> GetRecentReceivedAsync(int top = 50, CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 500);
        var sql = $@"
            SELECT TOP {top}
                   RecNo, Nos, RecDate, PartyName, ItemType, WeightRecd,
                   Note, EntryUser, EntryDate, EntryTime, ComputerName, Status
            FROM HireReceived
            WHERE (Status IS NULL OR Status <> 'Deleted')
            ORDER BY Nos DESC, RecDate DESC";

        var list = new List<HireReceivedItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapHireReceivedItem(reader));
        }

        return list;
    }

    public async Task<HireReceivedItem?> GetReceivedByNoAsync(string recNo, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP 1
                   RecNo, Nos, RecDate, PartyName, ItemType, WeightRecd,
                   Note, EntryUser, EntryDate, EntryTime, ComputerName, Status
            FROM HireReceived
            WHERE RecNo = @RecNo AND (Status IS NULL OR Status <> 'Deleted')";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@RecNo", OleDbType.VarWChar, 50).Value = recNo.Trim();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapHireReceivedItem(reader);
        }

        return null;
    }

    public async Task SaveReceivedAsync(HireReceivedItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.RecNo))
        {
            item.RecNo = await GetNextRecNoAsync(cancellationToken);
            item.Nos = await GetNextRecNosAsync(cancellationToken);
        }

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (isUpdate)
        {
            const string updateSql = @"
                UPDATE HireReceived
                SET RecDate = @RecDate,
                    PartyName = @PartyName,
                    ItemType = @ItemType,
                    WeightRecd = @WeightRecd,
                    Note = @Note,
                    EntryUser = @EntryUser,
                    EntryDate = @EntryDate,
                    EntryTime = @EntryTime,
                    ComputerName = @ComputerName,
                    Status = @Status
                WHERE RecNo = @WhereRecNo";

            await using var cmd = new OleDbCommand(updateSql, connection);
            cmd.Parameters.Add("@RecDate", OleDbType.Date).Value = item.RecDate;
            cmd.Parameters.Add("@PartyName", OleDbType.VarWChar, 50).Value = item.PartyName ?? string.Empty;
            cmd.Parameters.Add("@ItemType", OleDbType.VarWChar, 50).Value = item.ItemType ?? string.Empty;
            cmd.Parameters.Add("@WeightRecd", OleDbType.Currency).Value = item.WeightRecd;
            cmd.Parameters.Add("@Note", OleDbType.VarWChar, 50).Value = item.Note ?? string.Empty;
            cmd.Parameters.Add("@EntryUser", OleDbType.VarWChar, 50).Value = item.EntryUser ?? string.Empty;
            cmd.Parameters.Add("@EntryDate", OleDbType.Date).Value = item.EntryDate;
            cmd.Parameters.Add("@EntryTime", OleDbType.Date).Value = item.EntryTime;
            cmd.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = item.ComputerName ?? string.Empty;
            cmd.Parameters.Add("@Status", OleDbType.VarWChar, 50).Value = item.Status ?? "Saved";
            cmd.Parameters.Add("@WhereRecNo", OleDbType.VarWChar, 50).Value = item.RecNo;

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string insertSql = @"
                INSERT INTO [HireReceived]
                    ([RecNo], [Nos], [RecDate], [PartyName], [ItemType], [WeightRecd],
                     [Note], [EntryUser], [EntryDate], [EntryTime], [ComputerName], [Status])
                VALUES
                    (@RecNo, @Nos, @RecDate, @PartyName, @ItemType, @WeightRecd,
                     @Note, @EntryUser, @EntryDate, @EntryTime, @ComputerName, @Status)";

            await using var cmd = new OleDbCommand(insertSql, connection);
            cmd.Parameters.Add("@RecNo", OleDbType.VarWChar, 50).Value = item.RecNo;
            cmd.Parameters.Add("@Nos", OleDbType.Integer).Value = item.Nos;
            cmd.Parameters.Add("@RecDate", OleDbType.Date).Value = item.RecDate;
            cmd.Parameters.Add("@PartyName", OleDbType.VarWChar, 50).Value = item.PartyName ?? string.Empty;
            cmd.Parameters.Add("@ItemType", OleDbType.VarWChar, 50).Value = item.ItemType ?? string.Empty;
            cmd.Parameters.Add("@WeightRecd", OleDbType.Currency).Value = item.WeightRecd;
            cmd.Parameters.Add("@Note", OleDbType.VarWChar, 50).Value = item.Note ?? string.Empty;
            cmd.Parameters.Add("@EntryUser", OleDbType.VarWChar, 50).Value = item.EntryUser ?? string.Empty;
            cmd.Parameters.Add("@EntryDate", OleDbType.Date).Value = item.EntryDate;
            cmd.Parameters.Add("@EntryTime", OleDbType.Date).Value = item.EntryTime;
            cmd.Parameters.Add("@ComputerName", OleDbType.VarWChar, 50).Value = item.ComputerName ?? string.Empty;
            cmd.Parameters.Add("@Status", OleDbType.VarWChar, 50).Value = item.Status ?? "Saved";

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task DeleteReceivedAsync(string recNo, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM HireReceived WHERE RecNo = @RecNo";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@RecNo", OleDbType.VarWChar, 50).Value = recNo.Trim();
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    #endregion

    #region Lookups

    public async Task<IReadOnlyList<Account>> GetArtisansAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<Account>();
        const string sql = @"
            SELECT * FROM AccountCreation
            WHERE (HeadCategory = 'Artisians' OR HeadCategory = 'Karighar' OR HeadCategory = 'Sundry Creditors' OR HeadCategory = 'Sundry Debtors')
              AND (Status IS NULL OR Status <> 'Deleted')
            ORDER BY AccountName";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new Account
            {
                PartyCode = reader["PartyCode"]?.ToString()?.Trim() ?? string.Empty,
                AccountName = reader["AccountName"]?.ToString()?.Trim() ?? string.Empty,
                HeadCategory = reader["HeadCategory"]?.ToString()?.Trim() ?? string.Empty,
                SubCategory = reader["SubCategory"]?.ToString()?.Trim() ?? string.Empty,
                City = reader["City"]?.ToString()?.Trim() ?? string.Empty,
                PhoneNo = reader["PhoneNo"]?.ToString()?.Trim() ?? string.Empty
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<string>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        try
        {
            const string sql = "SELECT DISTINCT [ItemType] FROM ItemType WHERE [ItemType] IS NOT NULL AND [ItemType] <> '' ORDER BY [ItemType]";
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var type = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(type) && !list.Contains(type, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(type);
                }
            }
        }
        catch { }

        if (list.Count == 0)
        {
            try
            {
                const string sql2 = "SELECT DISTINCT [ItemType] FROM ItemCreation WHERE [ItemType] IS NOT NULL AND [ItemType] <> '' ORDER BY [ItemType]";
                await using var connection = new OleDbConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new OleDbCommand(sql2, connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var type = reader[0]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(type) && !list.Contains(type, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(type);
                    }
                }
            }
            catch { }
        }

        if (list.Count == 0)
        {
            list.AddRange(new[] { "GOLD 22K", "GOLD 18K", "SILVER", "DIAMOND", "PLATINUM" });
        }

        return list;
    }

    #endregion

    private static HireIssuedItem MapHireIssuedItem(IDataRecord r)
    {
        return new HireIssuedItem
        {
            IssueNo = r["IssueNo"]?.ToString()?.Trim() ?? string.Empty,
            Nos = r["Nos"] != DBNull.Value ? Convert.ToInt32(r["Nos"]) : 0,
            IssueDate = r["IssueDate"] != DBNull.Value ? Convert.ToDateTime(r["IssueDate"]) : DateTime.Today,
            ArtisianName = r["ArtisianName"]?.ToString()?.Trim() ?? string.Empty,
            ItemType = r["ItemType"]?.ToString()?.Trim() ?? string.Empty,
            WeightIssued = r["WeightIssued"] != DBNull.Value ? Convert.ToDecimal(r["WeightIssued"]) : 0m,
            Note = r["Note"]?.ToString()?.Trim() ?? string.Empty,
            EntryUser = r["EntryUser"]?.ToString()?.Trim() ?? string.Empty,
            EntryDate = r["EntryDate"] != DBNull.Value ? Convert.ToDateTime(r["EntryDate"]) : DateTime.Today,
            EntryTime = r["EntryTime"] != DBNull.Value ? Convert.ToDateTime(r["EntryTime"]) : DateTime.Now,
            ComputerName = r["ComputerName"]?.ToString()?.Trim() ?? string.Empty,
            Status = r["Status"]?.ToString()?.Trim() ?? "Saved"
        };
    }

    private static HireReceivedItem MapHireReceivedItem(IDataRecord r)
    {
        return new HireReceivedItem
        {
            RecNo = r["RecNo"]?.ToString()?.Trim() ?? string.Empty,
            Nos = r["Nos"] != DBNull.Value ? Convert.ToInt32(r["Nos"]) : 0,
            RecDate = r["RecDate"] != DBNull.Value ? Convert.ToDateTime(r["RecDate"]) : DateTime.Today,
            PartyName = r["PartyName"]?.ToString()?.Trim() ?? string.Empty,
            ItemType = r["ItemType"]?.ToString()?.Trim() ?? string.Empty,
            WeightRecd = r["WeightRecd"] != DBNull.Value ? Convert.ToDecimal(r["WeightRecd"]) : 0m,
            Note = r["Note"]?.ToString()?.Trim() ?? string.Empty,
            EntryUser = r["EntryUser"]?.ToString()?.Trim() ?? string.Empty,
            EntryDate = r["EntryDate"] != DBNull.Value ? Convert.ToDateTime(r["EntryDate"]) : DateTime.Today,
            EntryTime = r["EntryTime"] != DBNull.Value ? Convert.ToDateTime(r["EntryTime"]) : DateTime.Now,
            ComputerName = r["ComputerName"]?.ToString()?.Trim() ?? string.Empty,
            Status = r["Status"]?.ToString()?.Trim() ?? "Saved"
        };
    }
}
