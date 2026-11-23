using System;
using System.Data.OleDb;
using System.Threading;
using System.Threading.Tasks;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessWhatsAppBillTrackerSchemaMigrator
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

            await EnsureTableAsync(connection, cancellationToken);

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

    private static async Task EnsureTableAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        const string createTableSql = @"
            CREATE TABLE [WhatsApp_Bill_Tracker]
            (
                [WhatsAppBillTrackerId] AUTOINCREMENT CONSTRAINT [PK_WhatsApp_Bill_Tracker] PRIMARY KEY,
                [Wamid] TEXT(255),
                [InvoiceNo] TEXT(100),
                [MobileNumber] TEXT(50),
                [SentAt] DATETIME,
                [DeliveryStatus] TEXT(50),
                [Rating] TEXT(50),
                [Reply] LONGTEXT,
                [UpdatedAt] DATETIME
            )";

        try
        {
            await using var command = new OleDbCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Table may already exist
        }

        try
        {
            await using var addColCmd = new OleDbCommand("ALTER TABLE [WhatsApp_Bill_Tracker] ADD COLUMN [DeliveryStatus] TEXT(50)", connection);
            await addColCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Column may already exist
        }

        try
        {
            await using var idxWamidCmd = new OleDbCommand("CREATE INDEX IX_WhatsApp_Bill_Tracker_Wamid ON [WhatsApp_Bill_Tracker] ([Wamid])", connection);
            await idxWamidCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var idxInvoiceNoCmd = new OleDbCommand("CREATE INDEX IX_WhatsApp_Bill_Tracker_InvoiceNo ON [WhatsApp_Bill_Tracker] ([InvoiceNo])", connection);
            await idxInvoiceNoCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }
    }
}
