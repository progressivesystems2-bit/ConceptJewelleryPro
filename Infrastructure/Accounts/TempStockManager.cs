using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class TempStockManager
{
    public static async Task AddOrUpdateAsync(
        OleDbConnection connection,
        OleDbTransaction? transaction,
        BarcodeSubformItem item,
        string? resolvedSupplier = null,
        string? resolvedPartyCode = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.Barcode))
            return;

        var barcode = item.Barcode.Trim();

        // Check if barcode already exists in TempStock
        bool exists = false;
        const string checkSql = "SELECT COUNT(*) FROM [TempStock] WHERE [Barcode] = ?";
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.Transaction = transaction;
            checkCmd.CommandText = checkSql;
            checkCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = barcode;
            var res = await checkCmd.ExecuteScalarAsync(cancellationToken);
            exists = (res is not null && res != DBNull.Value && Convert.ToInt32(res) > 0);
        }

        if (!exists)
        {
            const string insertSql = @"
                INSERT INTO [TempStock]
                ([Barcode], [BarcodeAutoNo], [Nos], [VouNo], [BType], [ItemName], [ItemCode], [Purity], [Description],
                 [Description2], [Size], [Qty], [GrossWt], [StoneWt], [NetWt], [Making], [MakingType], [MPerGr],
                 [StoneValue], [CostTounch], [CostPg], [CostMaking], [CostPrice], [Costrate], [MRP],
                 [Supplier], [SupplierName], [SuppInvNo], [SuppDate], [PDate], [PunchedOn], [Photo], [Sti], [Status],
                 [BoxNo], [Godown])
                VALUES
                (?, ?, ?, ?, ?, ?, ?, ?, ?,
                 ?, ?, ?, ?, ?, ?, ?, ?, ?,
                 ?, ?, ?, ?, ?, ?, ?,
                 ?, ?, ?, ?, ?, ?, ?, ?, ?,
                 ?, ?)";

            await using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = insertSql;

            BindParams(insertCmd, item, resolvedSupplier, resolvedPartyCode);
            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string updateSql = @"
                UPDATE [TempStock]
                SET [BType] = ?, [ItemName] = ?, [ItemCode] = ?, [Purity] = ?, [Description] = ?,
                    [Description2] = ?, [Size] = ?, [Qty] = ?, [GrossWt] = ?, [StoneWt] = ?, [NetWt] = ?,
                    [Making] = ?, [MakingType] = ?, [MPerGr] = ?, [StoneValue] = ?, [CostTounch] = ?,
                    [CostPg] = ?, [CostMaking] = ?, [CostPrice] = ?, [Costrate] = ?, [MRP] = ?,
                    [Supplier] = ?, [SupplierName] = ?, [SuppInvNo] = ?, [SuppDate] = ?, [PDate] = ?,
                    [Photo] = ?, [Sti] = ?, [Status] = ?, [BoxNo] = ?, [Godown] = ?
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
            AddParam(updateCmd, resolvedSupplier ?? item.SupplierName, OleDbType.VarWChar);
            AddParam(updateCmd, resolvedPartyCode ?? item.SupplierName, OleDbType.VarWChar);
            AddParam(updateCmd, item.SuppInvNo, OleDbType.VarWChar);
            AddParam(updateCmd, item.SuppDate, OleDbType.Date);
            AddParam(updateCmd, item.PDate, OleDbType.Date);
            AddParam(updateCmd, item.Photo, OleDbType.VarWChar);
            AddParam(updateCmd, item.Sti, OleDbType.VarWChar);
            AddParam(updateCmd, item.Status, OleDbType.VarWChar);
            AddParam(updateCmd, item.BoxNo, OleDbType.VarWChar);
            AddParam(updateCmd, item.Godown, OleDbType.VarWChar);
            AddParam(updateCmd, barcode, OleDbType.VarWChar);

            await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static async Task RemoveAsync(
        OleDbConnection connection,
        OleDbTransaction? transaction,
        string barcode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return;

        const string deleteSql = "DELETE FROM [TempStock] WHERE [Barcode] = ?";
        await using var deleteCmd = connection.CreateCommand();
        deleteCmd.Transaction = transaction;
        deleteCmd.CommandText = deleteSql;
        deleteCmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = barcode.Trim();
        await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task RemoveSoldAsync(
        OleDbConnection connection,
        OleDbTransaction? transaction,
        IEnumerable<string> barcodes,
        CancellationToken cancellationToken = default)
    {
        var list = barcodes.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b.Trim()).Distinct().ToList();
        if (list.Count == 0) return;

        foreach (var bc in list)
        {
            const string sql = "DELETE FROM [TempStock] WHERE [Barcode] = ?";
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = bc;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static async Task RestoreBarcodesAsync(
        OleDbConnection connection,
        OleDbTransaction? transaction,
        IEnumerable<string> barcodes,
        CancellationToken cancellationToken = default)
    {
        var list = barcodes.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b.Trim()).Distinct().ToList();
        if (list.Count == 0) return;

        foreach (var bc in list)
        {
            const string restoreSql = @"
                INSERT INTO [TempStock]
                ([Barcode], [BarcodeAutoNo], [Nos], [VouNo], [BType], [ItemName], [ItemCode], [HeadGroup], [Purity], [Description],
                 [Description2], [Size], [Qty], [GrossWt], [StoneWt], [NetWt], [Making], [MakingType], [MPerGr],
                 [StoneValue], [CostTounch], [CostPg], [CostMaking], [CostPrice], [Costrate], [MRP],
                 [Supplier], [SupplierName], [SuppInvNo], [SuppDate], [PDate], [PunchedOn], [Photo], [Sti], [Status],
                 [BoxNo], [Godown])
                SELECT 
                 b.[Barcode], b.[BarcodeAutoNo], b.[Nos], b.[VouNo], b.[BType], b.[ItemName], b.[ItemCode], i.[HeadGroup], b.[Purity], b.[Description],
                 b.[Description2], b.[Size], b.[Qty], b.[GrossWt], b.[StoneWt], b.[NetWt], b.[Making], b.[MakingType], b.[MPerGr],
                 b.[StoneValue], b.[CostTounch], b.[CostPg], b.[CostMaking], b.[CostPrice], b.[Costrate], b.[MRP],
                 b.[Supplier], b.[SupplierName], b.[SuppInvNo], b.[SuppDate], b.[PDate], b.[PunchedOn], b.[Photo], b.[Sti], b.[Status],
                 b.[BoxNo], b.[Godown]
                FROM [BarcodeSubform] b
                LEFT JOIN [ItemCreation] i ON b.[ItemName] = i.[ItemName]
                WHERE b.[Barcode] = ? AND b.[Barcode] NOT IN (SELECT [Barcode] FROM [TempStock])";

            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = restoreSql;
                cmd.Parameters.Add("?", OleDbType.VarWChar, 50).Value = bc;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
                // Non-fatal if already present or schema variance
            }
        }
    }

    public static async Task<IReadOnlyList<string>> GetAvailableBarcodesAsync(
        OleDbConnection connection,
        string? targetItemHead = null,
        CancellationToken cancellationToken = default)
    {
        var list = new List<string>();

        string sql;
        string[] heads = Array.Empty<string>();

        if (!string.IsNullOrWhiteSpace(targetItemHead))
        {
            heads = targetItemHead.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        if (heads.Length > 0)
        {
            var paramPlaceholders = string.Join(",", heads.Select((_, i) => $"@Head{i}"));
            sql = $@"SELECT [Barcode] FROM [TempStock] 
                     WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                       AND ([HeadGroup] IN ({paramPlaceholders}) OR [HeadGroup] IS NULL OR [HeadGroup] = '')
                     ORDER BY [Barcode]";
        }
        else
        {
            sql = "SELECT [Barcode] FROM [TempStock] WHERE ([Status] IS NULL OR [Status] <> 'Deleted') ORDER BY [Barcode]";
        }

        await using var cmd = new OleDbCommand(sql, connection);
        if (heads.Length > 0)
        {
            for (int i = 0; i < heads.Length; i++)
            {
                cmd.Parameters.Add($"@Head{i}", OleDbType.VarWChar, 50).Value = heads[i];
            }
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var bc = reader["Barcode"]?.ToString();
            if (!string.IsNullOrWhiteSpace(bc))
            {
                list.Add(bc);
            }
        }

        return list;
    }

    public static async Task<IReadOnlyList<TempStockItem>> GetTempStockItemsAsync(
        OleDbConnection connection,
        string? targetItemHead = null,
        CancellationToken cancellationToken = default)
    {
        var list = new List<TempStockItem>();

        string sql;
        string[] heads = Array.Empty<string>();

        if (!string.IsNullOrWhiteSpace(targetItemHead))
        {
            heads = targetItemHead.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        if (heads.Length > 0)
        {
            var paramPlaceholders = string.Join(",", heads.Select((_, i) => $"@Head{i}"));
            sql = $@"SELECT * FROM [TempStock] 
                     WHERE ([Status] IS NULL OR [Status] <> 'Deleted')
                       AND ([HeadGroup] IN ({paramPlaceholders}) OR [HeadGroup] IS NULL OR [HeadGroup] = '')
                     ORDER BY [Barcode]";
        }
        else
        {
            sql = "SELECT * FROM [TempStock] WHERE ([Status] IS NULL OR [Status] <> 'Deleted') ORDER BY [Barcode]";
        }

        await using var cmd = new OleDbCommand(sql, connection);
        if (heads.Length > 0)
        {
            for (int i = 0; i < heads.Length; i++)
            {
                cmd.Parameters.Add($"@Head{i}", OleDbType.VarWChar, 50).Value = heads[i];
            }
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var desc2 = GetString(reader, "Description2");
            if (string.IsNullOrEmpty(desc2)) desc2 = GetString(reader, "OldBarcode");
            if (string.IsNullOrEmpty(desc2)) desc2 = GetString(reader, "HUID");

            list.Add(new TempStockItem
            {
                Barcode = GetString(reader, "Barcode"),
                BType = GetString(reader, "BType"),
                ItemName = GetString(reader, "ItemName"),
                ItemCode = GetString(reader, "ItemCode"),
                HeadGroup = GetString(reader, "HeadGroup"),
                Purity = GetString(reader, "Purity"),
                Qty = GetDecimal(reader, "Qty", 1m),
                GrossWt = GetDecimal(reader, "GrossWt"),
                NetWt = GetDecimal(reader, "NetWt"),
                StoneWt = GetDecimal(reader, "StoneWt"),
                Making = GetDecimal(reader, "Making"),
                MPerGr = GetDecimal(reader, "MPerGr"),
                StoneValue = GetDecimal(reader, "StoneValue"),
                MRP = GetDecimal(reader, "MRP"),
                CostRate = GetDecimal(reader, "Costrate"),
                Description = GetString(reader, "Description"),
                Description2 = desc2,
                SupplierName = GetString(reader, "SupplierName")
            });
        }

        if (list.Count == 0 && heads.Length > 0)
        {
            // If no stock items matched specific target head (e.g. DECORATIVES), fall back to all available active stock
            const string fallbackSql = "SELECT * FROM [TempStock] WHERE ([Status] IS NULL OR [Status] <> 'Deleted') ORDER BY [Barcode]";
            await using var fallbackCmd = new OleDbCommand(fallbackSql, connection);
            await using var fallbackReader = await fallbackCmd.ExecuteReaderAsync(cancellationToken);
            while (await fallbackReader.ReadAsync(cancellationToken))
            {
                var desc2 = GetString(fallbackReader, "Description2");
                if (string.IsNullOrEmpty(desc2)) desc2 = GetString(fallbackReader, "OldBarcode");
                if (string.IsNullOrEmpty(desc2)) desc2 = GetString(fallbackReader, "HUID");

                list.Add(new TempStockItem
                {
                    Barcode = GetString(fallbackReader, "Barcode"),
                    BType = GetString(fallbackReader, "BType"),
                    ItemName = GetString(fallbackReader, "ItemName"),
                    ItemCode = GetString(fallbackReader, "ItemCode"),
                    HeadGroup = GetString(fallbackReader, "HeadGroup"),
                    Purity = GetString(fallbackReader, "Purity"),
                    Qty = GetDecimal(fallbackReader, "Qty", 1m),
                    GrossWt = GetDecimal(fallbackReader, "GrossWt"),
                    NetWt = GetDecimal(fallbackReader, "NetWt"),
                    StoneWt = GetDecimal(fallbackReader, "StoneWt"),
                    Making = GetDecimal(fallbackReader, "Making"),
                    MPerGr = GetDecimal(fallbackReader, "MPerGr"),
                    StoneValue = GetDecimal(fallbackReader, "StoneValue"),
                    MRP = GetDecimal(fallbackReader, "MRP"),
                    CostRate = GetDecimal(fallbackReader, "Costrate"),
                    Description = GetString(fallbackReader, "Description"),
                    Description2 = desc2,
                    SupplierName = GetString(fallbackReader, "SupplierName")
                });
            }
        }

        if (list.Count == 0)
        {
            // Direct fallback to BarcodeSubform if TempStock is empty
            try
            {
                const string bsSql = @"
                    SELECT TOP 3000 b.[Barcode], b.[BType], b.[ItemName], b.[ItemCode], i.[HeadGroup], b.[Purity],
                           b.[Qty], b.[GrossWt], b.[NetWt], b.[StoneWt], b.[Making], b.[MPerGr],
                           b.[StoneValue], b.[CostTounch], b.[CostPg], b.[CostMaking], b.[CostPrice], b.[Costrate], b.[MRP],
                           b.[SupplierName], b.[Description], b.[Description2], b.[OldBarcode]
                    FROM [BarcodeSubform] b
                    LEFT JOIN [ItemCreation] i ON b.[ItemName] = i.[ItemName]
                    WHERE (b.[Status] IS NULL OR b.[Status] <> 'Deleted')
                      AND (b.[Sale] = False OR b.[Sale] IS NULL)
                      AND b.[Barcode] IS NOT NULL AND b.[Barcode] <> ''
                    ORDER BY b.[Barcode]";
                await using var bsCmd = new OleDbCommand(bsSql, connection);
                await using var bsReader = await bsCmd.ExecuteReaderAsync(cancellationToken);
                while (await bsReader.ReadAsync(cancellationToken))
                {
                    var desc2 = GetString(bsReader, "Description2");
                    if (string.IsNullOrEmpty(desc2)) desc2 = GetString(bsReader, "OldBarcode");

                    list.Add(new TempStockItem
                    {
                        Barcode = GetString(bsReader, "Barcode"),
                        BType = GetString(bsReader, "BType"),
                        ItemName = GetString(bsReader, "ItemName"),
                        ItemCode = GetString(bsReader, "ItemCode"),
                        HeadGroup = GetString(bsReader, "HeadGroup"),
                        Purity = GetString(bsReader, "Purity"),
                        Qty = GetDecimal(bsReader, "Qty", 1m),
                        GrossWt = GetDecimal(bsReader, "GrossWt"),
                        NetWt = GetDecimal(bsReader, "NetWt"),
                        StoneWt = GetDecimal(bsReader, "StoneWt"),
                        Making = GetDecimal(bsReader, "Making"),
                        MPerGr = GetDecimal(bsReader, "MPerGr"),
                        StoneValue = GetDecimal(bsReader, "StoneValue"),
                        MRP = GetDecimal(bsReader, "MRP"),
                        CostRate = GetDecimal(bsReader, "Costrate"),
                        Description = GetString(bsReader, "Description"),
                        Description2 = desc2,
                        SupplierName = GetString(bsReader, "SupplierName")
                    });
                }
            }
            catch { }
        }

        return list;
    }

    public static async Task<BarcodeDetails?> GetBarcodeDetailsAsync(
        OleDbConnection connection,
        string barcode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        var clean = barcode.Trim();
        bool isNumeric = int.TryParse(clean, out var nos) && nos > 0;

        var sql = isNumeric
            ? "SELECT TOP 1 * FROM [TempStock] WHERE [Barcode] = ? OR [Barcode] = ? OR [Nos] = ?"
            : "SELECT TOP 1 * FROM [TempStock] WHERE [Barcode] = ? OR [Barcode] = ?";

        await using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = clean;
        command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = clean.PadLeft(6, '0');
        if (isNumeric)
        {
            command.Parameters.Add("?", OleDbType.Integer).Value = nos;
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var desc2 = GetString(reader, "Description2");
        if (string.IsNullOrEmpty(desc2)) desc2 = GetString(reader, "OldBarcode");
        if (string.IsNullOrEmpty(desc2)) desc2 = GetString(reader, "HUID");

        return new BarcodeDetails
        {
            Barcode = GetString(reader, "Barcode"),
            BType = GetString(reader, "BType"),
            ItemName = GetString(reader, "ItemName"),
            Purity = GetString(reader, "Purity"),
            Qty = GetInt(reader, "Qty", 1),
            GrossWt = GetDecimal(reader, "GrossWt"),
            NetWt = GetDecimal(reader, "NetWt"),
            StoneWt = GetDecimal(reader, "StoneWt"),
            StoneValue = GetDecimal(reader, "StoneValue"),
            Making = GetDecimal(reader, "Making"),
            MPerGr = GetDecimal(reader, "MPerGr"),
            Description = GetString(reader, "Description"),
            Description2 = desc2,
            MRP = GetDecimal(reader, "MRP"),
            ItemCode = GetString(reader, "ItemCode"),
            CostRate = GetDecimal(reader, "Costrate")
        };
    }

    private static bool HasColumn(DbDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string GetString(DbDataReader reader, string column) =>
        HasColumn(reader, column) && reader[column] != DBNull.Value ? Convert.ToString(reader[column]) ?? string.Empty : string.Empty;

    private static decimal GetDecimal(DbDataReader reader, string column, decimal defaultValue = 0m)
    {
        if (HasColumn(reader, column) && reader[column] != DBNull.Value)
        {
            try { return Convert.ToDecimal(reader[column]); } catch { }
        }
        return defaultValue;
    }

    private static int GetInt(DbDataReader reader, string column, int defaultValue = 0)
    {
        if (HasColumn(reader, column) && reader[column] != DBNull.Value)
        {
            try { return Convert.ToInt32(Convert.ToDouble(reader[column])); } catch { }
        }
        return defaultValue;
    }

    private static void BindParams(OleDbCommand cmd, BarcodeSubformItem item, string? resolvedSupplier, string? resolvedPartyCode)
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
        AddParam(cmd, resolvedSupplier ?? item.SupplierName, OleDbType.VarWChar);
        AddParam(cmd, resolvedPartyCode ?? item.SupplierName, OleDbType.VarWChar);
        AddParam(cmd, item.SuppInvNo, OleDbType.VarWChar);
        AddParam(cmd, item.SuppDate, OleDbType.Date);
        AddParam(cmd, item.PDate, OleDbType.Date);
        AddParam(cmd, item.PunchedOn, OleDbType.Date);
        AddParam(cmd, item.Photo, OleDbType.VarWChar);
        AddParam(cmd, item.Sti, OleDbType.VarWChar);
        AddParam(cmd, item.Status, OleDbType.VarWChar);
        AddParam(cmd, item.BoxNo, OleDbType.VarWChar);
        AddParam(cmd, item.Godown, OleDbType.VarWChar);
    }

    private static void AddParam(OleDbCommand cmd, object? val, OleDbType type)
    {
        var p = cmd.Parameters.Add("?", type);
        p.Value = val ?? DBNull.Value;
    }
}
