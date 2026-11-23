using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Application.Services;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class BackupViewModel : ViewModelBase
{
    private readonly IDatabaseBackupService _backupService;
    private readonly DatabaseSettingsStore _settingsStore;
    private readonly string _currentUser;
    private readonly Func<string?> _resolveAccessDbPath;

    private int _selectedProviderIndex;
    private string _accessSourcePath = string.Empty;
    private string _backupFolder = string.Empty;
    private bool _accessCompressToZip = true;

    private string _sqlServerName = string.Empty;
    private string _sqlDatabaseName = "ConceptJewelleryPro";
    private bool _sqlUseWindowsAuth = true;
    private string _sqlUsername = string.Empty;
    private string _sqlPassword = string.Empty;

    private BackupFileInfo? _selectedBackupFile;
    private bool _isBusy;
    private string _busyMessage = "Processing...";
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _statusMessage = string.Empty;

    public BackupViewModel(
        IDatabaseBackupService backupService,
        DatabaseSettingsStore settingsStore,
        Func<string?> resolveAccessDbPath,
        string currentUser = "admin")
    {
        _backupService = backupService;
        _settingsStore = settingsStore;
        _resolveAccessDbPath = resolveAccessDbPath;
        _currentUser = currentUser;

        BackupHistory = new ObservableCollection<BackupFileInfo>();
        AvailableDatabases = new ObservableCollection<string>();

        // Default backup folder
        var defaultFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
        BackupFolder = defaultFolder;

        // Commands
        TakeBackupCommand = new RelayCommand(async () => await TakeBackupAsync());
        BrowseSourceAccessDbCommand = new RelayCommand(BrowseSourceAccessDb);
        BrowseBackupFolderCommand = new RelayCommand(BrowseBackupFolder);
        OpenBackupFolderCommand = new RelayCommand(OpenBackupFolder);
        RefreshHistoryCommand = new RelayCommand(async () => await LoadBackupHistoryAsync());
        OpenFileLocationCommand = new RelayCommand(parameter => OpenFileLocation(parameter as BackupFileInfo ?? SelectedBackupFile));
        DeleteBackupCommand = new RelayCommand(parameter => DeleteBackup(parameter as BackupFileInfo ?? SelectedBackupFile));
        RestoreBackupCommand = new RelayCommand(async parameter => await RestoreBackupAsync(parameter as BackupFileInfo ?? SelectedBackupFile));
        TestSqlConnectionCommand = new RelayCommand(async () => await TestSqlConnectionAsync());
        LoadSqlDatabasesCommand = new RelayCommand(async () => await LoadSqlDatabasesAsync());

        InitializeFromSettings();
    }

    public ObservableCollection<BackupFileInfo> BackupHistory { get; }

    public ObservableCollection<string> AvailableDatabases { get; }

    public ICommand TakeBackupCommand { get; }

    public ICommand BrowseSourceAccessDbCommand { get; }

    public ICommand BrowseBackupFolderCommand { get; }

    public ICommand OpenBackupFolderCommand { get; }

    public ICommand RefreshHistoryCommand { get; }

    public ICommand OpenFileLocationCommand { get; }

    public ICommand DeleteBackupCommand { get; }

    public ICommand RestoreBackupCommand { get; }

    public ICommand TestSqlConnectionCommand { get; }

    public ICommand LoadSqlDatabasesCommand { get; }

    public int SelectedProviderIndex
    {
        get => _selectedProviderIndex;
        set
        {
            if (SetProperty(ref _selectedProviderIndex, value))
            {
                OnPropertyChanged(nameof(IsAccessSelected));
                OnPropertyChanged(nameof(IsSqlSelected));
                OnPropertyChanged(nameof(ProviderSummaryText));
            }
        }
    }

    public bool IsAccessSelected => SelectedProviderIndex == (int)DatabaseEngineType.MicrosoftAccess;

    public bool IsSqlSelected => SelectedProviderIndex == (int)DatabaseEngineType.SqlServer;

    public string ProviderSummaryText => IsAccessSelected
        ? "Active Mode: Microsoft Access (.mdb / .accdb)"
        : "Active Mode: Microsoft SQL Server";

    public string AccessSourcePath
    {
        get => _accessSourcePath;
        set => SetProperty(ref _accessSourcePath, value);
    }

    public string BackupFolder
    {
        get => _backupFolder;
        set
        {
            if (SetProperty(ref _backupFolder, value))
            {
                _ = LoadBackupHistoryAsync();
            }
        }
    }

    public bool AccessCompressToZip
    {
        get => _accessCompressToZip;
        set => SetProperty(ref _accessCompressToZip, value);
    }

    public string SqlServerName
    {
        get => _sqlServerName;
        set => SetProperty(ref _sqlServerName, value);
    }

    public string SqlDatabaseName
    {
        get => _sqlDatabaseName;
        set => SetProperty(ref _sqlDatabaseName, value);
    }

    public bool SqlUseWindowsAuth
    {
        get => _sqlUseWindowsAuth;
        set
        {
            if (SetProperty(ref _sqlUseWindowsAuth, value))
            {
                OnPropertyChanged(nameof(SqlIsSqlAuth));
            }
        }
    }

    public bool SqlIsSqlAuth => !SqlUseWindowsAuth;

    public string SqlUsername
    {
        get => _sqlUsername;
        set => SetProperty(ref _sqlUsername, value);
    }

    public string SqlPassword
    {
        get => _sqlPassword;
        set => SetProperty(ref _sqlPassword, value);
    }

    public BackupFileInfo? SelectedBackupFile
    {
        get => _selectedBackupFile;
        set => SetProperty(ref _selectedBackupFile, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public string? ToastMessage
    {
        get => _toastMessage;
        set => SetProperty(ref _toastMessage, value);
    }

    public ToastType ToastType
    {
        get => _toastType;
        set => SetProperty(ref _toastType, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public void InitializeFromSettings()
    {
        try
        {
            var settings = _settingsStore.Load();
            SelectedProviderIndex = settings.Provider == DatabaseProvider.MicrosoftAccess.ToString()
                ? (int)DatabaseEngineType.MicrosoftAccess
                : (int)DatabaseEngineType.SqlServer;

            var resolved = _resolveAccessDbPath() ?? settings.AccessDatabasePath;
            AccessSourcePath = !string.IsNullOrWhiteSpace(resolved) ? resolved : string.Empty;

            SqlServerName = !string.IsNullOrWhiteSpace(settings.SqlServerName) ? settings.SqlServerName : "localhost";
            SqlUsername = settings.SqlUsername;
            SqlPassword = settings.SqlPassword;
            SqlUseWindowsAuth = string.IsNullOrWhiteSpace(settings.SqlUsername);

            _ = LoadBackupHistoryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load database settings: {ex.Message}";
        }
    }

    public async Task TakeBackupAsync()
    {
        if (IsAccessSelected)
        {
            await TakeAccessBackupAsync();
        }
        else
        {
            await TakeSqlBackupAsync();
        }
    }

    private async Task TakeAccessBackupAsync()
    {
        if (string.IsNullOrWhiteSpace(AccessSourcePath) || !File.Exists(AccessSourcePath))
        {
            ShowToast("Source Access database file not found. Please browse to a valid .mdb/.accdb file.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        BusyMessage = "Creating Access database backup...";
        StatusMessage = "Backing up Microsoft Access database...";

        try
        {
            var result = await _backupService.BackupAccessDatabaseAsync(
                AccessSourcePath,
                BackupFolder,
                AccessCompressToZip);

            if (result.Success)
            {
                StatusMessage = $"Backup completed in {result.Duration.TotalSeconds:F1}s. Size: {result.FileSizeBytes / 1024.0 / 1024.0:F2} MB";
                ShowToast($"Backup created successfully: {Path.GetFileName(result.BackupFilePath)}", ToastType.Success);
                await LoadBackupHistoryAsync();
            }
            else
            {
                StatusMessage = $"Backup failed: {result.ErrorMessage}";
                ShowToast($"Backup failed: {result.ErrorMessage}", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup error: {ex.Message}";
            ShowToast($"Backup error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TakeSqlBackupAsync()
    {
        if (string.IsNullOrWhiteSpace(SqlServerName))
        {
            ShowToast("Please specify the SQL Server instance name.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SqlDatabaseName))
        {
            ShowToast("Please specify the SQL database name.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        BusyMessage = "Executing SQL Server backup...";
        StatusMessage = $"Backing up SQL Server database '{SqlDatabaseName}'...";

        try
        {
            var result = await _backupService.BackupSqlServerDatabaseAsync(
                SqlServerName,
                SqlDatabaseName,
                SqlUsername,
                SqlPassword,
                SqlUseWindowsAuth,
                BackupFolder);

            if (result.Success)
            {
                StatusMessage = $"SQL Server backup completed in {result.Duration.TotalSeconds:F1}s.";
                ShowToast($"SQL Backup created successfully: {Path.GetFileName(result.BackupFilePath)}", ToastType.Success);
                await LoadBackupHistoryAsync();
            }
            else
            {
                StatusMessage = $"SQL Backup failed: {result.ErrorMessage}";
                ShowToast($"SQL Backup failed: {result.ErrorMessage}", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"SQL Backup error: {ex.Message}";
            ShowToast($"SQL Backup error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadBackupHistoryAsync()
    {
        try
        {
            var history = await _backupService.GetBackupHistoryAsync(BackupFolder);
            BackupHistory.Clear();
            foreach (var item in history)
            {
                BackupHistory.Add(item);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load backup history: {ex.Message}";
        }
    }

    private void BrowseSourceAccessDb()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Access Database File",
                Filter = "Access Database (*.mdb;*.accdb)|*.mdb;*.accdb|All Files (*.*)|*.*",
                InitialDirectory = !string.IsNullOrWhiteSpace(AccessSourcePath) && File.Exists(AccessSourcePath)
                    ? Path.GetDirectoryName(AccessSourcePath)
                    : AppContext.BaseDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                AccessSourcePath = dialog.FileName;
            }
        }
        catch
        {
        }
    }

    private void BrowseBackupFolder()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Backup Destination Directory",
                InitialDirectory = !string.IsNullOrWhiteSpace(BackupFolder) && Directory.Exists(BackupFolder)
                    ? BackupFolder
                    : AppContext.BaseDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                BackupFolder = dialog.FolderName;
            }
        }
        catch
        {
        }
    }

    private void OpenBackupFolder()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(BackupFolder))
            {
                BackupFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
            }

            Directory.CreateDirectory(BackupFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = BackupFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to open backup folder: {ex.Message}", ToastType.Error);
        }
    }

    private void OpenFileLocation(BackupFileInfo? file)
    {
        if (file is null || !File.Exists(file.FilePath))
        {
            ShowToast("Backup file not found.", ToastType.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{file.FilePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowToast($"Error opening file location: {ex.Message}", ToastType.Error);
        }
    }

    private void DeleteBackup(BackupFileInfo? file)
    {
        if (file is null || !File.Exists(file.FilePath))
        {
            ShowToast("Select a backup file to delete.", ToastType.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to permanently delete the backup file:\n\n{file.FileName} ({file.FormattedFileSize})?",
            "Confirm Delete Backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                File.Delete(file.FilePath);
                BackupHistory.Remove(file);
                ShowToast($"Deleted backup: {file.FileName}", ToastType.Info);
            }
            catch (Exception ex)
            {
                ShowToast($"Failed to delete file: {ex.Message}", ToastType.Error);
            }
        }
    }

    private async Task RestoreBackupAsync(BackupFileInfo? file)
    {
        if (file is null || !File.Exists(file.FilePath))
        {
            ShowToast("Select a backup file to restore.", ToastType.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"WARNING: Restoring a database backup will overwrite existing records with data from:\n\nFile: {file.FileName}\nDate: {file.CreatedAt:dd/MM/yyyy HH:mm:ss}\nSize: {file.FormattedFileSize}\n\nAn automatic safety snapshot of the current database will be preserved.\n\nDo you want to proceed with database restore?",
            "Confirm Database Restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Exclamation);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = "Restoring database from backup...";
        StatusMessage = $"Restoring database from '{file.FileName}'...";

        try
        {
            if (file.EngineType == DatabaseEngineType.MicrosoftAccess)
            {
                var targetDb = AccessSourcePath;
                if (string.IsNullOrWhiteSpace(targetDb))
                {
                    targetDb = _resolveAccessDbPath() ?? Path.Combine(AppContext.BaseDirectory, "RGRG2526.mdb");
                }

                var restoreResult = await _backupService.RestoreAccessDatabaseAsync(file.FilePath, targetDb);
                if (restoreResult.Success)
                {
                    var msg = "Database restored successfully!";
                    if (!string.IsNullOrWhiteSpace(restoreResult.SafetyBackupPath))
                    {
                        msg += $"\nSafety copy saved to: {Path.GetFileName(restoreResult.SafetyBackupPath)}";
                    }
                    StatusMessage = "Database restored successfully.";
                    ShowToast(msg, ToastType.Success);
                    MessageBox.Show(msg, "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Restore failed: {restoreResult.ErrorMessage}";
                    ShowToast($"Restore failed: {restoreResult.ErrorMessage}", ToastType.Error);
                }
            }
            else
            {
                var restoreResult = await _backupService.RestoreSqlServerDatabaseAsync(
                    SqlServerName,
                    SqlDatabaseName,
                    SqlUsername,
                    SqlPassword,
                    SqlUseWindowsAuth,
                    file.FilePath);

                if (restoreResult.Success)
                {
                    StatusMessage = "SQL Server database restored successfully.";
                    ShowToast("SQL Server database restored successfully!", ToastType.Success);
                    MessageBox.Show("SQL Server database restored successfully.", "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Restore failed: {restoreResult.ErrorMessage}";
                    ShowToast($"Restore failed: {restoreResult.ErrorMessage}", ToastType.Error);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore error: {ex.Message}";
            ShowToast($"Restore error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TestSqlConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(SqlServerName))
        {
            ShowToast("Please enter a SQL Server name.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        BusyMessage = "Testing SQL Server connection...";
        try
        {
            var success = await _backupService.TestSqlServerConnectionAsync(
                SqlServerName,
                SqlUsername,
                SqlPassword,
                SqlUseWindowsAuth);

            if (success)
            {
                ShowToast("Connection test successful! SQL Server is reachable.", ToastType.Success);
                await LoadSqlDatabasesAsync();
            }
            else
            {
                ShowToast("Connection test failed. Check server name, credentials, or network connection.", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Connection error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSqlDatabasesAsync()
    {
        try
        {
            var databases = await _backupService.GetSqlServerDatabasesAsync(
                SqlServerName,
                SqlUsername,
                SqlPassword,
                SqlUseWindowsAuth);

            AvailableDatabases.Clear();
            foreach (var db in databases)
            {
                AvailableDatabases.Add(db);
            }

            if (AvailableDatabases.Count > 0 && !AvailableDatabases.Contains(SqlDatabaseName))
            {
                SqlDatabaseName = AvailableDatabases[0];
            }
        }
        catch
        {
        }
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastType = type;
        ToastMessage = message;
    }
}
