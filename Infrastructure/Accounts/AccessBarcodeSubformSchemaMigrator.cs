using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessBarcodeSubformSchemaMigrator
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

            await TryEnsureColumnAsync(connection, "BarcodeSubform", "Godown", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, "BarcodeSubform", "BoxNo", "TEXT(100)", cancellationToken);

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

    private static async Task TryEnsureColumnAsync(OleDbConnection connection, string table, string column, string typeDefinition, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new OleDbCommand($"ALTER TABLE [{table}] ADD COLUMN [{column}] {typeDefinition}", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Column may already exist or table may be locked
        }
    }
}
