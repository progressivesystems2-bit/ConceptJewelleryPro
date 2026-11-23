using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessPurchaseExpenseRepository : IPurchaseExpenseRepository
{
    private readonly string _connectionString;
    private bool _schemaEnsured;

    public AccessPurchaseExpenseRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured)
            return;

        await AccessPurchaseExpenseSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
        _schemaEnsured = true;
    }

    public async Task<IReadOnlyList<PurchaseExpense>> GetRecentAsync(int count = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        count = Math.Clamp(count, 1, 500);

        var sql = $@"
            SELECT TOP {count}
                   Nos, VouType, VouNo, BillDate, AccountName, Head, PartyName,
                   GSTIN AS Gstin, SuppBillNo, SuppDate AS SuppBillDate, [Reverse],
                   [Tax%] AS ReverseTaxRate, GrossTotal, TaxableAmount, TaxAmt AS TaxAmount,
                   AddLessNote AS ExclusiveAccount, Exclusive, IGST AS Igst, CGST AS Cgst,
                   SGST AS Sgst, AddLess AS RoundedOff, NetAmt AS NetAmount,
                   Narration, [Unique], [User], Status
            FROM PurchaseE
            WHERE Status IS NULL OR Status <> 'Deleted'
            ORDER BY BillDate DESC, Nos DESC";

        var result = new List<PurchaseExpense>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapPurchaseExpenseHeader(reader));
        }

        return result;
    }

    public async Task<PurchaseExpense?> GetByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        const string headerSql = @"
            SELECT Nos, VouType, VouNo, BillDate, AccountName, Head, PartyName,
                   GSTIN AS Gstin, SuppBillNo, SuppDate AS SuppBillDate, [Reverse],
                   [Tax%] AS ReverseTaxRate, GrossTotal, TaxableAmount, TaxAmt AS TaxAmount,
                   AddLessNote AS ExclusiveAccount, Exclusive, IGST AS Igst, CGST AS Cgst,
                   SGST AS Sgst, AddLess AS RoundedOff, NetAmt AS NetAmount,
                   Narration, [Unique], [User], Status
            FROM PurchaseE
            WHERE VouNo = @VouNo AND (Status IS NULL OR Status <> 'Deleted')";

        const string linesSql = @"
            SELECT AutoNo, Vouno AS VouNo, SlNo, ItemName, HSN AS Hsn, Qty, Rate, RateAmount,
                   GrossAmt AS GrossAmount, TaxRate, TaxAmount
            FROM PurchaseESubform
            WHERE Vouno = @VouNo
            ORDER BY AutoNo";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        PurchaseExpense? expense = null;
        await using (var headerCommand = new OleDbCommand(headerSql, connection))
        {
            headerCommand.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;
            await using var reader = await headerCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                expense = MapPurchaseExpenseHeader(reader);
            }
        }

        if (expense is null)
            return null;

        await using (var linesCommand = new OleDbCommand(linesSql, connection))
        {
            linesCommand.Parameters.Add("@VouNo", OleDbType.VarWChar, 50).Value = vouNo;
            await using var reader = await linesCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                expense.Lines.Add(MapPurchaseExpenseLine(reader));
            }
        }

        expense.TotalQuantity = expense.Lines.Sum(l => l.Qty);
        return expense;
    }

    public async Task<int> GetNextNosAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM PurchaseE";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 1 : Convert.ToInt32(result);
    }

    public async Task SaveAsync(PurchaseExpense expense, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (OleDbTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!updateExisting && expense.Nos <= 0)
            {
                expense.Nos = await ExecuteScalarIntAsync(
                    connection,
                    transaction,
                    "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM PurchaseE",
                    cancellationToken);
            }

            expense.Unique = BuildUnique(expense.AccountName, expense.SuppBillNo);
            await EnsureUniqueAsync(connection, transaction, expense, cancellationToken);

            if (updateExisting)
            {
                const string updateSql = @"
                    UPDATE PurchaseE
                    SET VouType = ?, VouNo = ?, BillDate = ?, AccountName = ?, Head = ?, PartyName = ?,
                        GSTIN = ?, SuppBillNo = ?, SuppDate = ?, [Reverse] = ?, [Tax%] = ?,
                        GrossTotal = ?, TaxableAmount = ?, TaxAmt = ?, AddLessNote = ?, Exclusive = ?,
                        IGST = ?, CGST = ?, SGST = ?, AddLess = ?, NetAmt = ?, Narration = ?,
                        [Unique] = ?, [User] = ?, [Status] = ?, EntryDate = ?, [Time] = ?, Expense = ?
                    WHERE Nos = ?";

                await using var update = CreateHeaderCommand(connection, transaction, updateSql, expense, includeNosAtEnd: true);
                await update.ExecuteNonQueryAsync(cancellationToken);

                await using var deleteLines = connection.CreateCommand();
                deleteLines.Transaction = transaction;
                deleteLines.CommandText = "DELETE FROM PurchaseESubform WHERE Vouno = ?";
                deleteLines.Parameters.Add("?", OleDbType.VarWChar, 50).Value = expense.VouNo;
                await deleteLines.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string insertSql = @"
                    INSERT INTO [PurchaseE]
                    ([VouType], [Nos], [VouNo], [BillDate], [AccountName], [Head], [PartyName], [GSTIN],
                     [SuppBillNo], [SuppDate], [Reverse], [Tax%], [GrossTotal], [TaxableAmount], [TaxAmt],
                     [AddLessNote], [Exclusive], [IGST], [CGST], [SGST], [AddLess], [NetAmt], [Narration],
                     [Unique], [User], [Status], [EntryDate], [Time], [Expense], [Cform], [Cleared], [3b], [InputTaken])
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                await using var insert = CreateHeaderCommand(connection, transaction, insertSql, expense, includeNosAtEnd: false);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            const string lineSql = @"
                INSERT INTO [PurchaseESubform]
                ([Vouno], [SlNo], [ItemName], [HSN], [Qty], [Rate], [RateAmount], [DPer], [DAmt], [GrossAmt],
                 [Count], [TrackIdNo], [TaxRate], [TaxAmount])
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            foreach (var line in expense.Lines)
            {
                await using var lineCommand = connection.CreateCommand();
                lineCommand.Transaction = transaction;
                lineCommand.CommandText = lineSql;

                AddParam(lineCommand, expense.VouNo, OleDbType.VarWChar);
                AddParam(lineCommand, line.SlNo, OleDbType.VarWChar);
                AddParam(lineCommand, line.ItemName, OleDbType.VarWChar);
                AddParam(lineCommand, line.Hsn, OleDbType.VarWChar);
                AddParam(lineCommand, line.Qty, OleDbType.Currency);
                AddParam(lineCommand, line.Rate, OleDbType.Currency);
                AddParam(lineCommand, line.RateAmount, OleDbType.Currency);
                AddParam(lineCommand, 0m, OleDbType.Currency);
                AddParam(lineCommand, 0m, OleDbType.Currency);
                AddParam(lineCommand, line.GrossAmount, OleDbType.Currency);
                AddParam(lineCommand, 1, OleDbType.Integer);
                AddParam(lineCommand, Guid.NewGuid().ToString("N"), OleDbType.VarWChar);
                AddParam(lineCommand, line.TaxRate, OleDbType.Currency);
                AddParam(lineCommand, line.TaxAmount, OleDbType.Currency);

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

    public async Task DeleteAsync(int nos, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PurchaseE SET [Status] = 'Deleted' WHERE Nos = ?";
        command.Parameters.Add("?", OleDbType.Integer).Value = nos;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static OleDbCommand CreateHeaderCommand(
        OleDbConnection connection,
        OleDbTransaction transaction,
        string sql,
        PurchaseExpense expense,
        bool includeNosAtEnd)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        AddParam(command, expense.VouType, OleDbType.VarWChar);
        if (!includeNosAtEnd)
            AddParam(command, expense.Nos, OleDbType.Integer);

        AddParam(command, expense.VouNo, OleDbType.VarWChar);
        AddParam(command, expense.BillDate, OleDbType.Date);
        AddParam(command, expense.AccountName, OleDbType.VarWChar);
        AddParam(command, expense.Head, OleDbType.VarWChar);
        AddParam(command, expense.PartyName, OleDbType.VarWChar);
        AddParam(command, expense.Gstin, OleDbType.VarWChar);
        AddParam(command, expense.SuppBillNo, OleDbType.VarWChar);
        AddParam(command, expense.SuppBillDate, OleDbType.Date);
        AddParam(command, expense.Reverse, OleDbType.Boolean);
        AddParam(command, expense.ReverseTaxRate, OleDbType.Currency);
        AddParam(command, expense.GrossTotal, OleDbType.Currency);
        AddParam(command, expense.TaxableAmount, OleDbType.Currency);
        AddParam(command, expense.TaxAmount, OleDbType.Currency);
        AddParam(command, DbStringOrNull(expense.ExclusiveAccount), OleDbType.VarWChar);
        AddParam(command, expense.Exclusive, OleDbType.Currency);
        AddParam(command, expense.Igst, OleDbType.Currency);
        AddParam(command, expense.Cgst, OleDbType.Currency);
        AddParam(command, expense.Sgst, OleDbType.Currency);
        AddParam(command, expense.RoundedOff, OleDbType.Currency);
        AddParam(command, expense.NetAmount, OleDbType.Currency);
        AddParam(command, expense.Narration, OleDbType.VarWChar);
        AddParam(command, expense.Unique, OleDbType.VarWChar);
        AddParam(command, expense.User, OleDbType.VarWChar);
        AddParam(command, expense.Status, OleDbType.VarWChar);
        AddParam(command, DateTime.Today, OleDbType.Date);
        AddParam(command, DateTime.Now, OleDbType.Date);
        AddParam(command, true, OleDbType.Boolean);

        if (includeNosAtEnd)
        {
            AddParam(command, expense.Nos, OleDbType.Integer);
        }
        else
        {
            AddParam(command, false, OleDbType.Boolean);
            AddParam(command, false, OleDbType.Boolean);
            AddParam(command, false, OleDbType.Boolean);
            AddParam(command, false, OleDbType.Boolean);
        }

        return command;
    }

    private static string BuildUnique(string supplierName, string suppBillNo)
    {
        var raw = $"{suppBillNo.Trim()}|{supplierName.Trim()}|EXP".ToUpperInvariant();
        return raw.Length <= 50 ? raw : raw[..50];
    }

    private static object DbStringOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static async Task EnsureUniqueAsync(
        OleDbConnection connection,
        OleDbTransaction transaction,
        PurchaseExpense expense,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expense.SuppBillNo))
            return;

        const string sql = @"
            SELECT TOP 1 Nos
            FROM PurchaseE
            WHERE [Unique] = ? AND Nos <> ? AND (Status IS NULL OR Status <> 'Deleted')";

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParam(command, expense.Unique, OleDbType.VarWChar);
        AddParam(command, expense.Nos, OleDbType.Integer);
        var existingNos = await command.ExecuteScalarAsync(cancellationToken);
        if (existingNos is not null and not DBNull)
            throw new InvalidOperationException($"A purchase expense already exists for supplier bill '{expense.SuppBillNo}' and supplier '{expense.AccountName}'.");
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
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static void AddParam(OleDbCommand command, object? value, OleDbType type)
    {
        var param = command.CreateParameter();
        param.OleDbType = type;
        param.Value = value ?? DBNull.Value;
        command.Parameters.Add(param);
    }

    private static PurchaseExpense MapPurchaseExpenseHeader(IDataRecord record) => new()
    {
        Nos = record["Nos"] is DBNull ? 0 : Convert.ToInt32(record["Nos"]),
        VouType = record["VouType"]?.ToString() ?? "PurchaseE",
        VouNo = record["VouNo"]?.ToString() ?? string.Empty,
        BillDate = record["BillDate"] is DBNull ? DateTime.Today : Convert.ToDateTime(record["BillDate"]),
        AccountName = record["AccountName"]?.ToString() ?? string.Empty,
        Head = record["Head"]?.ToString() ?? string.Empty,
        PartyName = record["PartyName"]?.ToString() ?? string.Empty,
        Gstin = record["Gstin"]?.ToString() ?? string.Empty,
        SuppBillNo = record["SuppBillNo"]?.ToString() ?? string.Empty,
        SuppBillDate = record["SuppBillDate"] is DBNull ? null : Convert.ToDateTime(record["SuppBillDate"]),
        Reverse = record["Reverse"] is not DBNull && Convert.ToBoolean(record["Reverse"]),
        ReverseTaxRate = record["ReverseTaxRate"] is DBNull ? 0m : Convert.ToDecimal(record["ReverseTaxRate"]),
        GrossTotal = record["GrossTotal"] is DBNull ? 0m : Convert.ToDecimal(record["GrossTotal"]),
        TaxableAmount = record["TaxableAmount"] is DBNull ? 0m : Convert.ToDecimal(record["TaxableAmount"]),
        TaxAmount = record["TaxAmount"] is DBNull ? 0m : Convert.ToDecimal(record["TaxAmount"]),
        ExclusiveAccount = record["ExclusiveAccount"]?.ToString() ?? string.Empty,
        Exclusive = record["Exclusive"] is DBNull ? 0m : Convert.ToDecimal(record["Exclusive"]),
        Igst = record["Igst"] is DBNull ? 0m : Convert.ToDecimal(record["Igst"]),
        Cgst = record["Cgst"] is DBNull ? 0m : Convert.ToDecimal(record["Cgst"]),
        Sgst = record["Sgst"] is DBNull ? 0m : Convert.ToDecimal(record["Sgst"]),
        RoundedOff = record["RoundedOff"] is DBNull ? 0m : Convert.ToDecimal(record["RoundedOff"]),
        NetAmount = record["NetAmount"] is DBNull ? 0m : Convert.ToDecimal(record["NetAmount"]),
        Narration = record["Narration"]?.ToString() ?? string.Empty,
        Unique = record["Unique"]?.ToString() ?? string.Empty,
        User = record["User"]?.ToString() ?? string.Empty,
        Status = record["Status"]?.ToString() ?? "Active"
    };

    private static PurchaseExpenseLine MapPurchaseExpenseLine(IDataRecord record) => new()
    {
        AutoNo = record["AutoNo"] is DBNull ? 0 : Convert.ToInt32(record["AutoNo"]),
        VouNo = record["VouNo"]?.ToString() ?? string.Empty,
        SlNo = record["SlNo"]?.ToString() ?? string.Empty,
        ItemName = record["ItemName"]?.ToString() ?? string.Empty,
        Hsn = record["Hsn"]?.ToString() ?? string.Empty,
        Qty = record["Qty"] is DBNull ? 0m : Convert.ToDecimal(record["Qty"]),
        Rate = record["Rate"] is DBNull ? 0m : Convert.ToDecimal(record["Rate"]),
        RateAmount = record["RateAmount"] is DBNull ? 0m : Convert.ToDecimal(record["RateAmount"]),
        GrossAmount = record["GrossAmount"] is DBNull ? 0m : Convert.ToDecimal(record["GrossAmount"]),
        TaxRate = record["TaxRate"] is DBNull ? 0m : Convert.ToDecimal(record["TaxRate"]),
        TaxAmount = record["TaxAmount"] is DBNull ? 0m : Convert.ToDecimal(record["TaxAmount"])
    };
}
