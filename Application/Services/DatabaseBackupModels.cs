namespace ConceptJewelleryPro.Application.Services;

public enum DatabaseEngineType
{
    MicrosoftAccess,
    SqlServer
}

public sealed class BackupFileInfo
{
    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string FormattedFileSize
    {
        get
        {
            if (FileSizeBytes < 1024)
                return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024)
                return $"{FileSizeBytes / 1024.0:F1} KB";
            if (FileSizeBytes < 1024L * 1024 * 1024)
                return $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
            return $"{FileSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    public DateTime CreatedAt { get; set; }

    public DatabaseEngineType EngineType { get; set; }

    public bool IsCompressed { get; set; }

    public string Extension => System.IO.Path.GetExtension(FilePath).ToLowerInvariant();
}

public sealed class BackupResult
{
    public bool Success { get; set; }

    public string? BackupFilePath { get; set; }

    public long FileSizeBytes { get; set; }

    public string? ErrorMessage { get; set; }

    public TimeSpan Duration { get; set; }

    public static BackupResult Succeeded(string filePath, long size, TimeSpan duration) => new()
    {
        Success = true,
        BackupFilePath = filePath,
        FileSizeBytes = size,
        Duration = duration
    };

    public static BackupResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

public sealed class RestoreResult
{
    public bool Success { get; set; }

    public string? SafetyBackupPath { get; set; }

    public string? ErrorMessage { get; set; }

    public static RestoreResult Succeeded(string? safetyBackupPath = null) => new()
    {
        Success = true,
        SafetyBackupPath = safetyBackupPath
    };

    public static RestoreResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
