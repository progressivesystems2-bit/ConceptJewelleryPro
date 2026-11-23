using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Services;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class SoftwareUpdateViewModel : ViewModelBase
{
    private readonly SoftwareUpdateService _updateService;
    private CancellationTokenSource? _downloadCts;
    private UpdateCheckResult? _latestCheckResult;

    private bool _isBusy;
    private string _busyMessage = "Checking for updates...";
    private bool _isDownloading;
    private double _downloadProgress;
    private string _downloadStatusText = string.Empty;

    private string _statusBannerTitle = "Ready to check for updates";
    private string _statusBannerDetail = "Click 'Check for Updates' to query GitHub Releases for the latest version of Concept Jewellery Pro.";
    private string _latestVersionDisplay = "—";
    private string _releaseName = "—";
    private string _publishedDateDisplay = "—";
    private string _packageSizeDisplay = "—";
    private string _releaseNotes = "No release information loaded yet.";
    private string _lastCheckedDisplay = "Never";
    private bool _isUpdateAvailable;
    private bool _canDownloadUpdate;

    private string _gitHubOwner = "progressivesystems2-bit";
    private string _gitHubRepo = "ConceptJewelleryPro";
    private string _assetNamePattern = "ConceptJewelleryPro-WPF";
    private bool _includePreReleases;
    private string _gitHubToken = string.Empty;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public SoftwareUpdateViewModel(SoftwareUpdateService? updateService = null)
    {
        _updateService = updateService ?? new SoftwareUpdateService();

        CurrentVersion = _updateService.GetCurrentVersion();
        InstallDirectory = _updateService.GetInstallDirectory();
        RuntimeInfo = $"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.OSArchitecture})";

        LoadSavedSettings();

        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync(), () => !IsBusy && !IsDownloading);
        DownloadAndInstallCommand = new RelayCommand(async () => await DownloadAndInstallAsync(), () => CanDownloadUpdate && !IsDownloading && !IsBusy);
        CancelDownloadCommand = new RelayCommand(CancelDownload, () => IsDownloading);
        InstallFromLocalZipCommand = new RelayCommand(InstallFromLocalZip, () => !IsDownloading && !IsBusy);
        OpenReleasesPageCommand = new RelayCommand(OpenReleasesPage);
        OpenInstallFolderCommand = new RelayCommand(OpenInstallFolder);
        SaveSettingsCommand = new RelayCommand(SaveSettings, () => !IsDownloading);
    }

    public string CurrentVersion { get; }
    public string CurrentVersionLabel => $"v{CurrentVersion}";
    public string InstallDirectory { get; }
    public string RuntimeInfo { get; }

    public ICommand CheckForUpdatesCommand { get; }
    public ICommand DownloadAndInstallCommand { get; }
    public ICommand CancelDownloadCommand { get; }
    public ICommand InstallFromLocalZipCommand { get; }
    public ICommand OpenReleasesPageCommand { get; }
    public ICommand OpenInstallFolderCommand { get; }
    public ICommand SaveSettingsCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    public string DownloadStatusText
    {
        get => _downloadStatusText;
        set => SetProperty(ref _downloadStatusText, value);
    }

    public string StatusBannerTitle
    {
        get => _statusBannerTitle;
        set => SetProperty(ref _statusBannerTitle, value);
    }

    public string StatusBannerDetail
    {
        get => _statusBannerDetail;
        set => SetProperty(ref _statusBannerDetail, value);
    }

    public string LatestVersionDisplay
    {
        get => _latestVersionDisplay;
        set => SetProperty(ref _latestVersionDisplay, value);
    }

    public string ReleaseName
    {
        get => _releaseName;
        set => SetProperty(ref _releaseName, value);
    }

    public string PublishedDateDisplay
    {
        get => _publishedDateDisplay;
        set => SetProperty(ref _publishedDateDisplay, value);
    }

    public string PackageSizeDisplay
    {
        get => _packageSizeDisplay;
        set => SetProperty(ref _packageSizeDisplay, value);
    }

    public string ReleaseNotes
    {
        get => _releaseNotes;
        set => SetProperty(ref _releaseNotes, value);
    }

    public string LastCheckedDisplay
    {
        get => _lastCheckedDisplay;
        set => SetProperty(ref _lastCheckedDisplay, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    public bool CanDownloadUpdate
    {
        get => _canDownloadUpdate;
        set
        {
            if (SetProperty(ref _canDownloadUpdate, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string GitHubOwner
    {
        get => _gitHubOwner;
        set => SetProperty(ref _gitHubOwner, value);
    }

    public string GitHubRepo
    {
        get => _gitHubRepo;
        set => SetProperty(ref _gitHubRepo, value);
    }

    public string AssetNamePattern
    {
        get => _assetNamePattern;
        set => SetProperty(ref _assetNamePattern, value);
    }

    public bool IncludePreReleases
    {
        get => _includePreReleases;
        set => SetProperty(ref _includePreReleases, value);
    }

    public string GitHubToken
    {
        get => _gitHubToken;
        set => SetProperty(ref _gitHubToken, value);
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

    private void LoadSavedSettings()
    {
        var cfg = _updateService.LoadSettings();
        GitHubOwner = cfg.GitHubOwner;
        GitHubRepo = cfg.GitHubRepo;
        AssetNamePattern = cfg.AssetNamePattern;
        IncludePreReleases = cfg.IncludePreReleases;
        GitHubToken = cfg.GitHubToken;
    }

    private UpdateSettings BuildCurrentSettings() => new()
    {
        GitHubOwner = GitHubOwner.Trim(),
        GitHubRepo = GitHubRepo.Trim(),
        AssetNamePattern = AssetNamePattern.Trim(),
        IncludePreReleases = IncludePreReleases,
        GitHubToken = GitHubToken.Trim()
    };

    private void SaveSettings()
    {
        try
        {
            var cfg = BuildCurrentSettings();
            if (string.IsNullOrWhiteSpace(cfg.GitHubOwner) || string.IsNullOrWhiteSpace(cfg.GitHubRepo))
            {
                ShowToast("GitHub Owner and Repository Name are required.", ToastType.Warning);
                return;
            }

            _updateService.SaveSettings(cfg);
            ShowToast("Update settings saved.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save update settings: {ex.Message}", ToastType.Error);
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        if (IsBusy || IsDownloading) return;

        IsBusy = true;
        BusyMessage = $"Checking {GitHubOwner}/{GitHubRepo} for updates...";
        try
        {
            var result = await _updateService.CheckForUpdatesAsync(BuildCurrentSettings());
            _latestCheckResult = result;
            LastCheckedDisplay = DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt");

            if (!result.Success)
            {
                IsUpdateAvailable = false;
                CanDownloadUpdate = false;
                StatusBannerTitle = "Unable to check for updates";
                StatusBannerDetail = result.Message;
                ShowToast(result.Message, ToastType.Error);
                return;
            }

            LatestVersionDisplay = string.IsNullOrWhiteSpace(result.LatestVersion) ? "—" : $"v{result.LatestVersion}";
            ReleaseName = string.IsNullOrWhiteSpace(result.ReleaseName) ? "—" : result.ReleaseName;
            PublishedDateDisplay = result.PublishedAt.HasValue
                ? result.PublishedAt.Value.LocalDateTime.ToString("dd-MMM-yyyy hh:mm tt")
                : "—";
            PackageSizeDisplay = result.AssetSizeBytes > 0
                ? SoftwareUpdateService.FormatBytes(result.AssetSizeBytes)
                : "No ZIP asset";
            ReleaseNotes = string.IsNullOrWhiteSpace(result.ReleaseNotes)
                ? "No release notes available."
                : result.ReleaseNotes;

            IsUpdateAvailable = result.IsUpdateAvailable;
            CanDownloadUpdate = result.IsUpdateAvailable && !string.IsNullOrWhiteSpace(result.DownloadUrl);

            if (result.IsUpdateAvailable)
            {
                StatusBannerTitle = $"New Version Available: v{result.LatestVersion}";
                StatusBannerDetail = result.Message;
                ShowToast($"Update v{result.LatestVersion} is available!", ToastType.Success);
            }
            else
            {
                StatusBannerTitle = "You are up to date";
                StatusBannerDetail = result.Message;
                ShowToast(result.Message, ToastType.Info);
            }
        }
        catch (Exception ex)
        {
            StatusBannerTitle = "Update check failed";
            StatusBannerDetail = ex.Message;
            ShowToast($"Update check failed: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DownloadAndInstallAsync()
    {
        if (_latestCheckResult is null || string.IsNullOrWhiteSpace(_latestCheckResult.DownloadUrl))
        {
            ShowToast("No downloadable update package is available.", ToastType.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Download and install Concept Jewellery Pro v{_latestCheckResult.LatestVersion}?\n\n" +
            "Your local database and appsettings.json will be preserved automatically.\n" +
            "The application will restart once the update is ready.",
            "Confirm Software Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatusText = "Starting download...";

        try
        {
            var progress = new Progress<UpdateDownloadProgress>(p =>
            {
                DownloadProgress = p.Percentage;
                DownloadStatusText = p.StatusText;
            });

            var zipPath = await _updateService.DownloadUpdateAsync(
                _latestCheckResult,
                BuildCurrentSettings(),
                progress,
                _downloadCts.Token);

            DownloadStatusText = "Extracting and preparing installer...";
            _updateService.ApplyUpdateAndRestart(zipPath, _latestCheckResult.LatestVersion);

            ShowToast("Update ready. Restarting Concept Jewellery Pro...", ToastType.Success);
            System.Windows.Application.Current.Shutdown();
        }
        catch (OperationCanceledException)
        {
            DownloadStatusText = "Download cancelled.";
            ShowToast("Update download cancelled.", ToastType.Warning);
        }
        catch (Exception ex)
        {
            DownloadStatusText = $"Update failed: {ex.Message}";
            ShowToast($"Update failed: {ex.Message}", ToastType.Error);
            MessageBox.Show(
                $"Could not complete the update:\n\n{ex.Message}",
                "Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    private void InstallFromLocalZip()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Concept Jewellery Pro Update Package (.zip)",
            Filter = "Update Packages (*.zip)|*.zip|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            $"Install update from local package?\n\nFile: {Path.GetFileName(dialog.FileName)}\n\n" +
            "Your local database and appsettings.json will be preserved.\n" +
            "The application will restart automatically.",
            "Install Local Update Package",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            BusyMessage = "Validating and extracting local update package...";
            _updateService.ApplyUpdateAndRestart(dialog.FileName, "local");
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            IsBusy = false;
            ShowToast($"Local update failed: {ex.Message}", ToastType.Error);
            MessageBox.Show(
                $"Could not install the selected update package:\n\n{ex.Message}",
                "Local Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenReleasesPage()
    {
        try
        {
            var url = !string.IsNullOrWhiteSpace(_latestCheckResult?.ReleasePageUrl)
                ? _latestCheckResult.ReleasePageUrl
                : _updateService.GetReleasesPageUrl(BuildCurrentSettings());
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowToast($"Unable to open browser: {ex.Message}", ToastType.Error);
        }
    }

    private void OpenInstallFolder()
    {
        try
        {
            if (Directory.Exists(InstallDirectory))
            {
                Process.Start(new ProcessStartInfo(InstallDirectory) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Unable to open folder: {ex.Message}", ToastType.Error);
        }
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = null;
        ToastType = type;
        ToastMessage = message;
    }

    private void RaiseCommandStates()
    {
        (CheckForUpdatesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DownloadAndInstallCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelDownloadCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (InstallFromLocalZipCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
