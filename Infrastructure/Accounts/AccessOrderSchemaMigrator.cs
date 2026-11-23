using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessOrderSchemaMigrator
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

            await EnsureOrderTableAsync(connection, cancellationToken);

            lock (_lock)
            {
                _migratedConnections.Add(connectionString);
            }
        }
        catch
        {
            // Non-fatal fallback
        }
    }

    public static void EnsureSchemaSync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        lock (_lock)
        {
            if (_migratedConnections.Contains(connectionString))
                return;
        }

        try
        {
            using var connection = new OleDbConnection(connectionString);
            connection.Open();

            EnsureOrderTableSync(connection);

            lock (_lock)
            {
                _migratedConnections.Add(connectionString);
            }
        }
        catch
        {
        }
    }

    private static async Task EnsureOrderTableAsync(OleDbConnection connection, CancellationToken cancellationToken)
    {
        bool exists = false;
        try
        {
            await using var checkCmd = new OleDbCommand("SELECT TOP 1 OrderNo FROM [Order]", connection);
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
                CREATE TABLE [Order] (
                    AutoNo AUTOINCREMENT CONSTRAINT PK_Order PRIMARY KEY,
                    OrderNo TEXT(50),
                    Nos INTEGER,
                    OrderDate DATETIME,
                    DeliveryDate DATETIME,
                    CustomerName TEXT(100),
                    Address TEXT(150),
                    Address2 TEXT(150),
                    City TEXT(50),
                    Phone TEXT(50),
                    ItemName TEXT(100),
                    Qty CURRENCY,
                    Size TEXT(50),
                    Description LONGTEXT,
                    Purity TEXT(50),
                    AppWeight CURRENCY,
                    MakPerGr CURRENCY,
                    Making CURRENCY,
                    MetalRate CURRENCY,
                    AppValue CURRENCY,
                    RateType TEXT(50),
                    Photo TEXT(255),
                    EntryUser TEXT(50),
                    EntryDate DATETIME,
                    EntryTime DATETIME,
                    [Status] TEXT(50),
                    ComputerName TEXT(50),
                    Closed YESNO,
                    StoneWt CURRENCY,
                    StoneValue CURRENCY,
                    StoneName TEXT(50),
                    KarigarName TEXT(100),
                    Company TEXT(100),
                    StyleNo TEXT(50),
                    CCode TEXT(50),
                    CompanyPerson TEXT(50),
                    OrderDeliveryDate DATETIME,
                    AdvancePaid CURRENCY,
                    OrderCategory TEXT(50)
                )";

            await using var cmd = new OleDbCommand(createSql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await TryExecuteCommandAsync(connection, "CREATE INDEX IX_Order_OrderNo ON [Order] (OrderNo)", cancellationToken);
            await TryExecuteCommandAsync(connection, "CREATE INDEX IX_Order_CustomerName ON [Order] (CustomerName)", cancellationToken);
        }
        else
        {
            await TryEnsureColumnAsync(connection, "Order", "OrderNo", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Nos", "INTEGER", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "OrderDate", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "DeliveryDate", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "CustomerName", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Address", "TEXT(150)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Address2", "TEXT(150)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "City", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Phone", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "ItemName", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Qty", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Size", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Description", "LONGTEXT", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Purity", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "AppWeight", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "MakPerGr", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Making", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "MetalRate", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "AppValue", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "RateType", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Photo", "TEXT(255)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "EntryUser", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "EntryDate", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "EntryTime", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Status", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "ComputerName", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Closed", "YESNO", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "StoneWt", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "StoneValue", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "StoneName", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "KarigarName", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "Company", "TEXT(100)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "StyleNo", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "CCode", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "CompanyPerson", "TEXT(50)", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "OrderDeliveryDate", "DATETIME", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "AdvancePaid", "CURRENCY", cancellationToken);
            await TryEnsureColumnAsync(connection, "Order", "OrderCategory", "TEXT(50)", cancellationToken);
        }
    }

    private static void EnsureOrderTableSync(OleDbConnection connection)
    {
        bool exists = false;
        try
        {
            using var checkCmd = new OleDbCommand("SELECT TOP 1 OrderNo FROM [Order]", connection);
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
                CREATE TABLE [Order] (
                    AutoNo AUTOINCREMENT CONSTRAINT PK_Order PRIMARY KEY,
                    OrderNo TEXT(50),
                    Nos INTEGER,
                    OrderDate DATETIME,
                    DeliveryDate DATETIME,
                    CustomerName TEXT(100),
                    Address TEXT(150),
                    Address2 TEXT(150),
                    City TEXT(50),
                    Phone TEXT(50),
                    ItemName TEXT(100),
                    Qty CURRENCY,
                    Size TEXT(50),
                    Description LONGTEXT,
                    Purity TEXT(50),
                    AppWeight CURRENCY,
                    MakPerGr CURRENCY,
                    Making CURRENCY,
                    MetalRate CURRENCY,
                    AppValue CURRENCY,
                    RateType TEXT(50),
                    Photo TEXT(255),
                    EntryUser TEXT(50),
                    EntryDate DATETIME,
                    EntryTime DATETIME,
                    [Status] TEXT(50),
                    ComputerName TEXT(50),
                    Closed YESNO,
                    StoneWt CURRENCY,
                    StoneValue CURRENCY,
                    StoneName TEXT(50),
                    KarigarName TEXT(100),
                    Company TEXT(100),
                    StyleNo TEXT(50),
                    CCode TEXT(50),
                    CompanyPerson TEXT(50),
                    OrderDeliveryDate DATETIME,
                    AdvancePaid CURRENCY,
                    OrderCategory TEXT(50)
                )";

            using var cmd = new OleDbCommand(createSql, connection);
            cmd.ExecuteNonQuery();

            TryExecuteCommandSync(connection, "CREATE INDEX IX_Order_OrderNo ON [Order] (OrderNo)");
            TryExecuteCommandSync(connection, "CREATE INDEX IX_Order_CustomerName ON [Order] (CustomerName)");
        }
        else
        {
            TryEnsureColumnSync(connection, "Order", "OrderNo", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "Nos", "INTEGER");
            TryEnsureColumnSync(connection, "Order", "OrderDate", "DATETIME");
            TryEnsureColumnSync(connection, "Order", "DeliveryDate", "DATETIME");
            TryEnsureColumnSync(connection, "Order", "CustomerName", "TEXT(100)");
            TryEnsureColumnSync(connection, "Order", "Address", "TEXT(150)");
            TryEnsureColumnSync(connection, "Order", "Address2", "TEXT(150)");
            TryEnsureColumnSync(connection, "Order", "City", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "Phone", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "ItemName", "TEXT(100)");
            TryEnsureColumnSync(connection, "Order", "Qty", "CURRENCY");
            TryEnsureColumnSync(connection, "Order", "Size", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "Description", "LONGTEXT");
            TryEnsureColumnSync(connection, "Order", "Purity", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "AppWeight", "CURRENCY");
            TryEnsureColumnSync(connection, "Order", "MakPerGr", "CURRENCY");
            TryEnsureColumnSync(connection, "Order", "Making", "CURRENCY");
            TryEnsureColumnSync(connection, "Order", "MetalRate", "CURRENCY");
            TryEnsureColumnSync(connection, "Order", "AppValue", "CURRENCY");
            TryEnsureColumnSync(connection, "Order", "RateType", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "Photo", "TEXT(255)");
            TryEnsureColumnSync(connection, "Order", "EntryUser", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "EntryDate", "DATETIME");
            TryEnsureColumnSync(connection, "Order", "EntryTime", "DATETIME");
            TryEnsureColumnSync(connection, "Order", "Status", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "ComputerName", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "Closed", "YESNO");
            TryEnsureColumnSync(connection, "Order", "StoneWt", "CURRENCY");
            TryEnsureColumnSync(connection, "Order", "StoneValue", "CURRENCY");
            TryEnsureColumnSync(connection, "Order", "StoneName", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "KarigarName", "TEXT(100)");
            TryEnsureColumnSync(connection, "Order", "Company", "TEXT(100)");
            TryEnsureColumnSync(connection, "Order", "StyleNo", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "CCode", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "CompanyPerson", "TEXT(50)");
            TryEnsureColumnSync(connection, "Order", "OrderDeliveryDate", "DATETIME");
            TryEnsureColumnSync(connection, "Order", "AdvancePaid", "CURRENCY");
            TryEnsureColumnSync(connection, "Order", "OrderCategory", "TEXT(50)");
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

    private static void TryEnsureColumnSync(OleDbConnection connection, string table, string column, string typeDefinition)
    {
        try
        {
            using var command = new OleDbCommand($"ALTER TABLE [{table}] ADD COLUMN [{column}] {typeDefinition}", connection);
            command.ExecuteNonQuery();
        }
        catch
        {
        }
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

    private static void TryExecuteCommandSync(OleDbConnection connection, string sql)
    {
        try
        {
            using var command = new OleDbCommand(sql, connection);
            command.ExecuteNonQuery();
        }
        catch
        {
        }
    }
}
