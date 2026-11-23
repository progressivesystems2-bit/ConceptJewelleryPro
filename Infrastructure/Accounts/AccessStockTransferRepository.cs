using System.Data;
using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class AccessStockTransferRepository : IStockTransferRepository
{
    private readonly string _connectionString;
    private bool _schemaEnsured;
    private readonly object _schemaLock = new();

    public AccessStockTransferRepository(string databasePath)
    {
        _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
    }

    private async Task EnsureSchemaAsync(OleDbConnection connection, CancellationToken cancellationToken = default)
    {
        if (_schemaEnsured) return;

        try
        {
            await ExecuteNonQuerySilentlyAsync(connection, @"
                CREATE TABLE [StockTransfer] (
                    [TransferNo] TEXT(50) PRIMARY KEY,
                    [TransferDate] DATETIME,
                    [FromLocation] TEXT(50),
                    [ToLocation] TEXT(50),
                    [TransferType] TEXT(50),
                    [TotalQty] DOUBLE,
                    [TotalGrossWt] DOUBLE,
                    [TotalStoneWt] DOUBLE,
                    [TotalNetWt] DOUBLE,
                    [TotalStoneValue] CURRENCY,
                    [TotalMaking] CURRENCY,
                    [TotalMRP] CURRENCY,
                    [Remarks] TEXT(255),
                    [EntryUser] TEXT(50),
                    [EntryDate] DATETIME,
                    [Status] TEXT(25)
                )", cancellationToken);

            await ExecuteNonQuerySilentlyAsync(connection, @"
                CREATE TABLE [StockTransferSubform] (
                    [AutoNo] AUTOINCREMENT PRIMARY KEY,
                    [TransferNo] TEXT(50),
                    [Barcode] TEXT(50),
                    [ItemName] TEXT(100),
                    [Qty] DOUBLE,
                    [GrossWt] DOUBLE,
                    [StoneWt] DOUBLE,
                    [NetWt] DOUBLE,
                    [StoneValue] CURRENCY,
                    [Making] CURRENCY,
                    [MRP] CURRENCY,
                    [Purity] TEXT(50),
                    [BoxNo] TEXT(50),
                    [Description] TEXT(255),
                    [CostPrice] CURRENCY
                )", cancellationToken);

            lock (_schemaLock)
            {
                _schemaEnsured = true;
            }
        }
        catch
        {
            // Ignore if tables already exist
        }
    }

    private static async Task ExecuteNonQuerySilentlyAsync(OleDbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = new OleDbCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Table/column already exists or operation not permitted
        }
    }

    public async Task<string> GetNextTransferNoAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        const string sql = "SELECT [TransferNo] FROM [StockTransfer] ORDER BY [TransferNo] DESC";
        await using var command = new OleDbCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        int maxSeq = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            var no = reader[0]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(no)) continue;

            if (no.StartsWith("ST-", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = no[3..];
                if (int.TryParse(numStr, out var n) && n > maxSeq)
                {
                    maxSeq = n;
                }
            }
            else if (int.TryParse(no, out var n) && n > maxSeq)
            {
                maxSeq = n;
            }
        }

        return $"ST-{(maxSeq + 1):D5}";
    }

    public async Task<StockTransfer?> GetByTransferNoAsync(string transferNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transferNo)) return null;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        const string sql = "SELECT [TransferNo], [TransferDate], [FromLocation], [ToLocation], [TransferType], [TotalQty], [TotalGrossWt], [TotalStoneWt], [TotalNetWt], [TotalStoneValue], [TotalMaking], [TotalMRP], [Remarks], [EntryUser], [EntryDate], [Status] FROM [StockTransfer] WHERE [TransferNo] = ?";
        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transferNo.Trim() });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapTransferHeader(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<StockTransfer>> GetAllTransfersAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var list = new List<StockTransfer>();

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        var sql = "SELECT [TransferNo], [TransferDate], [FromLocation], [ToLocation], [TransferType], [TotalQty], [TotalGrossWt], [TotalStoneWt], [TotalNetWt], [TotalStoneValue], [TotalMaking], [TotalMRP], [Remarks], [EntryUser], [EntryDate], [Status] FROM [StockTransfer] WHERE 1=1";
        var cmd = new OleDbCommand { Connection = connection };

        if (fromDate.HasValue)
        {
            sql += " AND [TransferDate] >= ?";
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = fromDate.Value.Date });
        }
        if (toDate.HasValue)
        {
            sql += " AND [TransferDate] <= ?";
            cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = toDate.Value.Date.AddDays(1).AddSeconds(-1) });
        }

        sql += " ORDER BY [TransferDate] DESC, [TransferNo] DESC";
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapTransferHeader(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<StockTransferItem>> GetTransferItemsAsync(string transferNo, CancellationToken cancellationToken = default)
    {
        var list = new List<StockTransferItem>();
        if (string.IsNullOrWhiteSpace(transferNo)) return list;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        const string sql = "SELECT [AutoNo], [TransferNo], [Barcode], [ItemName], [Qty], [GrossWt], [StoneWt], [NetWt], [StoneValue], [Making], [MRP], [Purity], [BoxNo], [Description], [CostPrice] FROM [StockTransferSubform] WHERE [TransferNo] = ? ORDER BY [AutoNo]";
        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transferNo.Trim() });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapTransferItem(reader));
        }

        return list;
    }

    public async Task SaveTransferAsync(StockTransfer transfer, IEnumerable<StockTransferItem> items, CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();

        // Recalculate totals
        transfer.TotalQty = itemList.Sum(x => x.Qty);
        transfer.TotalGrossWt = Math.Round(itemList.Sum(x => x.GrossWt), 3);
        transfer.TotalStoneWt = Math.Round(itemList.Sum(x => x.StoneWt), 3);
        transfer.TotalNetWt = Math.Round(itemList.Sum(x => x.NetWt), 3);
        transfer.TotalStoneValue = Math.Round(itemList.Sum(x => x.StoneValue), 2);
        transfer.TotalMaking = Math.Round(itemList.Sum(x => x.Making), 2);
        transfer.TotalMRP = Math.Round(itemList.Sum(x => x.MRP), 2);
        transfer.EntryDate ??= DateTime.Now;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        // Check if header exists
        bool exists = false;
        {
            await using var chkCmd = new OleDbCommand("SELECT COUNT(*) FROM [StockTransfer] WHERE [TransferNo] = ?", connection);
            chkCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.TransferNo.Trim() });
            var count = Convert.ToInt32(await chkCmd.ExecuteScalarAsync(cancellationToken));
            exists = count > 0;
        }

        if (exists)
        {
            const string updateSql = @"
                UPDATE [StockTransfer]
                SET [TransferDate] = ?,
                    [FromLocation] = ?,
                    [ToLocation] = ?,
                    [TransferType] = ?,
                    [TotalQty] = ?,
                    [TotalGrossWt] = ?,
                    [TotalStoneWt] = ?,
                    [TotalNetWt] = ?,
                    [TotalStoneValue] = ?,
                    [TotalMaking] = ?,
                    [TotalMRP] = ?,
                    [Remarks] = ?,
                    [EntryUser] = ?,
                    [EntryDate] = ?,
                    [Status] = ?
                WHERE [TransferNo] = ?";

            await using var upCmd = new OleDbCommand(updateSql, connection);
            AddTransferHeaderParameters(upCmd, transfer);
            upCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.TransferNo.Trim() });
            await upCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string insertSql = @"
                INSERT INTO [StockTransfer] (
                    [TransferDate], [FromLocation], [ToLocation], [TransferType],
                    [TotalQty], [TotalGrossWt], [TotalStoneWt], [TotalNetWt],
                    [TotalStoneValue], [TotalMaking], [TotalMRP], [Remarks],
                    [EntryUser], [EntryDate], [Status], [TransferNo]
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            await using var inCmd = new OleDbCommand(insertSql, connection);
            AddTransferHeaderParameters(inCmd, transfer);
            inCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.TransferNo.Trim() });
            await inCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Replace detail items
        {
            await using var delItems = new OleDbCommand("DELETE FROM [StockTransferSubform] WHERE [TransferNo] = ?", connection);
            delItems.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.TransferNo.Trim() });
            await delItems.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertItemSql = @"
            INSERT INTO [StockTransferSubform] (
                [TransferNo], [Barcode], [ItemName], [Qty], [GrossWt], [StoneWt], [NetWt],
                [StoneValue], [Making], [MRP], [Purity], [BoxNo], [Description], [CostPrice]
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

        foreach (var item in itemList)
        {
            await using var itemCmd = new OleDbCommand(insertItemSql, connection);
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.TransferNo.Trim() });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = item.Barcode?.Trim() ?? string.Empty });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = item.ItemName?.Trim() ?? string.Empty });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = (double)item.Qty });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = (double)item.GrossWt });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = (double)item.StoneWt });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = (double)item.NetWt });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.StoneValue });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.Making });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.MRP });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = item.Purity?.Trim() ?? string.Empty });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = item.BoxNo?.Trim() ?? string.Empty });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = item.Description?.Trim() ?? string.Empty });
            itemCmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = item.CostPrice });
            await itemCmd.ExecuteNonQueryAsync(cancellationToken);

            // Update Godown in BarcodeSubform for this barcode
            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                var bc = item.Barcode.Trim();
                var padded = bc.PadLeft(6, '0');
                const string updateGodownSql = "UPDATE [BarcodeSubform] SET [Godown] = ? WHERE ([Barcode] = ? OR [Barcode] = ?)";
                await using var godownCmd = new OleDbCommand(updateGodownSql, connection);
                godownCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.ToLocation?.Trim() ?? string.Empty });
                godownCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = bc });
                godownCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = padded });
                await godownCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public async Task DeleteTransferAsync(string transferNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transferNo)) return;

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        // Fetch transfer details so we can revert barcodes back to FromLocation
        var transfer = await GetByTransferNoAsync(transferNo, cancellationToken);
        if (transfer != null)
        {
            var items = await GetTransferItemsAsync(transferNo, cancellationToken);
            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.Barcode)))
            {
                var bc = item.Barcode.Trim();
                var padded = bc.PadLeft(6, '0');
                const string revertGodownSql = "UPDATE [BarcodeSubform] SET [Godown] = ? WHERE ([Barcode] = ? OR [Barcode] = ?)";
                await using var godownCmd = new OleDbCommand(revertGodownSql, connection);
                godownCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.FromLocation?.Trim() ?? string.Empty });
                godownCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = bc });
                godownCmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = padded });
                await godownCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using var delItems = new OleDbCommand("DELETE FROM [StockTransferSubform] WHERE [TransferNo] = ?", connection);
        delItems.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transferNo.Trim() });
        await delItems.ExecuteNonQueryAsync(cancellationToken);

        await using var delHeader = new OleDbCommand("DELETE FROM [StockTransfer] WHERE [TransferNo] = ?", connection);
        delHeader.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transferNo.Trim() });
        await delHeader.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<StockTransferItem?> LookupBarcodeForTransferAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;

        var code = barcode.Trim();
        var padded = code.PadLeft(6, '0');

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT 
                b.[Barcode], b.[ItemName], b.[Qty], b.[GrossWt], b.[StoneWt], b.[NetWt],
                b.[StoneValue], b.[Making], b.[MRP], b.[Purity], b.[BoxNo], b.[Description],
                b.[CostPrice]
            FROM [BarcodeSubform] b
            WHERE ([Barcode] = ? OR [Barcode] = ?)";

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = code });
        command.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = padded });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new StockTransferItem
            {
                Barcode = reader[0]?.ToString()?.Trim() ?? code,
                ItemName = reader[1]?.ToString()?.Trim() ?? string.Empty,
                Qty = Convert.ToDecimal(reader[2] == DBNull.Value ? 1 : reader[2]),
                GrossWt = Convert.ToDecimal(reader[3] == DBNull.Value ? 0 : reader[3]),
                StoneWt = Convert.ToDecimal(reader[4] == DBNull.Value ? 0 : reader[4]),
                NetWt = Convert.ToDecimal(reader[5] == DBNull.Value ? 0 : reader[5]),
                StoneValue = Convert.ToDecimal(reader[6] == DBNull.Value ? 0 : reader[6]),
                Making = Convert.ToDecimal(reader[7] == DBNull.Value ? 0 : reader[7]),
                MRP = Convert.ToDecimal(reader[8] == DBNull.Value ? 0 : reader[8]),
                Purity = reader[9]?.ToString()?.Trim() ?? string.Empty,
                BoxNo = reader[10]?.ToString()?.Trim() ?? string.Empty,
                Description = reader[11]?.ToString()?.Trim() ?? string.Empty,
                CostPrice = Convert.ToDecimal(reader[12] == DBNull.Value ? 0 : reader[12])
            };
        }

        return null;
    }

    public async Task<IReadOnlyList<string>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Showroom",
            "Damage",
            "Counter 1",
            "Counter 2",
            "Counter 3",
            "Safe",
            "Workshop",
            "Branch",
            "Exhibition",
            "Repair"
        };

        try
        {
            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);

            await using var cmd = new OleDbCommand("SELECT DISTINCT [Godown] FROM [BarcodeSubform] WHERE [Godown] IS NOT NULL AND [Godown] <> ''", connection);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var loc = r[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(loc)) locations.Add(loc);
            }
        }
        catch { }

        return locations.OrderBy(x => x).ToList();
    }

    private static void AddTransferHeaderParameters(OleDbCommand cmd, StockTransfer transfer)
    {
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = transfer.TransferDate });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.FromLocation?.Trim() ?? string.Empty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.ToLocation?.Trim() ?? string.Empty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.TransferType?.Trim() ?? "Manual Transfer" });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = (double)transfer.TotalQty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = (double)transfer.TotalGrossWt });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = (double)transfer.TotalStoneWt });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = (double)transfer.TotalNetWt });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = transfer.TotalStoneValue });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = transfer.TotalMaking });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Currency) { Value = transfer.TotalMRP });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.Remarks?.Trim() ?? string.Empty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.EntryUser?.Trim() ?? string.Empty });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.Date) { Value = transfer.EntryDate ?? DateTime.Now });
        cmd.Parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = transfer.Status?.Trim() ?? "Saved" });
    }

    private static StockTransfer MapTransferHeader(IDataRecord r)
    {
        return new StockTransfer
        {
            TransferNo = r[0]?.ToString()?.Trim() ?? string.Empty,
            TransferDate = r[1] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(r[1]),
            FromLocation = r[2]?.ToString()?.Trim() ?? string.Empty,
            ToLocation = r[3]?.ToString()?.Trim() ?? string.Empty,
            TransferType = r[4]?.ToString()?.Trim() ?? string.Empty,
            TotalQty = Convert.ToDecimal(r[5] == DBNull.Value ? 0 : r[5]),
            TotalGrossWt = Convert.ToDecimal(r[6] == DBNull.Value ? 0 : r[6]),
            TotalStoneWt = Convert.ToDecimal(r[7] == DBNull.Value ? 0 : r[7]),
            TotalNetWt = Convert.ToDecimal(r[8] == DBNull.Value ? 0 : r[8]),
            TotalStoneValue = Convert.ToDecimal(r[9] == DBNull.Value ? 0 : r[9]),
            TotalMaking = Convert.ToDecimal(r[10] == DBNull.Value ? 0 : r[10]),
            TotalMRP = Convert.ToDecimal(r[11] == DBNull.Value ? 0 : r[11]),
            Remarks = r[12]?.ToString()?.Trim() ?? string.Empty,
            EntryUser = r[13]?.ToString()?.Trim() ?? string.Empty,
            EntryDate = r[14] == DBNull.Value ? null : Convert.ToDateTime(r[14]),
            Status = r[15]?.ToString()?.Trim() ?? string.Empty
        };
    }

    private static StockTransferItem MapTransferItem(IDataRecord r)
    {
        return new StockTransferItem
        {
            AutoNo = Convert.ToInt64(r[0] == DBNull.Value ? 0 : r[0]),
            TransferNo = r[1]?.ToString()?.Trim() ?? string.Empty,
            Barcode = r[2]?.ToString()?.Trim() ?? string.Empty,
            ItemName = r[3]?.ToString()?.Trim() ?? string.Empty,
            Qty = Convert.ToDecimal(r[4] == DBNull.Value ? 0 : r[4]),
            GrossWt = Convert.ToDecimal(r[5] == DBNull.Value ? 0 : r[5]),
            StoneWt = Convert.ToDecimal(r[6] == DBNull.Value ? 0 : r[6]),
            NetWt = Convert.ToDecimal(r[7] == DBNull.Value ? 0 : r[7]),
            StoneValue = Convert.ToDecimal(r[8] == DBNull.Value ? 0 : r[8]),
            Making = Convert.ToDecimal(r[9] == DBNull.Value ? 0 : r[9]),
            MRP = Convert.ToDecimal(r[10] == DBNull.Value ? 0 : r[10]),
            Purity = r[11]?.ToString()?.Trim() ?? string.Empty,
            BoxNo = r[12]?.ToString()?.Trim() ?? string.Empty,
            Description = r[13]?.ToString()?.Trim() ?? string.Empty,
            CostPrice = Convert.ToDecimal(r[14] == DBNull.Value ? 0 : r[14])
        };
    }
}
