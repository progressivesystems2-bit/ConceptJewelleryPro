using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessBoxMasterSchemaMigrator
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
            await TryEnsureColumnAsync(connection, "BoxMaster", "Rack", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "BoxMaster", "NumberOfBoxes", "INTEGER", cancellationToken);
            await TryEnsureColumnAsync(connection, "BoxMaster", "BoxWt", "DOUBLE", cancellationToken);
            await TryEnsureColumnAsync(connection, "BarcodeSubform", "BoxNo", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, "NonBarcodeSubform", "BoxNo", "TEXT(100)", cancellationToken);

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
            CREATE TABLE BoxMaster
            (
                BoxNo TEXT(50) NOT NULL CONSTRAINT PK_BoxMaster PRIMARY KEY,
                BoxName TEXT(100),
                Rack TEXT(50),
                NumberOfBoxes INTEGER,
                BoxWt DOUBLE,
                Serial INTEGER,
                Status TEXT(50),
                Remarks LONGTEXT,
                CreatedDate DATETIME,
                ModifiedDate DATETIME
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
            await using var idxRackCmd = new OleDbCommand("CREATE INDEX IX_BoxMaster_Rack ON BoxMaster (Rack)", connection);
            await idxRackCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        try
        {
            await using var idxSerialCmd = new OleDbCommand("CREATE INDEX IX_BoxMaster_Serial ON BoxMaster (Serial)", connection);
            await idxSerialCmd.ExecuteNonQueryAsync(cancellationToken);
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
        catch
        {
            // Column may already exist or table may be locked
        }
    }
}
