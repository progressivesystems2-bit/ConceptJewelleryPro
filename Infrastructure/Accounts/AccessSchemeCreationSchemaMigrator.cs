using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessSchemeCreationSchemaMigrator
{
    private static readonly HashSet<string> _migratedConnections = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    public static async Task EnsureSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        lock (_lock)
        {
            if (_migratedConnections.Contains(connectionString))
                return;
        }

        try
        {
            await using var connection = new OleDbConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await EnsureSchemeCreationTableAsync(connection, cancellationToken);

            lock (_lock)
            {
                _migratedConnections.Add(connectionString);
            }
        }
        catch
        {
            // Non-fatal if schema cannot be locked or modified right now
        }
    }

    public static void EnsureSchemaSync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        lock (_lock)
        {
            if (_migratedConnections.Contains(connectionString))
                return;
        }

        try
        {
            using var connection = new OleDbConnection(connectionString);
            connection.Open();

            EnsureSchemeCreationTableSync(connection);

            lock (_lock)
            {
                _migratedConnections.Add(connectionString);
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    private static async Task EnsureSchemeCreationTableAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        bool exists = false;
        try
        {
            await using var checkCmd = new OleDbCommand("SELECT TOP 1 Scode FROM [SchemeCreation]", connection);
            await checkCmd.ExecuteScalarAsync(cancellationToken);
            exists = true;
        }
        catch
        {
            exists = false;
        }

        if (!exists)
        {
            const string createSql = @"
                CREATE TABLE [SchemeCreation] (
                    [Scode] TEXT(50) NOT NULL CONSTRAINT PK_SchemeCreation PRIMARY KEY,
                    [Nos] INTEGER,
                    [CustomerName] TEXT(100),
                    [InstallmentAmt] CURRENCY,
                    [Month] CURRENCY,
                    [TotalAmt] CURRENCY,
                    [SchemeDiscount] CURRENCY,
                    [TotalValue] CURRENCY,
                    [SDate] DATETIME,
                    [EDate] DATETIME,
                    [Status] TEXT(25),
                    [Closed] YESNO,
                    [Staff] TEXT(50),
                    [EntryUser] TEXT(50),
                    [EntryDate] DATETIME,
                    [EntryTime] DATETIME,
                    [ComputerName] TEXT(50)
                )";

            try
            {
                await using var cmd = new OleDbCommand(createSql, connection);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
            }
        }
        else
        {
            await TryEnsureColumnAsync(connection, "SchemeCreation", "Staff", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "SchemeCreation", "Closed", "YESNO", cancellationToken);
            await TryEnsureColumnAsync(connection, "SchemeCreation", "CustomerName", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, "SchemeCreation", "TotalValue", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "SchemeCreation", "SchemeDiscount", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "SchemeCreation", "Status", "TEXT(25)", cancellationToken);
            await TryEnsureColumnAsync(connection, "SchemeCreation", "EntryUser", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "SchemeCreation", "ComputerName", "TEXT(50)", cancellationToken);
        }
    }

    private static void EnsureSchemeCreationTableSync(OleDbConnection connection)
    {
        bool exists = false;
        try
        {
            using var checkCmd = new OleDbCommand("SELECT TOP 1 Scode FROM [SchemeCreation]", connection);
            checkCmd.ExecuteScalar();
            exists = true;
        }
        catch
        {
            exists = false;
        }

        if (!exists)
        {
            const string createSql = @"
                CREATE TABLE [SchemeCreation] (
                    [Scode] TEXT(50) NOT NULL CONSTRAINT PK_SchemeCreation PRIMARY KEY,
                    [Nos] INTEGER,
                    [CustomerName] TEXT(100),
                    [InstallmentAmt] CURRENCY,
                    [Month] CURRENCY,
                    [TotalAmt] CURRENCY,
                    [SchemeDiscount] CURRENCY,
                    [TotalValue] CURRENCY,
                    [SDate] DATETIME,
                    [EDate] DATETIME,
                    [Status] TEXT(25),
                    [Closed] YESNO,
                    [Staff] TEXT(50),
                    [EntryUser] TEXT(50),
                    [EntryDate] DATETIME,
                    [EntryTime] DATETIME,
                    [ComputerName] TEXT(50)
                )";

            try
            {
                using var cmd = new OleDbCommand(createSql, connection);
                cmd.ExecuteNonQuery();
            }
            catch
            {
            }
        }
        else
        {
            TryEnsureColumnSync(connection, "SchemeCreation", "Staff", "TEXT(50)");
            TryEnsureColumnSync(connection, "SchemeCreation", "Closed", "YESNO");
            TryEnsureColumnSync(connection, "SchemeCreation", "CustomerName", "TEXT(100)");
            TryEnsureColumnSync(connection, "SchemeCreation", "TotalValue", "CURRENCY");
            TryEnsureColumnSync(connection, "SchemeCreation", "SchemeDiscount", "CURRENCY");
            TryEnsureColumnSync(connection, "SchemeCreation", "Status", "TEXT(25)");
            TryEnsureColumnSync(connection, "SchemeCreation", "EntryUser", "TEXT(50)");
            TryEnsureColumnSync(connection, "SchemeCreation", "ComputerName", "TEXT(50)");
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
            // Column may already exist or table locked
        }
    }

    private static void TryEnsureColumnSync(OleDbConnection connection, string table, string column, string typeDefinition)
    {
        try
        {
            using var command = new OleDbCommand($"ALTER TABLE [{table}] ADD COLUMN [{column}] {typeDefinition}", connection);
            command.ExecuteNonQuery();
        }
        catch
        {
            // Column may already exist or table locked
        }
    }
}