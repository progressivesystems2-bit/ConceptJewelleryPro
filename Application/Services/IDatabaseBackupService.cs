namespace ConceptJewelleryPro.Application.Services;

public interface IDatabaseBackupService
{
    Task<BackupResult> BackupAccessDatabaseAsync(
        string sourceDatabasePath,
        string targetFolder,
        bool compressZip,
        CancellationToken cancellationToken = default);

    Task<BackupResult> BackupSqlServerDatabaseAsync(
        string serverName,
        string databaseName,
        string? username,
        string? password,
        bool useWindowsAuth,
        string targetFolder,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupFileInfo>> GetBackupHistoryAsync(
        string backupFolder,
        CancellationToken cancellationToken = default);

    Task<RestoreResult> RestoreAccessDatabaseAsync(
        string backupFilePath,
        string currentDatabasePath,
        CancellationToken cancellationToken = default);

    Task<RestoreResult> RestoreSqlServerDatabaseAsync(
        string serverName,
        string databaseName,
        string? username,
        string? password,
        bool useWindowsAuth,
        string backupFilePath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSqlServerDatabasesAsync(
        string serverName,
        string? username,
        string? password,
        bool useWindowsAuth,
        CancellationToken cancellationToken = default);

    Task<bool> TestSqlServerConnectionAsync(
        string serverName,
        string? username,
        string? password,
        bool useWindowsAuth,
        CancellationToken cancellationToken = default);
}
