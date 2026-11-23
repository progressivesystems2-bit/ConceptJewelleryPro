using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessPurchasesSchemaMigrator
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

            var tablesSchema = await connection.GetSchemaAsync("Tables", cancellationToken);
            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Data.DataRow row in tablesSchema.Rows)
            {
                var name = row["TABLE_NAME"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    existingTables.Add(name);
                }
            }

            bool purchasesExists = existingTables.Contains("Purchases");
            if (!purchasesExists)
            {
                try
                {
                    await using var checkCmd = new OleDbCommand("SELECT TOP 1 [VouNo] FROM [Purchases]", connection);
                    await checkCmd.ExecuteScalarAsync(cancellationToken);
                    purchasesExists = true;
                }
                catch
                {
                    purchasesExists = false;
                }
            }

            if (!purchasesExists)
            {
                const string createPurchasesSql = @"
                    CREATE TABLE Purchases
                    (
                        [VouNo] TEXT(50) NOT NULL,
                        [VouType] TEXT(50),
                        [BillDate] DATETIME,
                        [DueDate] DATETIME,
                        [SuppBillNo] TEXT(50),
                        [SuppDate] DATETIME,
                        [Phone] TEXT(50),
                        [AccountName] TEXT(150),
                        [CustomerName] TEXT(150),
                        [CustomerCode] TEXT(50),
                        [Address] TEXT(255),
                        [Address2] TEXT(255),
                        [City] TEXT(50),
                        [Pincode] TEXT(20),
                        [Email] TEXT(100),
                        [VatNo] TEXT(50),
                        [VatType] TEXT(50),
                        [Reverse] YESNO,
                        [Check] YESNO,
                        [BillCanceled] YESNO,
                        [GrossAmt] CURRENCY,
                        [DiscountAllowed] CURRENCY,
                        [TaxableAmount] CURRENCY,
                        [Tax] CURRENCY,
                        [TaxAmt] CURRENCY,
                        [CGSTAmt] CURRENCY,
                        [SGSTAmt] CURRENCY,
                        [IGSTAmt] CURRENCY,
                        [AddLess] CURRENCY,
                        [AddLessNote] TEXT(100),
                        [NetAmt] CURRENCY,
                        [Remarks] MEMO,
                        [Narration] MEMO,
                        [Note] TEXT(255),
                        [EntryUser] TEXT(50),
                        [EntryDate] DATETIME,
                        [EntryTime] DATETIME,
                        [ComputerName] TEXT(50),
                        [Status] TEXT(50),
                        [InOut] TEXT(10),
                        [GrWt] CURRENCY,
                        [NtWt] CURRENCY,
                        [TotalQty] DOUBLE,
                        CONSTRAINT PK_Purchases PRIMARY KEY ([VouNo])
                    )";

                await ExecuteCommandAsync(connection, createPurchasesSql, cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_Purchases_BillDate ON Purchases (BillDate)", cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_Purchases_VouType ON Purchases (VouType)", cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_Purchases_AccountName ON Purchases (AccountName)", cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_Purchases_SuppBillNo ON Purchases (SuppBillNo)", cancellationToken);
            }
            else
            {
                // Ensure columns exist if updated over time
                await TryEnsureColumnAsync(connection, "Purchases", "SuppBillNo", "TEXT(50)", cancellationToken);
                await TryEnsureColumnAsync(connection, "Purchases", "SuppDate", "DATETIME", cancellationToken);
                await TryEnsureColumnAsync(connection, "Purchases", "CGSTAmt", "CURRENCY", cancellationToken);
                await TryEnsureColumnAsync(connection, "Purchases", "SGSTAmt", "CURRENCY", cancellationToken);
                await TryEnsureColumnAsync(connection, "Purchases", "IGSTAmt", "CURRENCY", cancellationToken);
                await TryEnsureColumnAsync(connection, "Purchases", "AddLess", "CURRENCY", cancellationToken);
                await TryEnsureColumnAsync(connection, "Purchases", "AddLessNote", "TEXT(100)", cancellationToken);
                await TryEnsureColumnAsync(connection, "Purchases", "TotalQty", "DOUBLE", cancellationToken);
                await TryAlterColumnTypeAsync(connection, "Purchases", "TotalQty", "DOUBLE", cancellationToken);
            }

            bool subformExists = existingTables.Contains("PurchaseSubform");
            if (!subformExists)
            {
                try
                {
                    await using var checkSubCmd = new OleDbCommand("SELECT TOP 1 [VouNo] FROM [PurchaseSubform]", connection);
                    await checkSubCmd.ExecuteScalarAsync(cancellationToken);
                    subformExists = true;
                }
                catch
                {
                    subformExists = false;
                }
            }

            if (!subformExists)
            {
                const string createSubformSql = @"
                    CREATE TABLE PurchaseSubform
                    (
                        [AutoNo] AUTOINCREMENT,
                        [VouNo] TEXT(50),
                        [SlNo] INTEGER,
                        [Barcode] TEXT(50),
                        [OldBarcode] TEXT(50),
                        [PcRate] YESNO,
                        [ItemName] TEXT(150),
                        [Purity] TEXT(50),
                        [HSN] TEXT(50),
                        [ENote] TEXT(255),
                        [Qty] DOUBLE,
                        [GrWt] CURRENCY,
                        [LessWt] CURRENCY,
                        [NtWt] CURRENCY,
                        [Rate] CURRENCY,
                        [Amount] CURRENCY,
                        [MetalValue] CURRENCY,
                        [Making] CURRENCY,
                        [MperGr] CURRENCY,
                        [StoneValue] CURRENCY,
                        [DisPer] CURRENCY,
                        [DisAmt] CURRENCY,
                        [GrossAmt] CURRENCY,
                        [TaxableAmount] CURRENCY,
                        [GstPer] CURRENCY,
                        [GstAmt] CURRENCY,
                        [TotalAmt] CURRENCY,
                        [IsDiscount] YESNO,
                        CONSTRAINT PK_PurchaseSubform PRIMARY KEY ([AutoNo])
                    )";

                await ExecuteCommandAsync(connection, createSubformSql, cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_PurchaseSubform_VouNo ON PurchaseSubform (VouNo)", cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_PurchaseSubform_ItemName ON PurchaseSubform (ItemName)", cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_PurchaseSubform_Barcode ON PurchaseSubform (Barcode)", cancellationToken);
            }
            else
            {
                // Ensure line-level tax columns exist
                await TryEnsureColumnAsync(connection, "PurchaseSubform", "TaxableAmount", "CURRENCY", cancellationToken);
                await TryEnsureColumnAsync(connection, "PurchaseSubform", "GstPer", "CURRENCY", cancellationToken);
                await TryEnsureColumnAsync(connection, "PurchaseSubform", "GstAmt", "CURRENCY", cancellationToken);
                await TryEnsureColumnAsync(connection, "PurchaseSubform", "TotalAmt", "CURRENCY", cancellationToken);
                await TryEnsureColumnAsync(connection, "PurchaseSubform", "LessWt", "CURRENCY", cancellationToken);
                await TryAlterColumnTypeAsync(connection, "PurchaseSubform", "Qty", "DOUBLE", cancellationToken);
            }

            lock (_lock)
            {
                _migrated = true;
            }
        }
        catch
        {
            // Non-fatal fallback
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
            // Ignore index exists or non-fatal DDL errors
        }
    }

    private static async Task TryEnsureColumnAsync(OleDbConnection connection, string table, string column, string typeDefinition, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new OleDbCommand($"ALTER TABLE [{table}] ADD COLUMN [{column}] {typeDefinition}", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Ignore column exists error
        }
    }

    private static async Task TryAlterColumnTypeAsync(OleDbConnection connection, string table, string column, string typeDefinition, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new OleDbCommand($"ALTER TABLE [{table}] ALTER COLUMN [{column}] {typeDefinition}", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Ignore if already the type or error
        }
    }
}
