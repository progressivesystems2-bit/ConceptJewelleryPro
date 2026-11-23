using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessPurchaseExpenseSchemaMigrator
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

            bool purchaseEExists = false;
            try
            {
                await using var checkCmd = new OleDbCommand("SELECT TOP 1 VouNo FROM PurchaseE", connection);
                await checkCmd.ExecuteScalarAsync(cancellationToken);
                purchaseEExists = true;
            }
            catch
            {
                purchaseEExists = false;
            }

            if (!purchaseEExists)
        {
            const string createPurchaseESql = @"
                CREATE TABLE PurchaseE
                (
                    Nos INTEGER,
                    VouType TEXT(50),
                    VouNo TEXT(50) NOT NULL CONSTRAINT PK_PurchaseE PRIMARY KEY,
                    BillDate DATETIME,
                    AccountName TEXT(50),
                    Head TEXT(50),
                    PartyName TEXT(50),
                    GSTIN TEXT(50),
                    SuppBillNo TEXT(50),
                    SuppDate DATETIME,
                    [Reverse] YESNO,
                    [Tax%] CURRENCY,
                    GrossTotal CURRENCY,
                    TaxableAmount CURRENCY,
                    TaxAmt CURRENCY,
                    AddLessNote TEXT(50),
                    Exclusive CURRENCY,
                    ExclusiveAccount TEXT(150),
                    IGST CURRENCY,
                    CGST CURRENCY,
                    SGST CURRENCY,
                    AddLess CURRENCY,
                    NetAmt CURRENCY,
                    Narration LONGTEXT,
                    [Unique] TEXT(50),
                    [User] TEXT(50),
                    [Status] TEXT(50),
                    EntryDate DATETIME,
                    [Time] DATETIME,
                    Expense YESNO,
                    Cform YESNO,
                    Cleared YESNO,
                    [3b] YESNO,
                    InputTaken YESNO
                )";

            await ExecuteCommandAsync(connection, createPurchaseESql, cancellationToken);
            await TryExecuteCommandAsync(connection, "CREATE INDEX IX_PurchaseE_Nos ON PurchaseE (Nos)", cancellationToken);
            await TryExecuteCommandAsync(connection, "CREATE INDEX IX_PurchaseE_AccountName ON PurchaseE (AccountName)", cancellationToken);
            await TryExecuteCommandAsync(connection, "CREATE INDEX IX_PurchaseE_BillDate ON PurchaseE (BillDate)", cancellationToken);
            await TryExecuteCommandAsync(connection, "CREATE INDEX IX_PurchaseE_Unique ON PurchaseE ([Unique])", cancellationToken);
        }
        else
        {
            // Ensure newer columns exist
            await TryEnsureColumnAsync(connection, "PurchaseE", "ExclusiveAccount", "TEXT(150)", cancellationToken);
            await TryEnsureColumnAsync(connection, "PurchaseE", "Exclusive", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "PurchaseE", "Head", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "PurchaseE", "PartyName", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "PurchaseE", "Status", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "PurchaseE", "Unique", "TEXT(50)", cancellationToken);
        }

            bool subformExists = false;
            try
            {
                await using var checkSubCmd = new OleDbCommand("SELECT TOP 1 Vouno FROM PurchaseESubform", connection);
                await checkSubCmd.ExecuteScalarAsync(cancellationToken);
                subformExists = true;
            }
            catch
            {
                subformExists = false;
            }

            if (!subformExists)
            {
                const string createSubformSql = @"
                    CREATE TABLE PurchaseESubform
                    (
                        AutoNo AUTOINCREMENT CONSTRAINT PK_PurchaseESubform PRIMARY KEY,
                        Vouno TEXT(50),
                        SlNo TEXT(50),
                        ItemName TEXT(150),
                        HSN TEXT(50),
                        Qty CURRENCY,
                        Rate CURRENCY,
                        RateAmount CURRENCY,
                        DPer CURRENCY,
                        DAmt CURRENCY,
                        GrossAmt CURRENCY,
                        [Count] INTEGER,
                        TrackIdNo TEXT(50),
                        TaxRate CURRENCY,
                        TaxAmount CURRENCY
                    )";

                await ExecuteCommandAsync(connection, createSubformSql, cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_PurchaseESubform_Vouno ON PurchaseESubform (Vouno)", cancellationToken);
            }

            lock (_lock)
            {
                _migrated = true;
            }
        }
        catch
        {
            // Non-fatal if schema cannot be locked or modified right now
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
            // Column may already exist
        }
    }
}
