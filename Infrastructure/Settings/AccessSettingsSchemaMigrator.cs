using System.Data.OleDb;

namespace ConceptJewelleryPro.Infrastructure.Settings;

public static class AccessSettingsSchemaMigrator
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
                await using var checkCmd = new OleDbCommand("SELECT TOP 1 SettingsName FROM Settings", connection);
                await checkCmd.ExecuteScalarAsync(cancellationToken);
                tableExists = true;
            }
            catch
            {
                tableExists = false;
            }

            if (!tableExists)
            {
                const string createSettingsSql = @"
                    CREATE TABLE Settings
                    (
                        SettingsName TEXT(100) NOT NULL CONSTRAINT PK_Settings PRIMARY KEY,
                        SettingsValue LONGTEXT
                    )";

                await ExecuteCommandAsync(connection, createSettingsSql, cancellationToken);
                await TryExecuteCommandAsync(connection, "CREATE INDEX IX_Settings_SettingsName ON Settings (SettingsName)", cancellationToken);
            }

            // Ensure SoftwarePath is seeded with production software executable path if not present
            try
            {
                await using var checkPathCmd = new OleDbCommand("SELECT SettingsValue FROM [Settings] WHERE [SettingsName] = 'SoftwarePath'", connection);
                var existing = await checkPathCmd.ExecuteScalarAsync(cancellationToken);
                if (existing == null || string.IsNullOrWhiteSpace(existing.ToString()))
                {
                    await using var insertCmd = new OleDbCommand("INSERT INTO [Settings] ([SettingsName], [SettingsValue]) VALUES ('SoftwarePath', 'E:\\TATTVA JEWELS26-27\\net8.0-windows')", connection);
                    await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            catch
            {
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
            // Ignore if index already exists or table locked
        }
    }
}
