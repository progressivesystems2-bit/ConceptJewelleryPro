using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessBarcodeCreationRepository : IBarcodeCreationRepository
{
    private readonly string? _databasePath;
    private readonly string _connectionString;
    private readonly DemoBarcodeCreationRepository _demoRepo = new();

    public AccessBarcodeCreationRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = AccessConnectionProvider.GetConnectionString(databasePath);
    }

    public async Task<IReadOnlyList<BarcodeSubformItem>> GetOpeningBarcodesAsync(int count = 30, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 100);
        await AccessBarcodeSubformSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        var sql = $@"
            SELECT TOP {count} *
            FROM BarcodeSubform
            WHERE VouNo = '9'
            ORDER BY BarcodeAutoNo DESC";

        var result = new List<BarcodeSubformItem>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var command = new OleDbCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            var fieldMap = new BarcodeSubformFieldMap(reader);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(fieldMap.MapItem(reader));
            }
        }

        // Fallback: If no barcodes found with VouNo = '9', fetch the latest barcodes overall
        if (result.Count == 0)
        {
            var fallbackSql = $@"
                SELECT TOP {count} *
                FROM BarcodeSubform
                ORDER BY BarcodeAutoNo DESC";

            await using var fallbackCmd = new OleDbCommand(fallbackSql, connection);
            await using var fallbackReader = await fallbackCmd.ExecuteReaderAsync(cancellationToken);
            var fbMap = new BarcodeSubformFieldMap(fallbackReader);
            while (await fallbackReader.ReadAsync(cancellationToken))
            {
                result.Add(fbMap.MapItem(fallbackReader));
            }
        }

        return result;
    }

    public async Task<BarcodeSubformItem?> GetBarcodeByCodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        var rawCode = barcode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(rawCode)) return null;

        if (string.IsNullOrWhiteSpace(_databasePath) || !File.Exists(_databasePath))
        {
            return await _demoRepo.GetBarcodeByCodeAsync(barcode, cancellationToken);
        }

        try
        {
            var d6Code = int.TryParse(rawCode, out var n) ? n.ToString("D6") : rawCode;

            const string sql = @"
                SELECT TOP 1 *
                FROM [BarcodeSubform]
                WHERE ([Barcode] = ? OR [Barcode] = ?)
                  AND ([Status] IS NULL OR [Status] <> 'Deleted')
                ORDER BY [BarcodeAutoNo] DESC";

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = rawCode;
            command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = d6Code;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var fieldMap = new BarcodeSubformFieldMap(reader);
                return fieldMap.MapItem(reader);
            }
        }
        catch
        {
            return await _demoRepo.GetBarcodeByCodeAsync(barcode, cancellationToken);
        }

        return await _demoRepo.GetBarcodeByCodeAsync(barcode, cancellationToken);
    }

    public async Task<string> GetNextBarcodeNumberAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM BarcodeSubform";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var res = await command.ExecuteScalarAsync(cancellationToken);
        var nextNos = res is null or DBNull ? 1 : Convert.ToInt32(res);
        return nextNos.ToString("D6");
    }

    public async Task<int> GetNextBarcodeAutoNoAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IIf(IsNull(Max(BarcodeAutoNo)), 0, Max(BarcodeAutoNo)) + 1 FROM BarcodeSubform";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var res = await command.ExecuteScalarAsync(cancellationToken);
        return res is null or DBNull ? 1 : Convert.ToInt32(res);
    }

    public async Task SaveSingleOpeningBarcodeAsync(BarcodeSubformItem item, bool isUpdate = false, CancellationToken cancellationToken = default)
    {
        await AccessBarcodeSubformSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (OleDbTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            item.VouNo = "9";

            // 1. Ensure VouNo '9' parent header exists in BarcodeCreation before inserting any subform rows!
            await EnsureOpeningHeaderExistsAsync(connection, transaction, cancellationToken);

            // 2. Resolve SupplierName to valid PartyCode for Foreign Key constraint
            var resolvedPartyCode = await ResolvePartyCodeAsync(connection, transaction, item.SupplierName, cancellationToken);
            var resolvedSupplier = string.IsNullOrWhiteSpace(item.SupplierName) ? "OPN" : item.SupplierName;

            if (!isUpdate)
            {
                if (item.Nos <= 0)
                {
                    item.Nos = await ExecuteScalarIntAsync(
                        connection,
                        transaction,
                        "SELECT IIf(IsNull(Max([Nos])), 0, Max([Nos])) + 1 FROM [BarcodeSubform]",
                        cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(item.Barcode))
                {
                    item.Barcode = item.Nos.ToString("D6");
                }

                if (item.BarcodeAutoNo <= 0)
                {
                    item.BarcodeAutoNo = await ExecuteScalarIntAsync(
                        connection,
                        transaction,
                        "SELECT IIf(IsNull(Max([BarcodeAutoNo])), 0, Max([BarcodeAutoNo])) + 1 FROM [BarcodeSubform]",
                        cancellationToken);
                }

                const string insertSql = @"
                    INSERT INTO [BarcodeSubform]
                    ([Barcode], [BarcodeAutoNo], [Nos], [VouNo], [BType], [ItemName], [ItemCode], [Purity], [Description],
                     [Description2], [Size], [Qty], [GrossWt], [StoneWt], [NetWt], [Making], [MakingType], [MPerGr],
                     [StoneValue], [CostTounch], [CostPg], [CostMaking], [CostPrice], [Costrate], [MRP], [Profit],
                     [Supplier], [SupplierName], [SuppInvNo], [SuppDate], [PDate], [PunchedOn], [Photo], [Sti], [Status],
                     [Sale], [Open], [EntryUser], [PrintStickerNo], [BoxNo], [Brand], [Remarks1], [Note], [Godown])
                    VALUES
                    (?, ?, ?, ?, ?, ?, ?, ?, ?,
                     ?, ?, ?, ?, ?, ?, ?, ?, ?,
                     ?, ?, ?, ?, ?, ?, ?, ?,
                     ?, ?, ?, ?, ?, ?, ?, ?, ?,
                     ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                await using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = insertSql;

                BindBarcodeItemParams(insertCmd, item, resolvedSupplier, resolvedPartyCode);
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string updateSql = @"
                    UPDATE [BarcodeSubform]
                    SET [BType] = ?, [ItemName] = ?, [ItemCode] = ?, [Purity] = ?, [Description] = ?,
                        [Description2] = ?, [Size] = ?, [Qty] = ?, [GrossWt] = ?, [StoneWt] = ?, [NetWt] = ?,
                        [Making] = ?, [MakingType] = ?, [MPerGr] = ?, [StoneValue] = ?, [CostTounch] = ?,
                        [CostPg] = ?, [CostMaking] = ?, [CostPrice] = ?, [Costrate] = ?, [MRP] = ?, [Profit] = ?,
                        [Supplier] = ?, [SupplierName] = ?, [SuppInvNo] = ?, [SuppDate] = ?, [PDate] = ?,
                        [Photo] = ?, [Sti] = ?, [Status] = ?, [ModifyUser] = ?, [ModifyDate] = ?, [ModifyTime] = ?,
                        [BoxNo] = ?, [Godown] = ?
                    WHERE [Barcode] = ?";

                await using var updateCmd = connection.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = updateSql;

                AddParam(updateCmd, item.BType, OleDbType.VarWChar);
                AddParam(updateCmd, item.ItemName, OleDbType.VarWChar);
                AddParam(updateCmd, item.ItemCode, OleDbType.VarWChar);
                AddParam(updateCmd, item.Purity, OleDbType.VarWChar);
                AddParam(updateCmd, item.Description, OleDbType.VarWChar);
                AddParam(updateCmd, item.HUID, OleDbType.VarWChar);
                AddParam(updateCmd, item.Size, OleDbType.VarWChar);
                AddParam(updateCmd, item.Qty, OleDbType.Currency);
                AddParam(updateCmd, item.GrossWt, OleDbType.Currency);
                AddParam(updateCmd, item.StoneWt, OleDbType.Currency);
                AddParam(updateCmd, item.NetWt, OleDbType.Currency);
                AddParam(updateCmd, item.Making, OleDbType.Currency);
                AddParam(updateCmd, item.MakingType, OleDbType.VarWChar);
                AddParam(updateCmd, item.MPerGr, OleDbType.Currency);
                AddParam(updateCmd, item.StoneValue, OleDbType.Currency);
                AddParam(updateCmd, item.CostTounch, OleDbType.Currency);
                AddParam(updateCmd, item.CostPg, OleDbType.Currency);
                AddParam(updateCmd, item.CostMaking, OleDbType.Currency);
                AddParam(updateCmd, item.CostPrice, OleDbType.Currency);
                AddParam(updateCmd, item.CostRate, OleDbType.Currency);
                AddParam(updateCmd, item.MRP, OleDbType.Currency);
                AddParam(updateCmd, item.Profit, OleDbType.Currency);
                AddParam(updateCmd, resolvedSupplier, OleDbType.VarWChar);
                AddParam(updateCmd, resolvedPartyCode, OleDbType.VarWChar);
                AddParam(updateCmd, item.SuppInvNo, OleDbType.VarWChar);
                AddParam(updateCmd, item.SuppDate, OleDbType.Date);
                AddParam(updateCmd, item.PDate, OleDbType.Date);
                AddParam(updateCmd, item.Photo, OleDbType.VarWChar);
                AddParam(updateCmd, item.Sti, OleDbType.VarWChar);
                AddParam(updateCmd, item.Status, OleDbType.VarWChar);
                AddParam(updateCmd, item.ModifyUser, OleDbType.VarWChar);
                AddParam(updateCmd, DateTime.Today, OleDbType.Date);
                AddParam(updateCmd, DateTime.Now, OleDbType.Date);
                AddParam(updateCmd, item.BoxNo, OleDbType.VarWChar);
                AddParam(updateCmd, item.Godown, OleDbType.VarWChar);
                AddParam(updateCmd, item.Barcode, OleDbType.VarWChar);

                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Sync TempStock
            await AccessTempStockSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
            await TempStockManager.AddOrUpdateAsync(connection, transaction, item, resolvedSupplier, resolvedPartyCode, cancellationToken);

            // Sync VouNo 9 Header totals in BarcodeCreation
            await SyncVouNo9HeaderAsync(connection, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = "UPDATE BarcodeSubform SET Status = 'Deleted' WHERE Barcode = ?";
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = barcode;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await TempStockManager.RemoveSoldAsync(connection, transaction, new[] { barcode }, cancellationToken);

            await SyncVouNo9HeaderAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<BarcodeCreationHeader>> GetRecentVouchersAsync(int count = 30, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 100);
        var sql = $@"
            SELECT TOP {count}
                   VouNo, Nos, VouDate, ArtisianName, SuppBillNo, GrossWts, NetWts, StoneWts,
                   StoneValues, Qty, MetalRate, Makings, Status, ComputerName, EntryUser,
                   EntryDate, EntryTime, OldVouNo
            FROM BarcodeCreation
            WHERE VouNo <> '9' AND (Status IS NULL OR Status <> 'Deleted')
            ORDER BY VouDate DESC, Nos DESC";

        var result = new List<BarcodeCreationHeader>();
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapBarcodeCreationHeader(reader));
        }

        return result;
    }

    public async Task<BarcodeCreationHeader?> GetVoucherByVouNoAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        const string headerSql = @"
            SELECT VouNo, Nos, VouDate, ArtisianName, SuppBillNo, GrossWts, NetWts, StoneWts,
                   StoneValues, Qty, MetalRate, Makings, Status, ComputerName, EntryUser,
                   EntryDate, EntryTime, OldVouNo
            FROM BarcodeCreation
            WHERE VouNo = ? AND (Status IS NULL OR Status <> 'Deleted')";

        const string linesSql = "SELECT * FROM BarcodeSubform WHERE VouNo = ? ORDER BY BarcodeAutoNo ASC";

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        BarcodeCreationHeader? header = null;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = headerSql;
            cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = vouNo;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                header = MapBarcodeCreationHeader(reader);
            }
        }

        if (header is null)
            return null;

        await using (var linesCmd = connection.CreateCommand())
        {
            linesCmd.CommandText = linesSql;
            linesCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = vouNo;
            await using var reader = await linesCmd.ExecuteReaderAsync(cancellationToken);
            var fieldMap = new BarcodeSubformFieldMap(reader);
            while (await reader.ReadAsync(cancellationToken))
            {
                header.Items.Add(fieldMap.MapItem(reader));
            }
        }

        return header;
    }

    public async Task<int> GetNextVoucherNosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM BarcodeCreation WHERE VouNo <> '9'";
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new OleDbCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 1 : Convert.ToInt32(result);
    }

    public async Task SaveBulkVoucherAsync(BarcodeCreationHeader voucher, bool updateExisting = false, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (OleDbTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!updateExisting && voucher.Nos <= 0)
            {
                voucher.Nos = await ExecuteScalarIntAsync(
                    connection,
                    transaction,
                    "SELECT IIf(IsNull(Max(Nos)), 0, Max(Nos)) + 1 FROM BarcodeCreation WHERE VouNo <> '9'",
                    cancellationToken);
            }

            voucher.GrossWts = voucher.TotalGrossWt;
            voucher.NetWts = voucher.TotalNetWt;
            voucher.StoneWts = voucher.TotalStoneWt;
            voucher.StoneValues = voucher.TotalStoneValue;
            voucher.Qty = voucher.TotalQty;
            voucher.Makings = voucher.TotalMaking;

            if (updateExisting)
            {
                const string updateSql = @"
                    UPDATE [BarcodeCreation]
                    SET [VouDate] = ?, [ArtisianName] = ?, [SuppBillNo] = ?, [GrossWts] = ?, [NetWts] = ?,
                        [StoneWts] = ?, [StoneValues] = ?, [Qty] = ?, [MetalRate] = ?, [Makings] = ?,
                        [Status] = ?, [ComputerName] = ?, [EntryUser] = ?, [EntryDate] = ?, [EntryTime] = ?
                    WHERE [VouNo] = ?";

                await using var updateCmd = connection.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = updateSql;

                AddParam(updateCmd, voucher.VouDate, OleDbType.Date);
                AddParam(updateCmd, voucher.ArtisianName, OleDbType.VarWChar);
                AddParam(updateCmd, voucher.SuppBillNo, OleDbType.VarWChar);
                AddParam(updateCmd, voucher.GrossWts, OleDbType.Currency);
                AddParam(updateCmd, voucher.NetWts, OleDbType.Currency);
                AddParam(updateCmd, voucher.StoneWts, OleDbType.Currency);
                AddParam(updateCmd, voucher.StoneValues, OleDbType.Currency);
                AddParam(updateCmd, voucher.Qty, OleDbType.Currency);
                AddParam(updateCmd, voucher.MetalRate, OleDbType.Currency);
                AddParam(updateCmd, voucher.Makings, OleDbType.Currency);
                AddParam(updateCmd, voucher.Status, OleDbType.VarWChar);
                AddParam(updateCmd, voucher.ComputerName, OleDbType.VarWChar);
                AddParam(updateCmd, voucher.EntryUser, OleDbType.VarWChar);
                AddParam(updateCmd, voucher.EntryDate, OleDbType.Date);
                AddParam(updateCmd, voucher.EntryTime, OleDbType.Date);
                AddParam(updateCmd, voucher.VouNo, OleDbType.VarWChar);

                await updateCmd.ExecuteNonQueryAsync(cancellationToken);

                await using var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = "DELETE FROM [BarcodeSubform] WHERE [VouNo] = ?";
                deleteCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = voucher.VouNo;
                await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                const string insertSql = @"
                    INSERT INTO [BarcodeCreation]
                    ([VouNo], [Nos], [VouDate], [ArtisianName], [SuppBillNo], [GrossWts], [NetWts], [StoneWts],
                     [StoneValues], [Qty], [MetalRate], [Makings], [Status], [ComputerName], [EntryUser],
                     [EntryDate], [EntryTime])
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                await using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = insertSql;

                AddParam(insertCmd, voucher.VouNo, OleDbType.VarWChar);
                AddParam(insertCmd, voucher.Nos, OleDbType.Integer);
                AddParam(insertCmd, voucher.VouDate, OleDbType.Date);
                AddParam(insertCmd, voucher.ArtisianName, OleDbType.VarWChar);
                AddParam(insertCmd, voucher.SuppBillNo, OleDbType.VarWChar);
                AddParam(insertCmd, voucher.GrossWts, OleDbType.Currency);
                AddParam(insertCmd, voucher.NetWts, OleDbType.Currency);
                AddParam(insertCmd, voucher.StoneWts, OleDbType.Currency);
                AddParam(insertCmd, voucher.StoneValues, OleDbType.Currency);
                AddParam(insertCmd, voucher.Qty, OleDbType.Currency);
                AddParam(insertCmd, voucher.MetalRate, OleDbType.Currency);
                AddParam(insertCmd, voucher.Makings, OleDbType.Currency);
                AddParam(insertCmd, voucher.Status, OleDbType.VarWChar);
                AddParam(insertCmd, voucher.ComputerName, OleDbType.VarWChar);
                AddParam(insertCmd, voucher.EntryUser, OleDbType.VarWChar);
                AddParam(insertCmd, voucher.EntryDate, OleDbType.Date);
                AddParam(insertCmd, voucher.EntryTime, OleDbType.Date);

                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var currentMaxNos = await ExecuteScalarIntAsync(
                connection,
                transaction,
                "SELECT IIf(IsNull(Max([Nos])), 0, Max([Nos])) FROM [BarcodeSubform]",
                cancellationToken);

            var currentMaxAuto = await ExecuteScalarIntAsync(
                connection,
                transaction,
                "SELECT IIf(IsNull(Max([BarcodeAutoNo])), 0, Max([BarcodeAutoNo])) FROM [BarcodeSubform]",
                cancellationToken);

            const string insertLineSql = @"
                INSERT INTO [BarcodeSubform]
                ([Barcode], [BarcodeAutoNo], [Nos], [VouNo], [BType], [ItemName], [ItemCode], [Purity], [Description],
                 [Description2], [Size], [Qty], [GrossWt], [StoneWt], [NetWt], [Making], [MakingType], [MPerGr],
                 [StoneValue], [CostTounch], [CostPg], [CostMaking], [CostPrice], [Costrate], [MRP], [Profit],
                 [Supplier], [SupplierName], [SuppInvNo], [SuppDate], [PDate], [PunchedOn], [Photo], [Sti], [Status],
                 [Sale], [Open], [EntryUser], [PrintStickerNo], [BoxNo], [Brand], [Remarks1], [Note], [Godown])
                VALUES
                (?, ?, ?, ?, ?, ?, ?, ?, ?,
                 ?, ?, ?, ?, ?, ?, ?, ?, ?,
                 ?, ?, ?, ?, ?, ?, ?, ?,
                 ?, ?, ?, ?, ?, ?, ?, ?, ?,
                 ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            foreach (var item in voucher.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ItemName) && item.GrossWt == 0 && item.Qty == 0)
                    continue;

                item.VouNo = voucher.VouNo;
                if (item.Nos <= 0)
                {
                    currentMaxNos++;
                    item.Nos = currentMaxNos;
                }
                if (string.IsNullOrWhiteSpace(item.Barcode))
                {
                    item.Barcode = item.Nos.ToString("D6");
                }
                if (item.BarcodeAutoNo <= 0)
                {
                    currentMaxAuto++;
                    item.BarcodeAutoNo = currentMaxAuto;
                }

                var itemSupplierName = string.IsNullOrWhiteSpace(item.SupplierName) ? voucher.ArtisianName : item.SupplierName;
                var resolvedPartyCode = await ResolvePartyCodeAsync(connection, transaction, itemSupplierName, cancellationToken);
                var resolvedSupplier = string.IsNullOrWhiteSpace(itemSupplierName) ? "OPN" : itemSupplierName;

                item.SupplierName = itemSupplierName;
                item.SuppInvNo = string.IsNullOrWhiteSpace(item.SuppInvNo) ? voucher.SuppBillNo : item.SuppInvNo;
                item.PDate = voucher.VouDate;

                await using var lineCmd = connection.CreateCommand();
                lineCmd.Transaction = transaction;
                lineCmd.CommandText = insertLineSql;

                BindBarcodeItemParams(lineCmd, item, resolvedSupplier, resolvedPartyCode);
                await lineCmd.ExecuteNonQueryAsync(cancellationToken);

                // Sync to TempStock
                await TempStockManager.AddOrUpdateAsync(connection, transaction, item, resolvedSupplier, resolvedPartyCode, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteVoucherAsync(string vouNo, CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (OleDbTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Collect barcodes to remove from TempStock
            var barcodesToRemove = new List<string>();
            const string getBcSql = "SELECT [Barcode] FROM [BarcodeSubform] WHERE [VouNo] = ?";
            await using (var getCmd = connection.CreateCommand())
            {
                getCmd.Transaction = transaction;
                getCmd.CommandText = getBcSql;
                getCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = vouNo;
                await using var r = await getCmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    var bc = r[0]?.ToString();
                    if (!string.IsNullOrEmpty(bc)) barcodesToRemove.Add(bc);
                }
            }

            await using var cmd1 = connection.CreateCommand();
            cmd1.Transaction = transaction;
            cmd1.CommandText = "UPDATE [BarcodeCreation] SET [Status] = 'Deleted' WHERE [VouNo] = ?";
            cmd1.Parameters.Add("?", OleDbType.VarWChar, 50).Value = vouNo;
            await cmd1.ExecuteNonQueryAsync(cancellationToken);

            await using var cmd2 = connection.CreateCommand();
            cmd2.Transaction = transaction;
            cmd2.CommandText = "DELETE FROM [BarcodeSubform] WHERE [VouNo] = ?";
            cmd2.Parameters.Add("?", OleDbType.VarWChar, 50).Value = vouNo;
            await cmd2.ExecuteNonQueryAsync(cancellationToken);

            if (barcodesToRemove.Count > 0)
            {
                await TempStockManager.RemoveSoldAsync(connection, transaction, barcodesToRemove, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GetStickerFormatsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<string>();
        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var cmd = new OleDbCommand("SELECT [Sticker] FROM [Sticker]", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var s = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(s))
                    result.Add(s);
            }
        }
        catch
        {
            result.AddRange(new[] { "StickerWt", "StickerPC", "No Making", "With Making", "Diamond" });
        }

        if (result.Count == 0)
        {
            result.AddRange(new[] { "StickerWt", "StickerPC", "No Making", "With Making", "Diamond" });
        }

        if (!result.Any(s => string.Equals(s, "StickerWt", StringComparison.OrdinalIgnoreCase)))
        {
            result.Insert(0, "StickerWt");
        }
        if (!result.Any(s => string.Equals(s, "StickerPC", StringComparison.OrdinalIgnoreCase)))
        {
            result.Insert(1, "StickerPC");
        }

        return result;
    }

    private async Task SyncVouNo9HeaderAsync(OleDbConnection connection, OleDbTransaction transaction, CancellationToken cancellationToken)
    {
        await EnsureOpeningHeaderExistsAsync(connection, transaction, cancellationToken);
    }

    private async Task EnsureOpeningHeaderExistsAsync(
        OleDbConnection connection,
        OleDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sumSql = @"
            SELECT Sum([GrossWt]), Sum([NetWt]), Sum([StoneWt]), Sum([StoneValue]), Sum([Qty]), Sum([Making])
            FROM [BarcodeSubform]
            WHERE [VouNo] = '9'";

        decimal sumGross = 0, sumStoneWt = 0, sumNet = 0, sumStoneVal = 0, sumQty = 0, sumMaking = 0;
        await using (var aggCmd = connection.CreateCommand())
        {
            aggCmd.Transaction = transaction;
            aggCmd.CommandText = sumSql;
            await using var reader = await aggCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                sumGross = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0));
                sumNet = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                sumStoneWt = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                sumStoneVal = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));
                sumQty = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4));
                sumMaking = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5));
            }
        }

        const string checkSql = "SELECT COUNT(*) FROM [BarcodeCreation] WHERE [VouNo] = '9'";
        var exists = 0;
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.Transaction = transaction;
            checkCmd.CommandText = checkSql;
            var res = await checkCmd.ExecuteScalarAsync(cancellationToken);
            exists = res is null or DBNull ? 0 : Convert.ToInt32(res);
        }

        if (exists > 0)
        {
            const string updSql = @"
                UPDATE [BarcodeCreation]
                SET [GrossWts] = ?, [NetWts] = ?, [StoneWts] = ?, [StoneValues] = ?, [Qty] = ?, [Makings] = ?
                WHERE [VouNo] = '9'";

            await using var updCmd = connection.CreateCommand();
            updCmd.Transaction = transaction;
            updCmd.CommandText = updSql;
            AddParam(updCmd, sumGross, OleDbType.Currency);
            AddParam(updCmd, sumNet, OleDbType.Currency);
            AddParam(updCmd, sumStoneWt, OleDbType.Currency);
            AddParam(updCmd, sumStoneVal, OleDbType.Currency);
            AddParam(updCmd, sumQty, OleDbType.Currency);
            AddParam(updCmd, sumMaking, OleDbType.Currency);
            await updCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string insSql = @"
                INSERT INTO [BarcodeCreation]
                ([VouNo], [Nos], [VouDate], [ArtisianName], [SuppBillNo], [GrossWts], [NetWts], [StoneWts], [StoneValues], [Qty], [Makings], [Status], [EntryDate], [EntryTime])
                VALUES ('9', 0, ?, 'OPN', '', ?, ?, ?, ?, ?, ?, 'Saved', ?, ?)";

            await using var insCmd = connection.CreateCommand();
            insCmd.Transaction = transaction;
            insCmd.CommandText = insSql;
            AddParam(insCmd, DateTime.Today, OleDbType.Date);
            AddParam(insCmd, sumGross, OleDbType.Currency);
            AddParam(insCmd, sumNet, OleDbType.Currency);
            AddParam(insCmd, sumStoneWt, OleDbType.Currency);
            AddParam(insCmd, sumStoneVal, OleDbType.Currency);
            AddParam(insCmd, sumQty, OleDbType.Currency);
            AddParam(insCmd, sumMaking, OleDbType.Currency);
            AddParam(insCmd, DateTime.Today, OleDbType.Date);
            AddParam(insCmd, DateTime.Now, OleDbType.Date);
            await insCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<string> ResolvePartyCodeAsync(
        OleDbConnection connection,
        OleDbTransaction transaction,
        string? supplierNameOrAccount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(supplierNameOrAccount) ||
            string.Equals(supplierNameOrAccount, "OPN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplierNameOrAccount, "Opening", StringComparison.OrdinalIgnoreCase) ||
            supplierNameOrAccount == "-")
        {
            return "-";
        }

        const string sql = "SELECT TOP 1 [PartyCode] FROM [AccountCreation] WHERE [PartyCode] = ? OR [AccountName] = ?";
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 100).Value = supplierNameOrAccount;
        cmd.Parameters.Add("?", OleDbType.VarWChar, 100).Value = supplierNameOrAccount;
        var res = await cmd.ExecuteScalarAsync(cancellationToken);
        if (res is not null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
        {
            return res.ToString()!;
        }

        return "-";
    }

    private static void BindBarcodeItemParams(OleDbCommand cmd, BarcodeSubformItem item, string supplier, string partyCode)
    {
        AddParam(cmd, item.Barcode, OleDbType.VarWChar);
        AddParam(cmd, item.BarcodeAutoNo, OleDbType.Integer);
        AddParam(cmd, item.Nos, OleDbType.Integer);
        AddParam(cmd, item.VouNo, OleDbType.VarWChar);
        AddParam(cmd, item.BType, OleDbType.VarWChar);
        AddParam(cmd, item.ItemName, OleDbType.VarWChar);
        AddParam(cmd, item.ItemCode, OleDbType.VarWChar);
        AddParam(cmd, item.Purity, OleDbType.VarWChar);
        AddParam(cmd, item.Description, OleDbType.VarWChar);
        AddParam(cmd, item.HUID, OleDbType.VarWChar);
        AddParam(cmd, item.Size, OleDbType.VarWChar);
        AddParam(cmd, item.Qty, OleDbType.Currency);
        AddParam(cmd, item.GrossWt, OleDbType.Currency);
        AddParam(cmd, item.StoneWt, OleDbType.Currency);
        AddParam(cmd, item.NetWt, OleDbType.Currency);
        AddParam(cmd, item.Making, OleDbType.Currency);
        AddParam(cmd, item.MakingType, OleDbType.VarWChar);
        AddParam(cmd, item.MPerGr, OleDbType.Currency);
        AddParam(cmd, item.StoneValue, OleDbType.Currency);
        AddParam(cmd, item.CostTounch, OleDbType.Currency);
        AddParam(cmd, item.CostPg, OleDbType.Currency);
        AddParam(cmd, item.CostMaking, OleDbType.Currency);
        AddParam(cmd, item.CostPrice, OleDbType.Currency);
        AddParam(cmd, item.CostRate, OleDbType.Currency);
        AddParam(cmd, item.MRP, OleDbType.Currency);
        AddParam(cmd, item.Profit, OleDbType.Currency);
        AddParam(cmd, supplier, OleDbType.VarWChar);
        AddParam(cmd, partyCode, OleDbType.VarWChar);
        AddParam(cmd, item.SuppInvNo, OleDbType.VarWChar);
        AddParam(cmd, item.SuppDate, OleDbType.Date);
        AddParam(cmd, item.PDate, OleDbType.Date);
        AddParam(cmd, item.PunchedOn == default ? DateTime.Now : item.PunchedOn, OleDbType.Date);
        AddParam(cmd, item.Photo, OleDbType.VarWChar);
        AddParam(cmd, item.Sti, OleDbType.VarWChar);
        AddParam(cmd, item.Status, OleDbType.VarWChar);
        AddParam(cmd, item.Sale, OleDbType.Boolean);
        AddParam(cmd, item.Open, OleDbType.Boolean);
        AddParam(cmd, item.EntryUser, OleDbType.VarWChar);
        AddParam(cmd, item.PrintStickerNo, OleDbType.Integer);
        AddParam(cmd, item.BoxNo, OleDbType.VarWChar);
        AddParam(cmd, item.Brand, OleDbType.VarWChar);
        AddParam(cmd, item.Remarks1, OleDbType.VarWChar);
        AddParam(cmd, item.Note, OleDbType.VarWChar);
        AddParam(cmd, item.Godown, OleDbType.VarWChar);
    }

    private static BarcodeCreationHeader MapBarcodeCreationHeader(IDataRecord r)
    {
        return new BarcodeCreationHeader
        {
            VouNo = GetStringDirect(r, "VouNo"),
            Nos = GetIntDirect(r, "Nos"),
            VouDate = GetDateTimeDirect(r, "VouDate") ?? DateTime.Today,
            ArtisianName = GetStringDirect(r, "ArtisianName"),
            SuppBillNo = GetStringDirect(r, "SuppBillNo"),
            GrossWts = GetDecimalDirect(r, "GrossWts"),
            NetWts = GetDecimalDirect(r, "NetWts"),
            StoneWts = GetDecimalDirect(r, "StoneWts"),
            StoneValues = GetDecimalDirect(r, "StoneValues"),
            Qty = GetDecimalDirect(r, "Qty"),
            MetalRate = GetDecimalDirect(r, "MetalRate"),
            Makings = GetDecimalDirect(r, "Makings"),
            Status = GetStringDirect(r, "Status", "Saved"),
            ComputerName = GetStringDirect(r, "ComputerName"),
            EntryUser = GetStringDirect(r, "EntryUser"),
            EntryDate = GetDateTimeDirect(r, "EntryDate") ?? DateTime.Today,
            EntryTime = GetDateTimeDirect(r, "EntryTime") ?? DateTime.Now,
            OldVouNo = GetStringDirect(r, "OldVouNo")
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

    private static readonly object _dbWriteLock = new();

    public async Task UpdateBarcodePhotoAsync(string barcode, string photoPath, CancellationToken cancellationToken = default)
    {
        var rawCode = barcode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(rawCode)) return;

        if (string.IsNullOrWhiteSpace(_databasePath) || !File.Exists(_databasePath))
        {
            await _demoRepo.UpdateBarcodePhotoAsync(barcode, photoPath, cancellationToken);
            return;
        }

        await Task.Run(() =>
        {
            lock (_dbWriteLock)
            {
                try
                {
                    var d6Code = int.TryParse(rawCode, out var n) ? n.ToString("D6") : rawCode;
                    const string sql = "UPDATE [BarcodeSubform] SET [Photo] = ? WHERE ([Barcode] = ? OR [Barcode] = ?)";

                    using var connection = new OleDbConnection(_connectionString);
                    connection.Open();

                    using var command = new OleDbCommand(sql, connection);
                    command.Parameters.Add("?", OleDbType.VarWChar, 255).Value = photoPath ?? string.Empty;
                    command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = rawCode;
                    command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = d6Code;

                    command.ExecuteNonQuery();
                }
                catch
                {
                    // Catch exception safely to prevent COM apartment crashing
                }
            }
        }, cancellationToken);
    }

    public Task UpdateBarcodeAdvancedAsync(string barcode, decimal? mrp, string? size, bool reprintSticker, CancellationToken cancellationToken = default)
    {
        return UpdateBarcodeAdvancedAsync(barcode, mrp, size, reprintSticker, null, null, null, cancellationToken);
    }

    public Task UpdateBarcodeAdvancedAsync(string barcode, decimal? mrp, string? size, bool reprintSticker, string? stickerFormat, CancellationToken cancellationToken = default)
    {
        return UpdateBarcodeAdvancedAsync(barcode, mrp, size, reprintSticker, stickerFormat, null, null, cancellationToken);
    }

    public async Task UpdateBarcodeAdvancedAsync(string barcode, decimal? mrp, string? size, bool reprintSticker, string? stickerFormat, string? bType, decimal? making, CancellationToken cancellationToken = default)
    {
        var rawCode = barcode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(rawCode)) return;

        if (string.IsNullOrWhiteSpace(_databasePath) || !File.Exists(_databasePath))
        {
            await _demoRepo.UpdateBarcodeAdvancedAsync(barcode, mrp, size, reprintSticker, stickerFormat, bType, making, cancellationToken);
            return;
        }

        await Task.Run(() =>
        {
            lock (_dbWriteLock)
            {
                try
                {
                    var d6Code = int.TryParse(rawCode, out var n) ? n.ToString("D6") : rawCode;

                    var setClauses = new List<string>();
                    var parameters = new List<(string name, OleDbType type, object val)>();

                    if (!string.IsNullOrWhiteSpace(bType))
                    {
                        setClauses.Add("[BType] = ?");
                        parameters.Add(("@BType", OleDbType.VarWChar, bType.Trim().ToUpperInvariant()));
                    }

                    if (mrp.HasValue)
                    {
                        setClauses.Add("[MRP] = ?");
                        parameters.Add(("@MRP", OleDbType.Currency, mrp.Value));
                    }

                    if (making.HasValue)
                    {
                        setClauses.Add("[Making] = ?");
                        parameters.Add(("@Making", OleDbType.Currency, making.Value));
                    }

                    if (!string.IsNullOrEmpty(size))
                    {
                        setClauses.Add("[Size] = ?");
                        parameters.Add(("@Size", OleDbType.VarWChar, size));
                    }

                    if (reprintSticker)
                    {
                        setClauses.Add("[PrintStickerNo] = ?");
                        parameters.Add(("@PrintStickerNo", OleDbType.Numeric, 1));
                    }

                    if (!string.IsNullOrWhiteSpace(stickerFormat))
                    {
                        setClauses.Add("[Sti] = ?");
                        parameters.Add(("@Sti", OleDbType.VarWChar, stickerFormat.Trim()));
                    }

                    if (setClauses.Count == 0) return;

                    var sql = $"UPDATE [BarcodeSubform] SET {string.Join(", ", setClauses)} WHERE ([Barcode] = ? OR [Barcode] = ?)";

                    using var connection = new OleDbConnection(_connectionString);
                    connection.Open();

                    using var command = new OleDbCommand(sql, connection);
                    foreach (var (_, type, val) in parameters)
                    {
                        command.Parameters.Add("?", type).Value = val ?? DBNull.Value;
                    }
                    command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = rawCode;
                    command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = d6Code;

                    command.ExecuteNonQuery();
                }
                catch
                {
                    // Catch exception safely
                }
            }
        }, cancellationToken);
    }

    private sealed class BarcodeSubformFieldMap
    {
        private readonly int _idxBarcode;
        private readonly int _idxBarcodeAutoNo;
        private readonly int _idxNos;
        private readonly int _idxVouNo;
        private readonly int _idxBType;
        private readonly int _idxItemName;
        private readonly int _idxItemCode;
        private readonly int _idxPurity;
        private readonly int _idxDescription;
        private readonly int _idxDescription2;
        private readonly int _idxSize;
        private readonly int _idxQty;
        private readonly int _idxGrossWt;
        private readonly int _idxStoneWt;
        private readonly int _idxNetWt;
        private readonly int _idxMaking;
        private readonly int _idxMakingType;
        private readonly int _idxMPerGr;
        private readonly int _idxStoneValue;
        private readonly int _idxCostTounch;
        private readonly int _idxCostPg;
        private readonly int _idxCostMaking;
        private readonly int _idxCostPrice;
        private readonly int _idxCostrate;
        private readonly int _idxMRP;
        private readonly int _idxProfit;
        private readonly int _idxSupplierName;
        private readonly int _idxSupplier;
        private readonly int _idxSuppInvNo;
        private readonly int _idxSuppDate;
        private readonly int _idxPDate;
        private readonly int _idxPunchedOn;
        private readonly int _idxPhoto;
        private readonly int _idxSti;
        private readonly int _idxStatus;
        private readonly int _idxSale;
        private readonly int _idxOpen;
        private readonly int _idxEntryUser;
        private readonly int _idxModifyUser;
        private readonly int _idxModifyDate;
        private readonly int _idxModifyTime;
        private readonly int _idxPrintStickerNo;
        private readonly int _idxBoxNo;
        private readonly int _idxBrand;
        private readonly int _idxRemarks1;
        private readonly int _idxNote;
        private readonly int _idxGodown;

        public BarcodeSubformFieldMap(IDataRecord r)
        {
            _idxBarcode = FindOrdinal(r, "Barcode");
            _idxBarcodeAutoNo = FindOrdinal(r, "BarcodeAutoNo");
            _idxNos = FindOrdinal(r, "Nos");
            _idxVouNo = FindOrdinal(r, "VouNo");
            _idxBType = FindOrdinal(r, "BType");
            _idxItemName = FindOrdinal(r, "ItemName");
            _idxItemCode = FindOrdinal(r, "ItemCode");
            _idxPurity = FindOrdinal(r, "Purity");
            _idxDescription = FindOrdinal(r, "Description");
            _idxDescription2 = FindOrdinal(r, "Description2");
            _idxSize = FindOrdinal(r, "Size");
            _idxQty = FindOrdinal(r, "Qty");
            _idxGrossWt = FindOrdinal(r, "GrossWt");
            _idxStoneWt = FindOrdinal(r, "StoneWt");
            _idxNetWt = FindOrdinal(r, "NetWt");
            _idxMaking = FindOrdinal(r, "Making");
            _idxMakingType = FindOrdinal(r, "MakingType");
            _idxMPerGr = FindOrdinal(r, "MPerGr");
            _idxStoneValue = FindOrdinal(r, "StoneValue");
            _idxCostTounch = FindOrdinal(r, "CostTounch");
            _idxCostPg = FindOrdinal(r, "CostPg");
            _idxCostMaking = FindOrdinal(r, "CostMaking");
            _idxCostPrice = FindOrdinal(r, "CostPrice");
            _idxCostrate = FindOrdinal(r, "Costrate");
            _idxMRP = FindOrdinal(r, "MRP");
            _idxProfit = FindOrdinal(r, "Profit");
            _idxSupplierName = FindOrdinal(r, "SupplierName");
            _idxSupplier = FindOrdinal(r, "Supplier");
            _idxSuppInvNo = FindOrdinal(r, "SuppInvNo");
            _idxSuppDate = FindOrdinal(r, "SuppDate");
            _idxPDate = FindOrdinal(r, "PDate");
            _idxPunchedOn = FindOrdinal(r, "PunchedOn");
            _idxPhoto = FindOrdinal(r, "Photo");
            _idxSti = FindOrdinal(r, "Sti");
            _idxStatus = FindOrdinal(r, "Status");
            _idxSale = FindOrdinal(r, "Sale");
            _idxOpen = FindOrdinal(r, "Open");
            _idxEntryUser = FindOrdinal(r, "EntryUser");
            _idxModifyUser = FindOrdinal(r, "ModifyUser");
            _idxModifyDate = FindOrdinal(r, "ModifyDate");
            _idxModifyTime = FindOrdinal(r, "ModifyTime");
            _idxPrintStickerNo = FindOrdinal(r, "PrintStickerNo");
            _idxBoxNo = FindOrdinal(r, "BoxNo");
            _idxBrand = FindOrdinal(r, "Brand");
            _idxRemarks1 = FindOrdinal(r, "Remarks1");
            _idxNote = FindOrdinal(r, "Note");
            _idxGodown = FindOrdinal(r, "Godown");
        }

        public BarcodeSubformItem MapItem(IDataRecord r)
        {
            var partyCode = GetStr(r, _idxSupplierName);
            var supplier = GetStr(r, _idxSupplier);
            if (string.IsNullOrWhiteSpace(partyCode)) partyCode = supplier;
            if (string.IsNullOrWhiteSpace(supplier)) supplier = partyCode;
            if (string.IsNullOrWhiteSpace(partyCode)) partyCode = "OPN";
            if (string.IsNullOrWhiteSpace(supplier)) supplier = "OPN";

            return new BarcodeSubformItem
            {
                Barcode = GetStr(r, _idxBarcode),
                BarcodeAutoNo = GetIntVal(r, _idxBarcodeAutoNo),
                Nos = GetIntVal(r, _idxNos),
                VouNo = GetStr(r, _idxVouNo, "9"),
                BType = GetStr(r, _idxBType, "WT"),
                ItemName = GetStr(r, _idxItemName),
                ItemCode = GetStr(r, _idxItemCode),
                Purity = GetStr(r, _idxPurity),
                Description = GetStr(r, _idxDescription),
                HUID = GetStr(r, _idxDescription2),
                Size = GetStr(r, _idxSize, "-"),
                Qty = GetDecVal(r, _idxQty, 1m),
                GrossWt = GetDecVal(r, _idxGrossWt),
                StoneWt = GetDecVal(r, _idxStoneWt),
                NetWt = GetDecVal(r, _idxNetWt),
                Making = GetDecVal(r, _idxMaking),
                MakingType = GetStr(r, _idxMakingType, "GRAM"),
                MPerGr = GetDecVal(r, _idxMPerGr),
                StoneValue = GetDecVal(r, _idxStoneValue),
                CostTounch = GetDecVal(r, _idxCostTounch),
                CostPg = GetDecVal(r, _idxCostPg),
                CostMaking = GetDecVal(r, _idxCostMaking),
                CostPrice = GetDecVal(r, _idxCostPrice),
                CostRate = GetDecVal(r, _idxCostrate),
                MRP = GetDecVal(r, _idxMRP),
                Profit = GetDecVal(r, _idxProfit, 1.65m),
                SupplierName = supplier,
                PartyCode = partyCode,
                SuppInvNo = GetStr(r, _idxSuppInvNo),
                SuppDate = GetDt(r, _idxSuppDate),
                PDate = GetDt(r, _idxPDate) ?? DateTime.Today,
                PunchedOn = GetDt(r, _idxPunchedOn) ?? DateTime.Now,
                Photo = GetStr(r, _idxPhoto),
                Sti = GetStr(r, _idxSti, "StickerWt"),
                Status = GetStr(r, _idxStatus, "Active"),
                Sale = GetBoolVal(r, _idxSale),
                Open = GetBoolVal(r, _idxOpen),
                EntryUser = GetStr(r, _idxEntryUser),
                ModifyUser = GetStr(r, _idxModifyUser),
                ModifyDate = GetDt(r, _idxModifyDate),
                ModifyTime = GetDt(r, _idxModifyTime),
                PrintStickerNo = GetIntVal(r, _idxPrintStickerNo),
                BoxNo = GetStr(r, _idxBoxNo),
                Brand = GetStr(r, _idxBrand),
                Remarks1 = GetStr(r, _idxRemarks1),
                Note = GetStr(r, _idxNote),
                Godown = GetStr(r, _idxGodown)
            };
        }

        private static int FindOrdinal(IDataRecord r, string col)
        {
            for (var i = 0; i < r.FieldCount; i++)
            {
                if (string.Equals(r.GetName(i), col, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string GetStr(IDataRecord r, int idx, string fallback = "")
        {
            if (idx < 0 || r.IsDBNull(idx)) return fallback;
            return Convert.ToString(r.GetValue(idx)) ?? fallback;
        }

        private static int GetIntVal(IDataRecord r, int idx, int fallback = 0)
        {
            if (idx < 0 || r.IsDBNull(idx)) return fallback;
            return Convert.ToInt32(r.GetValue(idx));
        }

        private static decimal GetDecVal(IDataRecord r, int idx, decimal fallback = 0m)
        {
            if (idx < 0 || r.IsDBNull(idx)) return fallback;
            return Convert.ToDecimal(r.GetValue(idx));
        }

        private static bool GetBoolVal(IDataRecord r, int idx, bool fallback = false)
        {
            if (idx < 0 || r.IsDBNull(idx)) return fallback;
            return Convert.ToBoolean(r.GetValue(idx));
        }

        private static DateTime? GetDt(IDataRecord r, int idx)
        {
            if (idx < 0 || r.IsDBNull(idx)) return null;
            return Convert.ToDateTime(r.GetValue(idx));
        }
    }
}
