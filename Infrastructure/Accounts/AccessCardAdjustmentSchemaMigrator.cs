using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessCardAdjustmentSchemaMigrator
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

            await EnsureCardSetupTableAsync(connection, cancellationToken);
            await EnsureCardAdjustmentReceiptTableAsync(connection, cancellationToken);
            await EnsureCardAdjustmentReceiptItemTableAsync(connection, cancellationToken);

            // Ensure Reconcilation flags
            await TryEnsureColumnAsync(connection, "Bill", "Reconcilation", "YESNO", cancellationToken);
            await TryEnsureColumnAsync(connection, "PaymentSubform", "Reconcilation", "YESNO", cancellationToken);

            // Ensure CardSetup compatibility columns if CardSetup was created with legacy schema
            await TryEnsureColumnAsync(connection, "CardSetup", "CardBank", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, "CardSetup", "CardComm", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "CardSetup", "Sbi", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "CardSetup", "Order", "INTEGER", cancellationToken);

            // Sync legacy CardSetup BankName -> CardBank if needed
            await TrySyncLegacyCardSetupAsync(connection, cancellationToken);

            lock (_lock)
            {
                _migrated = true;
            }
        }
        catch
        {
            // Non-fatal if schema cannot be altered immediately
        }
    }

    private static async Task EnsureCardSetupTableAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            CREATE TABLE CardSetup
            (
                CardBank TEXT(100) NOT NULL CONSTRAINT PK_CardSetup PRIMARY KEY,
                CardComm CURRENCY,
                Sbi CURRENCY,
                [Order] INTEGER
            )";

        try
        {
            await using var cmd = new OleDbCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }
    }

    private static async Task EnsureCardAdjustmentReceiptTableAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            CREATE TABLE CardAdjustmentReceipt
            (
                Nos INTEGER,
                ReceiptNo TEXT(50) NOT NULL CONSTRAINT PK_CardAdjustmentReceipt PRIMARY KEY,
                ReceiptDate DATETIME,
                Branch TEXT(50),
                CardAccount TEXT(100),
                BankAccount TEXT(100),
                ReceivableAccount TEXT(100),
                CommissionAccount TEXT(100),
                CommissionPercent CURRENCY,
                FromDate DATETIME,
                ToDate DATETIME,
                TotalCardAmount CURRENCY,
                TotalCommissionAmount CURRENCY,
                TotalReceivedAmount CURRENCY,
                TaxableAmount CURRENCY,
                SgstTaxAmount CURRENCY,
                CgstTaxAmount CURRENCY,
                IgstTaxAmount CURRENCY,
                RofAmount CURRENCY,
                InvoiceType TEXT(50),
                TaxType TEXT(50),
                TaxManual YESNO,
                Remarks LONGTEXT,
                VouNo TEXT(50),
                Status TEXT(50),
                CreatedBy TEXT(100),
                CreatedDate DATETIME
            )";

        try
        {
            await using var cmd = new OleDbCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var idxCmd = new OleDbCommand("CREATE INDEX IX_CAR_ReceiptDate ON CardAdjustmentReceipt (ReceiptDate)", connection);
            await idxCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }
    }

    private static async Task EnsureCardAdjustmentReceiptItemTableAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            CREATE TABLE CardAdjustmentReceiptItem
            (
                Id AUTOINCREMENT CONSTRAINT PK_CardAdjustmentReceiptItem PRIMARY KEY,
                ReceiptNo TEXT(50),
                SrNo INTEGER,
                DbName TEXT(50),
                BillNo TEXT(50),
                BillDate DATETIME,
                AccountName TEXT(150),
                CardNo TEXT(100),
                CardBank TEXT(100),
                CardAmount CURRENCY,
                CommissionPercent CURRENCY,
                CommissionAmount CURRENCY,
                ReceivedAmount CURRENCY
            )";

        try
        {
            await using var cmd = new OleDbCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var idxCmd1 = new OleDbCommand("CREATE INDEX IX_CARI_ReceiptNo ON CardAdjustmentReceiptItem (ReceiptNo)", connection);
            await idxCmd1.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var idxCmd2 = new OleDbCommand("CREATE INDEX IX_CARI_BillNo ON CardAdjustmentReceiptItem (BillNo)", connection);
            await idxCmd2.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }
    }

    private static async Task TryEnsureColumnAsync(OleDbConnection connection, string table, string column, string typeDefinition, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new OleDbCommand($"ALTER TABLE [{table}] ADD COLUMN [{column}] {typeDefinition}", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }
    }

    private static async Task TrySyncLegacyCardSetupAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = "UPDATE CardSetup SET CardBank = BankName WHERE (CardBank IS NULL OR CardBank = '') AND BankName IS NOT NULL";
            await using var cmd = new OleDbCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            const string sql2 = "UPDATE CardSetup SET CardComm = Comm WHERE (CardComm IS NULL OR CardComm = 0) AND Comm IS NOT NULL AND Comm > 0";
            await using var cmd2 = new OleDbCommand(sql2, connection);
            await cmd2.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }
    }
}
