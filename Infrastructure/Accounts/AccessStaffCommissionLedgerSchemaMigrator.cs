using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessStaffCommissionLedgerSchemaMigrator
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

            // Ensure Staff table exists
            await EnsureStaffTableAsync(connection, cancellationToken);

            // Ensure StaffCcommisionLedger table exists (primary name in legacy DBs)
            await EnsureLedgerTableAsync(connection, "StaffCcommisionLedger", cancellationToken);

            // Ensure StaffCommissionLedger table exists (alias / corrected spelling)
            await EnsureLedgerTableAsync(connection, "StaffCommissionLedger", cancellationToken);

            // Ensure StaffCommissionRule table exists
            await EnsureRuleTableAsync(connection, "StaffCommissionRule", cancellationToken);

            // Ensure StaffCcommisionRule table exists (alias / legacy spelling)
            await EnsureRuleTableAsync(connection, "StaffCcommisionRule", cancellationToken);

            lock (_lock)
            {
                _migratedConnections.Add(connectionString);
            }
        }
        catch
        {
            // Non-fatal if schema cannot be modified right now
        }
    }

    private static async Task EnsureStaffTableAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        bool exists = false;
        try
        {
            await using var checkCmd = new OleDbCommand("SELECT TOP 1 Staff FROM Staff", connection);
            await checkCmd.ExecuteScalarAsync(cancellationToken);
            exists = true;
        }
        catch
        {
            exists = false;
        }

        if (!exists)
        {
            const string createStaffSql = @"
                CREATE TABLE Staff
                (
                    Staff TEXT(50) NOT NULL CONSTRAINT PK_Staff PRIMARY KEY,
                    ComputerName TEXT(50),
                    EntryDate DATETIME,
                    EntryUser TEXT(50),
                    EntryTime DATETIME,
                    [Status] TEXT(50)
                )";

            await ExecuteCommandAsync(connection, createStaffSql, cancellationToken);
        }
        else
        {
            await TryEnsureColumnAsync(connection, "Staff", "ComputerName", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Staff", "EntryDate", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, "Staff", "EntryUser", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Staff", "EntryTime", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, "Staff", "Status", "TEXT(50)", cancellationToken);
        }
    }

    private static async Task EnsureLedgerTableAsync(OleDbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        bool exists = false;
        try
        {
            await using var checkCmd = new OleDbCommand($"SELECT TOP 1 Vno FROM [{tableName}]", connection);
            await checkCmd.ExecuteScalarAsync(cancellationToken);
            exists = true;
        }
        catch
        {
            exists = false;
        }

        if (!exists)
        {
            string createTableSql = $@"
                CREATE TABLE [{tableName}]
                (
                    AutoNo AUTOINCREMENT CONSTRAINT PK_{tableName} PRIMARY KEY,
                    Nos INTEGER,
                    Vno TEXT(50),
                    Vdate DATETIME,
                    StaffCode TEXT(50),
                    StaffName TEXT(100),
                    NetCommission CURRENCY,
                    PaidAmount CURRENCY,
                    BalanceAmount CURRENCY,
                    Remarks LONGTEXT,
                    EntryUser TEXT(50),
                    EntryDate DATETIME,
                    [Status] TEXT(50)
                )";

            await ExecuteCommandAsync(connection, createTableSql, cancellationToken);
            await TryExecuteCommandAsync(connection, $"CREATE INDEX IX_{tableName}_Vno ON [{tableName}] (Vno)", cancellationToken);
            await TryExecuteCommandAsync(connection, $"CREATE INDEX IX_{tableName}_StaffCode ON [{tableName}] (StaffCode)", cancellationToken);
            await TryExecuteCommandAsync(connection, $"CREATE INDEX IX_{tableName}_Vdate ON [{tableName}] (Vdate)", cancellationToken);
        }
        else
        {
            await TryEnsureColumnAsync(connection, tableName, "Nos", "INTEGER", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "Vno", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "Vdate", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "StaffCode", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "StaffName", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "NetCommission", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "PaidAmount", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "BalanceAmount", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "Remarks", "LONGTEXT", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "EntryUser", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "EntryDate", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "Status", "TEXT(50)", cancellationToken);
        }
    }

    private static async Task EnsureRuleTableAsync(OleDbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        bool exists = false;
        try
        {
            await using var checkCmd = new OleDbCommand($"SELECT TOP 1 RuleId FROM [{tableName}]", connection);
            await checkCmd.ExecuteScalarAsync(cancellationToken);
            exists = true;
        }
        catch
        {
            exists = false;
        }

        if (!exists)
        {
            string createTableSql = $@"
                CREATE TABLE [{tableName}]
                (
                    AutoNo AUTOINCREMENT CONSTRAINT PK_{tableName} PRIMARY KEY,
                    RuleId TEXT(50),
                    RuleName TEXT(100),
                    StaffCode TEXT(50),
                    StaffName TEXT(100),
                    Category TEXT(50),
                    ItemHead TEXT(50),
                    ItemType TEXT(50),
                    CommissionPer CURRENCY,
                    CommissionRate CURRENCY,
                    MinAmount CURRENCY,
                    MaxAmount CURRENCY,
                    EffectiveFrom DATETIME,
                    EffectiveTo DATETIME,
                    Remarks LONGTEXT,
                    EntryUser TEXT(50),
                    EntryDate DATETIME,
                    [Status] TEXT(50)
                )";

            await ExecuteCommandAsync(connection, createTableSql, cancellationToken);
            await TryExecuteCommandAsync(connection, $"CREATE INDEX IX_{tableName}_RuleId ON [{tableName}] (RuleId)", cancellationToken);
            await TryExecuteCommandAsync(connection, $"CREATE INDEX IX_{tableName}_StaffCode ON [{tableName}] (StaffCode)", cancellationToken);
        }
        else
        {
            await TryEnsureColumnAsync(connection, tableName, "RuleId", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "RuleName", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "StaffCode", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "StaffName", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "Category", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "ItemHead", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "ItemType", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "CommissionPer", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "CommissionRate", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "MinAmount", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "MaxAmount", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "EffectiveFrom", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "EffectiveTo", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "Remarks", "LONGTEXT", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "EntryUser", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "EntryDate", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, tableName, "Status", "TEXT(50)", cancellationToken);
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
        }
    }
}
