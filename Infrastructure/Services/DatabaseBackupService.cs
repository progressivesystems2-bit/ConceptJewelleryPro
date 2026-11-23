using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using ConceptJewelleryPro.Application.Services;
using Microsoft.Data.SqlClient;

namespace ConceptJewelleryPro.Infrastructure.Services;

public sealed class DatabaseBackupService : IDatabaseBackupService
{
    public async Task<BackupResult> BackupAccessDatabaseAsync(
        string sourceDatabasePath,
        string targetFolder,
        bool compressZip,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(sourceDatabasePath) || !File.Exists(sourceDatabasePath))
            {
                return BackupResult.Failed($"Source database file not found at: '{sourceDatabasePath}'");
            }

            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                targetFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
            }

            Directory.CreateDirectory(targetFolder);

            var dbFileName = Path.GetFileNameWithoutExtension(sourceDatabasePath);
            var extension = Path.GetExtension(sourceDatabasePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string finalBackupPath;

            if (compressZip)
            {
                finalBackupPath = Path.Combine(targetFolder, $"Backup_{dbFileName}_{timestamp}.zip");
                var tempCopy = Path.Combine(Path.GetTempPath(), $"{dbFileName}_{Guid.NewGuid():N}{extension}");

                try
                {
                    // Copy to temp first to allow reading if opened by OleDb
                    File.Copy(sourceDatabasePath, tempCopy, true);

                    using (var zipStream = new FileStream(finalBackupPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                    {
                        var entryName = $"{dbFileName}_{timestamp}{extension}";
                        archive.CreateEntryFromFile(tempCopy, entryName, CompressionLevel.Optimal);
                    }
                }
                finally
                {
                    if (File.Exists(tempCopy))
                    {
                        try { File.Delete(tempCopy); } catch { }
                    }
                }
            }
            else
            {
                finalBackupPath = Path.Combine(targetFolder, $"Backup_{dbFileName}_{timestamp}{extension}");
                File.Copy(sourceDatabasePath, finalBackupPath, true);
            }

            sw.Stop();
            var fileInfo = new FileInfo(finalBackupPath);
            return BackupResult.Succeeded(finalBackupPath, fileInfo.Length, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return BackupResult.Failed($"Failed to create Access backup: {ex.Message}");
        }
    }

    public async Task<BackupResult> BackupSqlServerDatabaseAsync(
        string serverName,
        string databaseName,
        string? username,
        string? password,
        bool useWindowsAuth,
        string targetFolder,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(serverName))
            {
                return BackupResult.Failed("SQL Server name or instance cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return BackupResult.Failed("Database name cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                targetFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
            }

            Directory.CreateDirectory(targetFolder);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"Backup_{databaseName}_{timestamp}.bak";
            var backupFilePath = Path.Combine(targetFolder, backupFileName);

            var connectionString = BuildSqlConnectionString(serverName, databaseName, username, password, useWindowsAuth);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var query = $@"
                BACKUP DATABASE [{databaseName}]
                TO DISK = @BackupPath
                WITH FORMAT, INIT, COMPRESSION, NAME = @BackupName;
            ";

            await using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 300; // 5 minutes
            command.Parameters.AddWithValue("@BackupPath", backupFilePath);
            command.Parameters.AddWithValue("@BackupName", $"ConceptJewellery Full Backup - {timestamp}");

            await command.ExecuteNonQueryAsync(cancellationToken);

            sw.Stop();

            long fileSize = 0;
            if (File.Exists(backupFilePath))
            {
                fileSize = new FileInfo(backupFilePath).Length;
            }

            return BackupResult.Succeeded(backupFilePath, fileSize, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return BackupResult.Failed($"SQL Server Backup failed: {ex.Message}");
        }
    }

    public Task<IReadOnlyList<BackupFileInfo>> GetBackupHistoryAsync(
        string backupFolder,
        CancellationToken cancellationToken = default)
    {
        var list = new List<BackupFileInfo>();

        if (string.IsNullOrWhiteSpace(backupFolder) || !Directory.Exists(backupFolder))
        {
            return Task.FromResult<IReadOnlyList<BackupFileInfo>>(list);
        }

        try
        {
            var dirInfo = new DirectoryInfo(backupFolder);
            var files = dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.Extension.Equals(".mdb", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".accdb", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".bak", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.CreationTime > f.LastWriteTime ? f.CreationTime : f.LastWriteTime);

            foreach (var file in files)
            {
                var isZip = file.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
                var isBak = file.Extension.Equals(".bak", StringComparison.OrdinalIgnoreCase);

                var engine = isBak ? DatabaseEngineType.SqlServer : DatabaseEngineType.MicrosoftAccess;

                list.Add(new BackupFileInfo
                {
                    FileName = file.Name,
                    FilePath = file.FullName,
                    FileSizeBytes = file.Length,
                    CreatedAt = file.CreationTime > file.LastWriteTime ? file.CreationTime : file.LastWriteTime,
                    EngineType = engine,
                    IsCompressed = isZip
                });
            }
        }
        catch
        {
        }

        return Task.FromResult<IReadOnlyList<BackupFileInfo>>(list);
    }

    public async Task<RestoreResult> RestoreAccessDatabaseAsync(
        string backupFilePath,
        string currentDatabasePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            {
                return RestoreResult.Failed($"Backup file does not exist at: '{backupFilePath}'");
            }

            if (string.IsNullOrWhiteSpace(currentDatabasePath))
            {
                return RestoreResult.Failed("Current database destination path is not defined.");
            }

            var targetDir = Path.GetDirectoryName(currentDatabasePath);
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 1. Safety snapshot of current database
            string? safetyPath = null;
            if (File.Exists(currentDatabasePath))
            {
                var safetyDir = Path.Combine(AppContext.BaseDirectory, "Backups", "SafetySnapshots");
                Directory.CreateDirectory(safetyDir);
                var curName = Path.GetFileNameWithoutExtension(currentDatabasePath);
                var curExt = Path.GetExtension(currentDatabasePath);
                safetyPath = Path.Combine(safetyDir, $"Safety_{curName}_{DateTime.Now:yyyyMMdd_HHmmss}{curExt}");
                File.Copy(currentDatabasePath, safetyPath, true);
            }

            // 2. Restore from ZIP or direct MDB/ACCDB
            var isZip = backupFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

            if (isZip)
            {
                using var archive = ZipFile.OpenRead(backupFilePath);
                var dbEntry = archive.Entries.FirstOrDefault(e =>
                    e.Name.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.EndsWith(".accdb", StringComparison.OrdinalIgnoreCase));

                if (dbEntry is null)
                {
                    return RestoreResult.Failed("The selected zip file does not contain a valid .mdb or .accdb database file.");
                }

                dbEntry.ExtractToFile(currentDatabasePath, overwrite: true);
            }
            else
            {
                File.Copy(backupFilePath, currentDatabasePath, true);
            }

            return RestoreResult.Succeeded(safetyPath);
        }
        catch (Exception ex)
        {
            return RestoreResult.Failed($"Restore failed: {ex.Message}");
        }
    }

    public async Task<RestoreResult> RestoreSqlServerDatabaseAsync(
        string serverName,
        string databaseName,
        string? username,
        string? password,
        bool useWindowsAuth,
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            {
                return RestoreResult.Failed($"Backup file not found at: '{backupFilePath}'");
            }

            // Connect to master to restore database
            var masterConnStr = BuildSqlConnectionString(serverName, "master", username, password, useWindowsAuth);

            await using var connection = new SqlConnection(masterConnStr);
            await connection.OpenAsync(cancellationToken);

            var query = $@"
                IF EXISTS (SELECT name FROM sys.databases WHERE name = @DbName)
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                END

                RESTORE DATABASE [{databaseName}]
                FROM DISK = @BackupPath
                WITH REPLACE;

                ALTER DATABASE [{databaseName}] SET MULTI_USER;
            ";

            await using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 300;
            command.Parameters.AddWithValue("@DbName", databaseName);
            command.Parameters.AddWithValue("@BackupPath", backupFilePath);

            await command.ExecuteNonQueryAsync(cancellationToken);

            return RestoreResult.Succeeded();
        }
        catch (Exception ex)
        {
            return RestoreResult.Failed($"SQL Server Restore failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<string>> GetSqlServerDatabasesAsync(
        string serverName,
        string? username,
        string? password,
        bool useWindowsAuth,
        CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        try
        {
            var masterConnStr = BuildSqlConnectionString(serverName, "master", username, password, useWindowsAuth);
            await using var connection = new SqlConnection(masterConnStr);
            await connection.OpenAsync(cancellationToken);

            var query = "SELECT name FROM sys.databases WHERE database_id > 4 AND state_desc = 'ONLINE' ORDER BY name;";
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(reader.GetString(0));
            }
        }
        catch
        {
        }

        return list;
    }

    public async Task<bool> TestSqlServerConnectionAsync(
        string serverName,
        string? username,
        string? password,
        bool useWindowsAuth,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var masterConnStr = BuildSqlConnectionString(serverName, "master", username, password, useWindowsAuth);
            await using var connection = new SqlConnection(masterConnStr);
            await connection.OpenAsync(cancellationToken);
            return connection.State == ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildSqlConnectionString(
        string serverName,
        string databaseName,
        string? username,
        string? password,
        bool useWindowsAuth)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = serverName,
            InitialCatalog = databaseName,
            IntegratedSecurity = useWindowsAuth,
            TrustServerCertificate = true,
            ConnectTimeout = 15
        };

        if (!useWindowsAuth)
        {
            builder.UserID = username ?? string.Empty;
            builder.Password = password ?? string.Empty;
        }

        return builder.ConnectionString;
    }
}
