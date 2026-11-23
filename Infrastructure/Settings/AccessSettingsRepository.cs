using System.Data.OleDb;
using ConceptJewelleryPro.Data.Access.Repositories;

using ConceptJewelleryPro.Infrastructure.Accounts;

namespace ConceptJewelleryPro.Infrastructure.Settings;

public sealed class AccessSettingsRepository : ISettingsRepository
{
    private readonly string? _databasePath;
    private readonly string _connectionString;
    private static bool _schemaEnsured;
    private static readonly object _schemaLock = new();
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _cacheLoaded;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AccessSettingsRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = AccessConnectionProvider.GetConnectionString(databasePath);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured)
            return;

        if (string.IsNullOrWhiteSpace(_databasePath) || !File.Exists(_databasePath))
            return;

        try
        {
            await AccessSettingsSchemaMigrator.EnsureSchemaAsync(_connectionString, cancellationToken);
            lock (_schemaLock)
            {
                _schemaEnsured = true;
            }
        }
        catch
        {
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cacheLoaded)
        {
            lock (_cache)
            {
                return new Dictionary<string, string>(_cache, StringComparer.OrdinalIgnoreCase);
            }
        }

        if (string.IsNullOrWhiteSpace(_databasePath) || !File.Exists(_databasePath))
        {
            lock (_cache)
            {
                return new Dictionary<string, string>(_cache, StringComparer.OrdinalIgnoreCase);
            }
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cacheLoaded)
            {
                lock (_cache)
                {
                    return new Dictionary<string, string>(_cache, StringComparer.OrdinalIgnoreCase);
                }
            }

            await EnsureSchemaAsync(cancellationToken);

            const string sql = "SELECT SettingsName, SettingsValue FROM Settings";
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            await using var connection = new OleDbConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader["SettingsName"]?.ToString();
                var val = reader["SettingsValue"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    dict[key] = val;
                }
            }

            lock (_cache)
            {
                _cache.Clear();
                foreach (var kv in dict)
                {
                    _cache[kv.Key] = kv.Value;
                }
                _cacheLoaded = true;
                return new Dictionary<string, string>(_cache, StringComparer.OrdinalIgnoreCase);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var all = await GetAllAsync(cancellationToken);
        return all.TryGetValue(key, out var val) ? val : null;
    }

    public async Task SaveAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await UpsertSingleAsync(connection, key.Trim(), value ?? string.Empty, cancellationToken);

        lock (_cache)
        {
            _cache[key.Trim()] = value ?? string.Empty;
        }
    }

    public async Task SaveManyAsync(IEnumerable<KeyValuePair<string, string?>> settings, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var kvp in settings)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                continue;

            var k = kvp.Key.Trim();
            var v = kvp.Value ?? string.Empty;
            await UpsertSingleAsync(connection, k, v, cancellationToken);

            lock (_cache)
            {
                _cache[k] = v;
            }
        }
    }

    private static async Task UpsertSingleAsync(OleDbConnection connection, string key, string value, CancellationToken cancellationToken)
    {
        const string updateSql = "UPDATE [Settings] SET [SettingsValue] = ? WHERE [SettingsName] = ?";
        await using (var updateCmd = new OleDbCommand(updateSql, connection))
        {
            updateCmd.Parameters.Add("@SettingsValue", OleDbType.LongVarWChar).Value = value;
            updateCmd.Parameters.Add("@SettingsName", OleDbType.VarWChar, 100).Value = key;

            var rows = await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            if (rows > 0)
                return;
        }

        const string insertSql = "INSERT INTO [Settings] ([SettingsName], [SettingsValue]) VALUES (?, ?)";
        await using (var insertCmd = new OleDbCommand(insertSql, connection))
        {
            insertCmd.Parameters.Add("@SettingsName", OleDbType.VarWChar, 100).Value = key;
            insertCmd.Parameters.Add("@SettingsValue", OleDbType.LongVarWChar).Value = value;

            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
