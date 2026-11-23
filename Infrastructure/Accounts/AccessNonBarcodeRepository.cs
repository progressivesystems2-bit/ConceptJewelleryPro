using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessNonBarcodeRepository : INonBarcodeRepository
{
    private readonly string _connectionString;

    public AccessNonBarcodeRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    public async Task<IReadOnlyList<NonBarcode>> GetRecentAsync(int count = 25, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 500);

        var sql = $@"
            SELECT TOP {count}
                   VouNo, Nos, VouDate, SupplierName, EntryUser, EntryDate, EntryTime, Status, ComputerName
            FROM NonBarcode
            WHERE Status IS NULL OR Status <> 'Deleted'
            ORDER BY VouDate DESC, Nos DESC";

        var result = new List<NonBarcode>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapHeader(reader));
        }

        return result;
    }

    public async Task<NonBarcode?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        const string headerSql = @"
            SELECT VouNo, Nos, VouDate, SupplierName, EntryUser, EntryDate, EntryTime, Status, ComputerName
            FROM NonBarcode
            WHERE VouNo = ? AND (Status IS NULL OR Status <> 'Deleted')";

        await AccessBoxMasterSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        const string linesSql = @"
            SELECT VouNo, ItemName, Qty, Weight, PunchTime, BoxNo
            FROM NonBarcodeSubform
            WHERE VouNo = ?";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        NonBarcode? header = null;
        await using (var headerCommand = connection.CreateCommand())
        {
            headerCommand.CommandText = headerSql;
            headerCommand.Parameters.Add("?", OleDbType.VarWChar, 50).Value = vouNo;
            await using var reader = await headerCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                header = MapHeader(reader);
            }
        }

        if (header is null)
            return null;

        await using (var linesCommand = connection.CreateCommand())
        {
            linesCommand.CommandText = linesSql;
            linesCommand.Parameters.Add("?", OleDbType.VarWChar, 50).Value = vouNo;
            await using var reader = await linesCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                header.Lines.Add(MapLine(reader));
            }
        }

        return header;
    }

    public async Task<int> GetNextNosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM NonBarcode";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 1 : Convert.ToInt32(result);
    }

    public async Task SaveAsync(NonBarcode stock, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (OleDbTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!updateExisting && stock.Nos <= 0)
            {
                stock.Nos = await ExecuteScalarIntAsync(
                    connection,
                    transaction,
                    "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM NonBarcode",
                    cancellationToken);
            }

            if (updateExisting)
            {
                const string updateSql = @"
                    UPDATE NonBarcode
                    SET VouDate = ?, SupplierName = ?, EntryUser = ?, EntryDate = ?, EntryTime = ?,
                        Status = ?, ComputerName = ?
                    WHERE VouNo = ?";

                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = updateSql;

                AddParam(update, stock.VouDate, OleDbType.Date);
                AddParam(update, stock.SupplierName, OleDbType.VarWChar);
                AddParam(update, stock.EntryUser, OleDbType.VarWChar);
                AddParam(update, stock.EntryDate, OleDbType.Date);
                AddParam(update, stock.EntryTime, OleDbType.Date);
                AddParam(update, stock.Status, OleDbType.VarWChar);
                AddParam(update, stock.ComputerName, OleDbType.VarWChar);
                AddParam(update, stock.VouNo, OleDbType.VarWChar);

                await update.ExecuteNonQueryAsync(cancellationToken);

                await using var deleteLines = connection.CreateCommand();
                deleteLines.Transaction = transaction;
                deleteLines.CommandText = "DELETE FROM NonBarcodeSubform WHERE VouNo = ?";
                deleteLines.Parameters.Add("?", OleDbType.VarWChar, 50).Value = stock.VouNo;
                await deleteLines.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string insertSql = @"
                    INSERT INTO [NonBarcode]
                    ([VouNo], [Nos], [VouDate], [SupplierName], [EntryUser], [EntryDate], [EntryTime], [Status], [ComputerName])
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)";

                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = insertSql;

                AddParam(insert, stock.VouNo, OleDbType.VarWChar);
                AddParam(insert, stock.Nos, OleDbType.Integer);
                AddParam(insert, stock.VouDate, OleDbType.Date);
                AddParam(insert, stock.SupplierName, OleDbType.VarWChar);
                AddParam(insert, stock.EntryUser, OleDbType.VarWChar);
                AddParam(insert, stock.EntryDate, OleDbType.Date);
                AddParam(insert, stock.EntryTime, OleDbType.Date);
                AddParam(insert, stock.Status, OleDbType.VarWChar);
                AddParam(insert, stock.ComputerName, OleDbType.VarWChar);

                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            const string lineSql = @"
                INSERT INTO [NonBarcodeSubform]
                ([VouNo], [ItemName], [Qty], [Weight], [PunchTime], [BoxNo])
                VALUES (?, ?, ?, ?, ?, ?)";

            foreach (var line in stock.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.ItemName) && line.Qty == 0 && line.Weight == 0)
                    continue;

                await using var lineCommand = connection.CreateCommand();
                lineCommand.Transaction = transaction;
                lineCommand.CommandText = lineSql;

                AddParam(lineCommand, stock.VouNo, OleDbType.VarWChar);
                AddParam(lineCommand, line.ItemName, OleDbType.VarWChar);
                AddParam(lineCommand, line.Qty, OleDbType.Currency);
                AddParam(lineCommand, line.Weight, OleDbType.Currency);
                AddParam(lineCommand, line.PunchTime == default ? DateTime.Now : line.PunchTime, OleDbType.Date);
                AddParam(lineCommand, line.BoxNo, OleDbType.VarWChar);

                await lineCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE NonBarcode SET Status = 'Deleted' WHERE VouNo = ?";
        command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = vouNo;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NonBarcode MapHeader(IDataRecord record)
    {
        return new NonBarcode
        {
            VouNo = GetString(record, "VouNo"),
            Nos = GetInt(record, "Nos"),
            VouDate = GetDateTime(record, "VouDate") ?? DateTime.Today,
            SupplierName = GetString(record, "SupplierName"),
            EntryUser = GetString(record, "EntryUser"),
            EntryDate = GetDateTime(record, "EntryDate") ?? DateTime.Today,
            EntryTime = GetDateTime(record, "EntryTime") ?? DateTime.Now,
            Status = GetString(record, "Status", "Active"),
            ComputerName = GetString(record, "ComputerName")
        };
    }

    private static NonBarcodeSubform MapLine(IDataRecord record)
    {
        return new NonBarcodeSubform
        {
            VouNo = GetString(record, "VouNo"),
            ItemName = GetString(record, "ItemName"),
            Qty = GetDecimal(record, "Qty"),
            Weight = GetDecimal(record, "Weight"),
            BoxNo = GetString(record, "BoxNo"),
            PunchTime = GetDateTime(record, "PunchTime") ?? DateTime.Now
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

    private static string GetString(IDataRecord record, string column, string fallback = "")
    {
        var ordinal = record.GetOrdinal(column);
        return record.IsDBNull(ordinal) ? fallback : Convert.ToString(record.GetValue(ordinal)) ?? fallback;
    }

    private static int GetInt(IDataRecord record, string column, int fallback = 0)
    {
        var ordinal = record.GetOrdinal(column);
        return record.IsDBNull(ordinal) ? fallback : Convert.ToInt32(record.GetValue(ordinal));
    }

    private static decimal GetDecimal(IDataRecord record, string column, decimal fallback = 0m)
    {
        var ordinal = record.GetOrdinal(column);
        return record.IsDBNull(ordinal) ? fallback : Convert.ToDecimal(record.GetValue(ordinal));
    }

    private static DateTime? GetDateTime(IDataRecord record, string column)
    {
        var ordinal = record.GetOrdinal(column);
        return record.IsDBNull(ordinal) ? null : Convert.ToDateTime(record.GetValue(ordinal));
    }
}
