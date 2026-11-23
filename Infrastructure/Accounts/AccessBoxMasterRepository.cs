using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessBoxMasterRepository : IBoxMasterRepository
{
    private readonly string _connectionString;

    public AccessBoxMasterRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<BoxMaster>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await AccessBoxMasterSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        const string sql = @"
            SELECT [BoxNo], [BoxName], [Rack], [NumberOfBoxes], [BoxWt], [Serial], [Status], [Remarks], [CreatedDate], [ModifiedDate]
            FROM [BoxMaster]
            WHERE [Status] IS NULL OR [Status] <> 'Deleted'
            ORDER BY [Serial] ASC, [BoxNo] ASC";

        var list = new List<BoxMaster>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(Map(reader));
        }

        return list;
    }

    public async Task<BoxMaster?> GetByBoxNoAsync(string boxNo, CancellationToken cancellationToken = default)
    {
        await AccessBoxMasterSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        const string sql = @"
            SELECT TOP 1 [BoxNo], [BoxName], [Rack], [NumberOfBoxes], [BoxWt], [Serial], [Status], [Remarks], [CreatedDate], [ModifiedDate]
            FROM [BoxMaster]
            WHERE [BoxNo] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@BoxNo", OleDbType.VarWChar, 50).Value = boxNo.Trim();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task AddAsync(BoxMaster box, CancellationToken cancellationToken = default)
    {
        await AccessBoxMasterSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        if (box.Serial <= 0)
        {
            box.Serial = await GetNextSerialAsync(cancellationToken);
        }

        const string sql = @"
            INSERT INTO [BoxMaster] ([BoxNo], [BoxName], [Rack], [NumberOfBoxes], [BoxWt], [Serial], [Status], [Remarks], [CreatedDate], [ModifiedDate])
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@BoxNo", OleDbType.VarWChar, 50).Value = box.BoxNo.Trim();
        command.Parameters.Add("@BoxName", OleDbType.VarWChar, 100).Value = NullIfEmpty(box.BoxName?.Trim());
        command.Parameters.Add("@Rack", OleDbType.VarWChar, 50).Value = NullIfEmpty(box.Rack?.Trim());
        command.Parameters.Add("@NumberOfBoxes", OleDbType.Integer).Value = box.NumberOfBoxes > 0 ? box.NumberOfBoxes : 1;
        command.Parameters.Add("@BoxWt", OleDbType.Double).Value = (double)box.BoxWt;
        command.Parameters.Add("@Serial", OleDbType.Integer).Value = box.Serial;
        command.Parameters.Add("@Status", OleDbType.VarWChar, 50).Value = string.IsNullOrWhiteSpace(box.Status) ? "Active" : box.Status.Trim();
        command.Parameters.Add("@Remarks", OleDbType.LongVarWChar).Value = NullIfEmpty(box.Remarks);
        command.Parameters.Add("@CreatedDate", OleDbType.Date).Value = box.CreatedDate ?? DateTime.Now;
        command.Parameters.Add("@ModifiedDate", OleDbType.Date).Value = DateTime.Now;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(string originalBoxNo, BoxMaster box, CancellationToken cancellationToken = default)
    {
        await AccessBoxMasterSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        if (!string.Equals(originalBoxNo, box.BoxNo, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteAsync(originalBoxNo, cancellationToken);
            await AddAsync(box, cancellationToken);
            return;
        }

        const string sql = @"
            UPDATE [BoxMaster]
            SET [BoxName] = ?, [Rack] = ?, [NumberOfBoxes] = ?, [BoxWt] = ?, [Serial] = ?, [Status] = ?, [Remarks] = ?, [ModifiedDate] = ?
            WHERE [BoxNo] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@BoxName", OleDbType.VarWChar, 100).Value = NullIfEmpty(box.BoxName?.Trim());
        command.Parameters.Add("@Rack", OleDbType.VarWChar, 50).Value = NullIfEmpty(box.Rack?.Trim());
        command.Parameters.Add("@NumberOfBoxes", OleDbType.Integer).Value = box.NumberOfBoxes > 0 ? box.NumberOfBoxes : 1;
        command.Parameters.Add("@BoxWt", OleDbType.Double).Value = (double)box.BoxWt;
        command.Parameters.Add("@Serial", OleDbType.Integer).Value = box.Serial;
        command.Parameters.Add("@Status", OleDbType.VarWChar, 50).Value = string.IsNullOrWhiteSpace(box.Status) ? "Active" : box.Status.Trim();
        command.Parameters.Add("@Remarks", OleDbType.LongVarWChar).Value = NullIfEmpty(box.Remarks);
        command.Parameters.Add("@ModifiedDate", OleDbType.Date).Value = DateTime.Now;
        command.Parameters.Add("@OriginalBoxNo", OleDbType.VarWChar, 50).Value = originalBoxNo.Trim();

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string boxNo, CancellationToken cancellationToken = default)
    {
        await AccessBoxMasterSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        const string sql = "DELETE FROM [BoxMaster] WHERE [BoxNo] = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("@BoxNo", OleDbType.VarWChar, 50).Value = boxNo.Trim();

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GetNextSerialAsync(CancellationToken cancellationToken = default)
    {
        await AccessBoxMasterSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        const string sql = "SELECT IIf(IsNull(Max([Serial])), 0, Max([Serial])) + 1 FROM [BoxMaster]";

        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? 1 : Convert.ToInt32(result);
        }
        catch
        {
            return 1;
        }
    }

    public async Task<IReadOnlyList<string>> GetRacksAsync(CancellationToken cancellationToken = default)
    {
        await AccessBoxMasterSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        const string sql = "SELECT DISTINCT [Rack] FROM [BoxMaster] WHERE [Rack] IS NOT NULL AND [Rack] <> '' ORDER BY [Rack]";

        var list = new List<string>();
        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var val = reader[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    list.Add(val);
                }
            }
        }
        catch
        {
            // fallback
        }

        return list;
    }

    private static BoxMaster Map(DbDataReader reader)
    {
        return new BoxMaster
        {
            BoxNo = GetString(reader, "BoxNo"),
            BoxName = GetString(reader, "BoxName"),
            Rack = GetString(reader, "Rack"),
            NumberOfBoxes = GetInt(reader, "NumberOfBoxes", 1),
            BoxWt = GetDecimal(reader, "BoxWt", 0m),
            Serial = GetInt(reader, "Serial"),
            Status = GetString(reader, "Status", "Active"),
            Remarks = GetString(reader, "Remarks"),
            CreatedDate = GetDateTime(reader, "CreatedDate"),
            ModifiedDate = GetDateTime(reader, "ModifiedDate")
        };
    }

    private static string GetString(DbDataReader reader, string column, string defaultValue = "")
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? defaultValue : Convert.ToString(reader.GetValue(ordinal)) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static int GetInt(DbDataReader reader, string column, int defaultValue = 0)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return defaultValue;
            var val = reader.GetValue(ordinal);
            return Convert.ToInt32(val);
        }
        catch
        {
            return defaultValue;
        }
    }

    private static decimal GetDecimal(DbDataReader reader, string column, decimal defaultValue = 0m)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return defaultValue;
            var val = reader.GetValue(ordinal);
            if (val is decimal d) return d;
            if (val is double db) return (decimal)db;
            if (val is float f) return (decimal)f;
            if (val is int i) return i;
            if (val is long l) return l;
            if (decimal.TryParse(Convert.ToString(val), out var parsed)) return parsed;
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static DateTime? GetDateTime(DbDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
        catch
        {
            return null;
        }
    }

    private static object NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }
}
