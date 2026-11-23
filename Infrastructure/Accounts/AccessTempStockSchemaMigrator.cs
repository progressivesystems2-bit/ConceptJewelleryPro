using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessTempStockSchemaMigrator
{
    private static bool _migrated;
    private static readonly object _lock = new();

    public static async Task EnsureSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (_migrated || string.IsNullOrWhiteSpace(connectionString))
            return;

        try
        {
            await using var connection = new OleDbConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            bool tableExists = false;
            try
            {
                await using var checkCmd = new OleDbCommand("SELECT TOP 1 [Barcode] FROM [TempStock]", connection);
                await checkCmd.ExecuteScalarAsync(cancellationToken);
                tableExists = true;
            }
            catch
            {
                tableExists = false;
            }

            if (!tableExists)
            {
                const string createTableSql = @"
                    CREATE TABLE [TempStock]
                    (
                        [Barcode] TEXT(50) NOT NULL CONSTRAINT PK_TempStock PRIMARY KEY,
                        [BarcodeAutoNo] LONG,
                        [Nos] LONG,
                        [VouNo] TEXT(50),
                        [BType] TEXT(50),
                        [ItemName] TEXT(100),
                        [ItemCode] TEXT(50),
                        [HeadGroup] TEXT(50),
                        [Purity] TEXT(50),
                        [Description] TEXT(255),
                        [Description2] TEXT(255),
                        [Size] TEXT(50),
                        [Qty] DOUBLE,
                        [GrossWt] CURRENCY,
                        [StoneWt] CURRENCY,
                        [NetWt] CURRENCY,
                        [Making] CURRENCY,
                        [MakingType] TEXT(50),
                        [MPerGr] CURRENCY,
                        [StoneValue] CURRENCY,
                        [CostTounch] CURRENCY,
                        [CostPg] CURRENCY,
                        [CostMaking] CURRENCY,
                        [CostPrice] CURRENCY,
                        [Costrate] CURRENCY,
                        [MRP] CURRENCY,
                        [Supplier] TEXT(50),
                        [SupplierName] TEXT(100),
                        [SuppInvNo] TEXT(50),
                        [SuppDate] DATETIME,
                        [PDate] DATETIME,
                        [PunchedOn] DATETIME,
                        [Photo] TEXT(255),
                        [Sti] TEXT(50),
                        [Status] TEXT(50),
                        [BoxNo] TEXT(50),
                        [Godown] TEXT(50)
                    )";

                await ExecuteCommandAsync(connection, createTableSql, cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_TempStock_ItemName ON [TempStock] ([ItemName])", cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_TempStock_HeadGroup ON [TempStock] ([HeadGroup])", cancellationToken);
            }
            else
            {
                // Ensure HeadGroup and BType columns exist if TempStock was already created
                await TryExecuteCommandAsync(connection, "ALTER TABLE [TempStock] ADD COLUMN [HeadGroup] TEXT(50)", cancellationToken);
                await TryExecuteCommandAsync(connection, "ALTER TABLE [TempStock] ADD COLUMN [BType] TEXT(50)", cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_TempStock_HeadGroup ON [TempStock] ([HeadGroup])", cancellationToken);

                // Backfill HeadGroup from ItemCreation if missing
                try
                {
                    const string backfillSql = @"
                        UPDATE [TempStock] t
                        INNER JOIN [ItemCreation] i ON t.[ItemName] = i.[ItemName]
                        SET t.[HeadGroup] = i.[HeadGroup]
                        WHERE t.[HeadGroup] IS NULL OR t.[HeadGroup] = ''";
                    await ExecuteCommandAsync(connection, backfillSql, cancellationToken);
                }
                catch
                {
                    // Non-fatal
                }
            }

            // Check if TempStock needs initial population
            try
            {
                await using var countCmd = new OleDbCommand("SELECT COUNT(*) FROM [TempStock]", connection);
                var res = await countCmd.ExecuteScalarAsync(cancellationToken);
                var count = res is null or DBNull ? 0 : Convert.ToInt32(res);

                if (count == 0)
                {
                    await PopulateInitialStockAsync(connection, cancellationToken);
                }
            }
            catch
            {
                // Non-fatal if initial population encounters schema variance
            }

            lock (_lock)
            {
                _migrated = true;
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    private static async Task PopulateInitialStockAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            // Initial sync: copy all active barcodes from BarcodeSubform that are not in BillSubform or BarcodeConversion
            const string initialSyncSql = @"
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
                 b.[BoxNo], '' AS [Godown]
                FROM [BarcodeSubform] b
                LEFT JOIN [ItemCreation] i ON b.[ItemName] = i.[ItemName]
                WHERE (b.[Status] IS NULL OR b.[Status] <> 'Deleted')
                  AND b.[Barcode] IS NOT NULL AND b.[Barcode] <> ''
                  AND b.[Barcode] NOT IN (SELECT DISTINCT [Barcode] FROM [BillSubform] WHERE [Barcode] IS NOT NULL AND [Barcode] <> '')";

            await ExecuteCommandAsync(connection, initialSyncSql, cancellationToken);
        }
        catch
        {
            // Non-fatal fallback if nested query syntax differs
        }
    }

    private static async Task ExecuteCommandAsync(OleDbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new OleDbCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task TryExecuteCommandAsync(OleDbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new OleDbCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Ignore index or column already existing errors
        }
    }
}
