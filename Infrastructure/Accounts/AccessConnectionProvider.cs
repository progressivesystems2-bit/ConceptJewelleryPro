using System;
using System.Data.OleDb;
using System.IO;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public static class AccessConnectionProvider
{
    private static string? _cachedWorkingProvider;
    private static readonly object _lock = new();

    public static string[] CandidateProviders => new[]
    {
        "Microsoft.ACE.OLEDB.16.0",
        "Microsoft.ACE.OLEDB.12.0",
        "Microsoft.Jet.OLEDB.4.0"
    };

    public static string GetConnectionString(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) return string.Empty;

        lock (_lock)
        {
            if (_cachedWorkingProvider != null)
            {
                return $"Provider={_cachedWorkingProvider};Data Source={databasePath};Persist Security Info=False;";
            }

            foreach (var provider in CandidateProviders)
            {
                var connStr = $"Provider={provider};Data Source={databasePath};Persist Security Info=False;";
                try
                {
                    using var conn = new OleDbConnection(connStr);
                    conn.Open();
                    _cachedWorkingProvider = provider;
                    return connStr;
                }
                catch
                {
                    // Try next provider
                }
            }

            // Default fallback
            return $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
        }
    }

    public static (bool Success, string? WorkingProvider, string? LastError) TestConnection(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            return (false, null, "Database file does not exist on disk.");
        }

        string? lastError = null;
        foreach (var provider in CandidateProviders)
        {
            var connStr = $"Provider={provider};Data Source={databasePath};Persist Security Info=False;";
            try
            {
                using var conn = new OleDbConnection(connStr);
                conn.Open();
                _cachedWorkingProvider = provider;
                return (true, provider, null);
            }
            catch (Exception ex)
            {
                lastError = $"[{provider} failed]: {ex.Message}";
            }
        }

        return (false, null, lastError);
    }
}
