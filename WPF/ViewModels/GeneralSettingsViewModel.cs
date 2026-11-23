using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class GeneralSettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly string _currentUser;

    private string _savePath = string.Empty;
    private string _printFiles = Infrastructure.Printing.FastReportPrintService.DefaultPrintFilesPath;
    private string _waSendMethod = "API";
    private string _waApiKey = string.Empty;
    private string _waTemplate = string.Empty;
    private string _waApiTemplateName = string.Empty;
    private string _waApiLanguage = "en_US";
    private string _waApiParameters = "{CustomerName}, {InvoiceNo}, {BillDate}, {NetAmount}, {Balance}, {StoreName}";
    private bool _waAttachPdf;
    private string _waApiDocumentUrl = string.Empty;
    private string _waBusinessNo = string.Empty;
    private bool _waAutoSend;
    private string _einvoiceMode = "DEMO";
    private string _allowNtWtModUpto = "0.000";
    private string _anniversaryMessage = string.Empty;
    private string _birthdayMessage = string.Empty;
    private string _wishesMode = "WhatsappCloud";

    // Weight Machine
    private bool _weightEnabled;
    private string _weightCommPort = "COM1";
    private string _weightBaudRate = "9600";
    private string _weightDataBits = "8";
    private string _weightStopBits = "1";
    private string _weightParity = "None";
    private string _weightHandshake = "None";
    private string _weightStablePrefix = "ST,GS";
    private string _weightTimeoutMs = "500";

    // Web Server
    private string _webServerPort = "5080";
    private string _webServerHost = "0.0.0.0";
    private string _webServerExePath = string.Empty;
    private bool _isWebServerRunning;
    private string _webServerStatusText = "Checking...";
    private bool _isAutoStartInstalled;
    private string _autoStartStatusText = "Checking...";
    private readonly IWebServerManagerService _webServerService;
    private readonly System.Windows.Threading.DispatcherTimer? _statusTimer;

    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _statusMessage = string.Empty;
    private readonly IWhatsAppService _whatsAppService;

    public ConnectionSettingsViewModel ConnectionSettings { get; }

    public GeneralSettingsViewModel(
        ISettingsRepository settingsRepository,
        string currentUser = "admin",
        IWhatsAppService? whatsAppService = null,
        IWhatsAppBillTrackerRepository? trackerRepository = null,
        IWebServerManagerService? webServerService = null,
        DatabaseSettingsStore? databaseSettingsStore = null)
    {
        _settingsRepository = settingsRepository;
        _currentUser = currentUser;
        _whatsAppService = whatsAppService ?? new WhatsAppService(settingsRepository, trackerRepository);
        _webServerService = webServerService ?? new WebServerManagerService();
        ConnectionSettings = new ConnectionSettingsViewModel(databaseSettingsStore ?? new DatabaseSettingsStore(DatabaseSettingsStore.DefaultFilePath));

        AvailableSendMethods = new ObservableCollection<string>
        {
            "API",
            "WhatsApp Web",
            "WhatsApp Desktop",
            "Disabled"
        };

        AvailableWishesModes = new ObservableCollection<string>
        {
            "WhatsappCloud",
            "WhatsAppWeb",
            "WhatsappDesktop"
        };

        AvailableEinvoiceModes = new ObservableCollection<string>
        {
            "DEMO",
            "LIVE"
        };

        AvailableWaLanguages = new ObservableCollection<string>
        {
            "en_US", "en", "en_GB", "hi", "mr", "gu", "ta", "te", "kn"
        };

        AvailableBaudRates = new ObservableCollection<string>
        {
            "1200", "2400", "4800", "9600", "14400", "19200", "38400", "57600", "115200"
        };

        AvailableDataBits = new ObservableCollection<string> { "5", "6", "7", "8" };

        AvailableStopBits = new ObservableCollection<string> { "1", "1.5", "2" };

        AvailableParityOptions = new ObservableCollection<string> { "None", "Even", "Odd", "Mark", "Space" };

        AvailableHandshakeOptions = new ObservableCollection<string> { "None", "RtsCtsCom", "XOnXOff" };

        SaveCommand = new RelayCommand(async () => await SaveSettingsAsync());
        RefreshCommand = new RelayCommand(async () => await LoadSettingsAsync());
        BrowseSavePathCommand = new RelayCommand(BrowseSavePath);
        BrowsePrintFilesCommand = new RelayCommand(BrowsePrintFiles);
        SyncWhatsAppRepliesCommand = new AsyncRelayCommand(SyncWhatsAppRepliesAsync);
        InsertTemplateVariableCommand = new RelayCommand(parameter => InsertTemplateVariable(parameter as string));
        InsertApiParameterVariableCommand = new RelayCommand(parameter => InsertApiParameterVariable(parameter as string));
        InsertBirthdayTemplateVariableCommand = new RelayCommand(parameter => InsertBirthdayTemplateVariable(parameter as string));
        InsertAnniversaryTemplateVariableCommand = new RelayCommand(parameter => InsertAnniversaryTemplateVariable(parameter as string));

        // Web Server Commands
        BrowseWebServerExeCommand = new RelayCommand(BrowseWebServerExe);
        AutoDetectWebServerExeCommand = new RelayCommand(AutoDetectWebServerExe);
        StartWebServerCommand = new RelayCommand(async () => await StartWebServerAsync());
        StopWebServerCommand = new RelayCommand(async () => await StopWebServerAsync());
        InstallAutoStartCommand = new RelayCommand(async () => await InstallAutoStartAsync());
        RemoveAutoStartCommand = new RelayCommand(async () => await RemoveAutoStartAsync());
        OpenPublishFolderCommand = new RelayCommand(OpenPublishFolder);
        OpenWebPortalCommand = new RelayCommand(OpenWebPortal);
        OpenSwaggerDocsCommand = new RelayCommand(OpenSwaggerDocs);
        RefreshWebServerStatusCommand = new RelayCommand(async () => await RefreshWebServerStatusAsync());

        // Setup periodic health status check every 6 seconds
        try
        {
            _statusTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(6)
            };
            _statusTimer.Tick += async (_, _) => await RefreshWebServerStatusAsync();
            _statusTimer.Start();
        }
        catch { }

        _ = LoadSettingsAsync();
    }

    public ICommand SyncWhatsAppRepliesCommand { get; }

    public ObservableCollection<string> AvailableSendMethods { get; }

    public ObservableCollection<string> AvailableWishesModes { get; }

    public ObservableCollection<string> AvailableEinvoiceModes { get; }

    public ObservableCollection<string> AvailableWaLanguages { get; }

    public ObservableCollection<string> AvailableBaudRates { get; }

    public ObservableCollection<string> AvailableDataBits { get; }

    public ObservableCollection<string> AvailableStopBits { get; }

    public ObservableCollection<string> AvailableParityOptions { get; }

    public ObservableCollection<string> AvailableHandshakeOptions { get; }

    public ICommand SaveCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand BrowseSavePathCommand { get; }

    public ICommand BrowsePrintFilesCommand { get; }

    public ICommand InsertTemplateVariableCommand { get; }

    public ICommand InsertApiParameterVariableCommand { get; }

    public ICommand InsertBirthdayTemplateVariableCommand { get; }

    public ICommand InsertAnniversaryTemplateVariableCommand { get; }

    // Web Server Commands
    public ICommand BrowseWebServerExeCommand { get; }
    public ICommand AutoDetectWebServerExeCommand { get; }
    public ICommand StartWebServerCommand { get; }
    public ICommand StopWebServerCommand { get; }
    public ICommand InstallAutoStartCommand { get; }
    public ICommand RemoveAutoStartCommand { get; }
    public ICommand OpenPublishFolderCommand { get; }
    public ICommand OpenWebPortalCommand { get; }
    public ICommand OpenSwaggerDocsCommand { get; }
    public ICommand RefreshWebServerStatusCommand { get; }

    // Web Server Properties
    public string WebServerPort
    {
        get => _webServerPort;
        set => SetProperty(ref _webServerPort, value);
    }

    public string WebServerHost
    {
        get => _webServerHost;
        set => SetProperty(ref _webServerHost, value);
    }

    public string WebServerExePath
    {
        get => _webServerExePath;
        set => SetProperty(ref _webServerExePath, value);
    }

    public bool IsWebServerRunning
    {
        get => _isWebServerRunning;
        set => SetProperty(ref _isWebServerRunning, value);
    }

    public string WebServerStatusText
    {
        get => _webServerStatusText;
        set => SetProperty(ref _webServerStatusText, value);
    }

    public bool IsAutoStartInstalled
    {
        get => _isAutoStartInstalled;
        set => SetProperty(ref _isAutoStartInstalled, value);
    }

    public string AutoStartStatusText
    {
        get => _autoStartStatusText;
        set => SetProperty(ref _autoStartStatusText, value);
    }

    public string SavePath
    {
        get => _savePath;
        set => SetProperty(ref _savePath, value);
    }

    public string PrintFiles
    {
        get => _printFiles;
        set => SetProperty(ref _printFiles, value);
    }

    public string EinvoiceMode
    {
        get => _einvoiceMode;
        set => SetProperty(ref _einvoiceMode, value);
    }

    public string WaSendMethod
    {
        get => _waSendMethod;
        set => SetProperty(ref _waSendMethod, value);
    }

    public string WaAPIKey
    {
        get => _waApiKey;
        set => SetProperty(ref _waApiKey, value);
    }

    public string WaTemplate
    {
        get => _waTemplate;
        set => SetProperty(ref _waTemplate, value);
    }

    public string WaApiTemplateName
    {
        get => _waApiTemplateName;
        set => SetProperty(ref _waApiTemplateName, value);
    }

    public string WaApiLanguage
    {
        get => _waApiLanguage;
        set => SetProperty(ref _waApiLanguage, value);
    }

    public string WaApiParameters
    {
        get => _waApiParameters;
        set => SetProperty(ref _waApiParameters, value);
    }

    public bool WaAttachPdf
    {
        get => _waAttachPdf;
        set => SetProperty(ref _waAttachPdf, value);
    }

    public string WaApiDocumentUrl
    {
        get => _waApiDocumentUrl;
        set => SetProperty(ref _waApiDocumentUrl, value);
    }

    public string WaBusinessNo
    {
        get => _waBusinessNo;
        set => SetProperty(ref _waBusinessNo, value);
    }

    public bool WaAutoSend
    {
        get => _waAutoSend;
        set => SetProperty(ref _waAutoSend, value);
    }

    public string AllowNtWtModUpto
    {
        get => _allowNtWtModUpto;
        set => SetProperty(ref _allowNtWtModUpto, value);
    }

    public string AnniversaryMessage
    {
        get => _anniversaryMessage;
        set => SetProperty(ref _anniversaryMessage, value);
    }

    public string BirthdayMessage
    {
        get => _birthdayMessage;
        set => SetProperty(ref _birthdayMessage, value);
    }

    public string WishesMode
    {
        get => _wishesMode;
        set => SetProperty(ref _wishesMode, value);
    }

    // ── Weight Machine Properties ────────────────────────────────────
    public bool WeightEnabled
    {
        get => _weightEnabled;
        set => SetProperty(ref _weightEnabled, value);
    }

    public string WeightCommPort
    {
        get => _weightCommPort;
        set => SetProperty(ref _weightCommPort, value);
    }

    public string WeightBaudRate
    {
        get => _weightBaudRate;
        set => SetProperty(ref _weightBaudRate, value);
    }

    public string WeightDataBits
    {
        get => _weightDataBits;
        set => SetProperty(ref _weightDataBits, value);
    }

    public string WeightStopBits
    {
        get => _weightStopBits;
        set => SetProperty(ref _weightStopBits, value);
    }

    public string WeightParity
    {
        get => _weightParity;
        set => SetProperty(ref _weightParity, value);
    }

    public string WeightHandshake
    {
        get => _weightHandshake;
        set => SetProperty(ref _weightHandshake, value);
    }

    public string WeightStablePrefix
    {
        get => _weightStablePrefix;
        set => SetProperty(ref _weightStablePrefix, value);
    }

    public string WeightTimeoutMs
    {
        get => _weightTimeoutMs;
        set => SetProperty(ref _weightTimeoutMs, value);
    }
    // ────────────────────────────────────────────────────────────────

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
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

    public async Task LoadSettingsAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading settings...";
        try
        {
            var settings = await _settingsRepository.GetAllAsync();

            if (settings.TryGetValue(AppSetting.SavePathKey, out var savePath) && !string.IsNullOrWhiteSpace(savePath))
            {
                SavePath = savePath;
            }
            else
            {
                SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ConceptJewellery", "Exports");
            }

            if (settings.TryGetValue(AppSetting.PrintFilesKey, out var printFiles) && !string.IsNullOrWhiteSpace(printFiles))
            {
                PrintFiles = printFiles;
            }
            else
            {
                PrintFiles = Infrastructure.Printing.FastReportPrintService.DefaultPrintFilesPath;
            }

            Infrastructure.Printing.FastReportPrintService.ConfigurePaths(PrintFiles, SavePath);

            if (settings.TryGetValue(AppSetting.WaSendMethodKey, out var sendMethod) && !string.IsNullOrWhiteSpace(sendMethod))
            {
                WaSendMethod = sendMethod;
            }
            else
            {
                WaSendMethod = "API";
            }

            if (settings.TryGetValue(AppSetting.WaAPIKeyKey, out var apiKey))
            {
                WaAPIKey = apiKey;
            }

            if (settings.TryGetValue(AppSetting.WaTemplateKey, out var template) && !string.IsNullOrWhiteSpace(template))
            {
                WaTemplate = template;
            }
            else
            {
                WaTemplate = "Dear {CustomerName},\nThank you for choosing us! Your invoice #{InvoiceNo} of Rs. {NetAmount} dated {BillDate} has been generated successfully.\nWe appreciate your business!";
            }

            if (settings.TryGetValue(AppSetting.WaApiTemplateNameKey, out var apiTemplateName))
            {
                WaApiTemplateName = apiTemplateName;
            }
            else
            {
                WaApiTemplateName = string.Empty;
            }

            if (settings.TryGetValue(AppSetting.WaApiLanguageKey, out var apiLanguage) && !string.IsNullOrWhiteSpace(apiLanguage))
            {
                WaApiLanguage = apiLanguage;
            }
            else
            {
                WaApiLanguage = "en_US";
            }

            if (settings.TryGetValue(AppSetting.WaApiParametersKey, out var apiParameters) && !string.IsNullOrWhiteSpace(apiParameters))
            {
                WaApiParameters = apiParameters;
            }
            else
            {
                WaApiParameters = "{CustomerName}, {InvoiceNo}, {BillDate}, {NetAmount}, {Balance}, {StoreName}";
            }

            if (settings.TryGetValue(AppSetting.WaAttachPdfKey, out var attachPdfStr))
            {
                WaAttachPdf = bool.TryParse(attachPdfStr, out var ap) && ap;
            }
            else
            {
                WaAttachPdf = false;
            }

            if (settings.TryGetValue(AppSetting.WaApiDocumentUrlKey, out var docUrl))
            {
                WaApiDocumentUrl = docUrl;
            }
            else
            {
                WaApiDocumentUrl = string.Empty;
            }

            if (settings.TryGetValue(AppSetting.WaBusinessNoKey, out var businessNo))
            {
                WaBusinessNo = businessNo;
            }

            if (settings.TryGetValue(AppSetting.WaAutoSendKey, out var autoSendStr))
            {
                WaAutoSend = bool.TryParse(autoSendStr, out var autoSend) && autoSend;
            }
            else
            {
                WaAutoSend = false;
            }

            if (settings.TryGetValue(AppSetting.EinvoiceModeKey, out var einvoiceMode) && !string.IsNullOrWhiteSpace(einvoiceMode))
            {
                EinvoiceMode = einvoiceMode;
            }
            else
            {
                EinvoiceMode = "DEMO";
            }

            if (settings.TryGetValue(AppSetting.AllowNtWtModUptoKey, out var allowNtWt) && !string.IsNullOrWhiteSpace(allowNtWt))
            {
                AllowNtWtModUpto = allowNtWt;
            }
            else
            {
                AllowNtWtModUpto = "0.000";
            }

            if (settings.TryGetValue(AppSetting.AnniversaryMessageKey, out var annivMsg) && !string.IsNullOrWhiteSpace(annivMsg))
            {
                AnniversaryMessage = annivMsg;
            }
            else if (settings.TryGetValue("AnniversarryMessage", out var annivMsgAlt) && !string.IsNullOrWhiteSpace(annivMsgAlt))
            {
                AnniversaryMessage = annivMsgAlt;
            }
            else
            {
                AnniversaryMessage = "Dear {CustomerName}, happy Wedding Anniversary from all of us at Concept Jewellery! Wishing you and your spouse a lifetime of love, happiness, and togetherness!";
            }

            if (settings.TryGetValue(AppSetting.BirthdayMessageKey, out var bdayMsg) && !string.IsNullOrWhiteSpace(bdayMsg))
            {
                BirthdayMessage = bdayMsg;
            }
            else
            {
                BirthdayMessage = "Dear {CustomerName}, warm wishes on your Birthday from the entire team at Concept Jewellery! May your day be as sparkling and precious as gold. Wishing you joy, health, and prosperity!";
            }

            if (settings.TryGetValue(AppSetting.WishesModeKey, out var wishesMode) && !string.IsNullOrWhiteSpace(wishesMode))
            {
                WishesMode = wishesMode;
            }
            else
            {
                WishesMode = "WhatsappCloud";
            }

            // ── Weight Machine ───────────────────────────────────────────────
            WeightEnabled = settings.TryGetValue(AppSetting.WeightEnabledKey, out var wEnabled)
                && bool.TryParse(wEnabled, out var wEnabledBool) && wEnabledBool;

            WeightCommPort = settings.TryGetValue(AppSetting.WeightCommPortKey, out var wPort) && !string.IsNullOrWhiteSpace(wPort)
                ? wPort : "COM1";

            WeightBaudRate = settings.TryGetValue(AppSetting.WeightBaudRateKey, out var wBaud) && !string.IsNullOrWhiteSpace(wBaud)
                ? wBaud : "9600";

            WeightDataBits = settings.TryGetValue(AppSetting.WeightDataBitsKey, out var wData) && !string.IsNullOrWhiteSpace(wData)
                ? wData : "8";

            WeightStopBits = settings.TryGetValue(AppSetting.WeightStopBitsKey, out var wStop) && !string.IsNullOrWhiteSpace(wStop)
                ? wStop : "1";

            WeightParity = settings.TryGetValue(AppSetting.WeightParityKey, out var wParity) && !string.IsNullOrWhiteSpace(wParity)
                ? wParity : "None";

            WeightHandshake = settings.TryGetValue(AppSetting.WeightHandshakeKey, out var wHandshake) && !string.IsNullOrWhiteSpace(wHandshake)
                ? wHandshake : "None";

            WeightStablePrefix = settings.TryGetValue(AppSetting.WeightStablePrefixKey, out var wPrefix) && !string.IsNullOrWhiteSpace(wPrefix)
                ? wPrefix : "ST,GS";

            WeightTimeoutMs = settings.TryGetValue(AppSetting.WeightTimeoutMsKey, out var wTimeout) && !string.IsNullOrWhiteSpace(wTimeout)
                ? wTimeout : "500";
            // ────────────────────────────────────────────────────────────────

            // ── Web Server ──────────────────────────────────────────────────
            WebServerPort = settings.TryGetValue(AppSetting.WebServerPortKey, out var wsPort) && !string.IsNullOrWhiteSpace(wsPort)
                ? wsPort : "5080";

            WebServerHost = settings.TryGetValue(AppSetting.WebServerHostKey, out var wsHost) && !string.IsNullOrWhiteSpace(wsHost)
                ? wsHost : "0.0.0.0";

            if (settings.TryGetValue(AppSetting.WebServerExePathKey, out var wsExe) && !string.IsNullOrWhiteSpace(wsExe) && File.Exists(wsExe))
            {
                WebServerExePath = wsExe;
            }
            else
            {
                WebServerExePath = _webServerService.AutoDetectExePath();
            }

            _ = RefreshWebServerStatusAsync();
            // ────────────────────────────────────────────────────────────────

            StatusMessage = "Settings loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load settings.";
            ShowToast($"Failed to load settings: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveSettingsAsync()
    {
        IsBusy = true;
        StatusMessage = "Saving settings...";
        try
        {
            var dict = new Dictionary<string, string?>
            {
                [AppSetting.SavePathKey] = SavePath?.Trim() ?? string.Empty,
                [AppSetting.PrintFilesKey] = string.IsNullOrWhiteSpace(PrintFiles)
                    ? Infrastructure.Printing.FastReportPrintService.DefaultPrintFilesPath
                    : PrintFiles.Trim(),
                [AppSetting.EinvoiceModeKey] = EinvoiceMode?.Trim() ?? "DEMO",
                [AppSetting.AllowNtWtModUptoKey] = AllowNtWtModUpto?.Trim() ?? "0.000",
                [AppSetting.WaSendMethodKey] = WaSendMethod?.Trim() ?? "API",
                [AppSetting.WaAPIKeyKey] = WaAPIKey?.Trim() ?? string.Empty,
                [AppSetting.WaTemplateKey] = WaTemplate?.Trim() ?? string.Empty,
                [AppSetting.WaApiTemplateNameKey] = WaApiTemplateName?.Trim() ?? string.Empty,
                [AppSetting.WaApiLanguageKey] = WaApiLanguage?.Trim() ?? "en_US",
                [AppSetting.WaApiParametersKey] = WaApiParameters?.Trim() ?? string.Empty,
                [AppSetting.WaAttachPdfKey] = WaAttachPdf.ToString(),
                [AppSetting.WaApiDocumentUrlKey] = WaApiDocumentUrl?.Trim() ?? string.Empty,
                [AppSetting.WaBusinessNoKey] = WaBusinessNo?.Trim() ?? string.Empty,
                [AppSetting.WaAutoSendKey] = WaAutoSend.ToString(),
                [AppSetting.AnniversaryMessageKey] = AnniversaryMessage?.Trim() ?? string.Empty,
                [AppSetting.BirthdayMessageKey] = BirthdayMessage?.Trim() ?? string.Empty,
                [AppSetting.WishesModeKey] = WishesMode?.Trim() ?? "WhatsappCloud",
                // Web Server
                [AppSetting.WebServerPortKey] = WebServerPort?.Trim() ?? "5080",
                [AppSetting.WebServerHostKey] = WebServerHost?.Trim() ?? "0.0.0.0",
                [AppSetting.WebServerExePathKey] = WebServerExePath?.Trim() ?? string.Empty,
                // Weight Machine
                [AppSetting.WeightEnabledKey] = WeightEnabled.ToString(),
                [AppSetting.WeightCommPortKey] = WeightCommPort?.Trim() ?? "COM1",
                [AppSetting.WeightBaudRateKey] = WeightBaudRate?.Trim() ?? "9600",
                [AppSetting.WeightDataBitsKey] = WeightDataBits?.Trim() ?? "8",
                [AppSetting.WeightStopBitsKey] = WeightStopBits?.Trim() ?? "1",
                [AppSetting.WeightParityKey] = WeightParity?.Trim() ?? "None",
                [AppSetting.WeightHandshakeKey] = WeightHandshake?.Trim() ?? "None",
                [AppSetting.WeightStablePrefixKey] = WeightStablePrefix?.Trim() ?? "ST,GS",
                [AppSetting.WeightTimeoutMsKey] = WeightTimeoutMs?.Trim() ?? "500"
            };

            await _settingsRepository.SaveManyAsync(dict);
            ConnectionSettings.SaveCommand.Execute(null);

            var resolvedPrintFiles = string.IsNullOrWhiteSpace(PrintFiles)
                ? Infrastructure.Printing.FastReportPrintService.DefaultPrintFilesPath
                : PrintFiles.Trim();
            var resolvedSavePath = SavePath?.Trim() ?? string.Empty;
            Infrastructure.Printing.FastReportPrintService.ConfigurePaths(resolvedPrintFiles, resolvedSavePath);
            try
            {
                var localStore = new LocalPrinterSettingsStore();
                var localSettings = localStore.Load();
                localSettings.PrintFiles = resolvedPrintFiles;
                localSettings.SavePath = resolvedSavePath;
                localStore.Save(localSettings);
            }
            catch { }

            StatusMessage = "Settings saved successfully.";
            ShowToast("General settings saved successfully!", ToastType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to save settings.";
            ShowToast($"Error saving settings: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BrowseSavePath()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Export & Save Directory",
                InitialDirectory = !string.IsNullOrWhiteSpace(SavePath) && Directory.Exists(SavePath)
                    ? SavePath
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                SavePath = dialog.FolderName;
            }
        }
        catch
        {
            // Fallback if dialog fails
        }
    }

    private void BrowsePrintFiles()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select FastReport (.frx) PrintFiles Directory",
                InitialDirectory = !string.IsNullOrWhiteSpace(PrintFiles) && Directory.Exists(PrintFiles)
                    ? PrintFiles
                    : !string.IsNullOrWhiteSpace(SavePath) && Directory.Exists(SavePath)
                        ? SavePath
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                PrintFiles = dialog.FolderName;
            }
        }
        catch
        {
            // Fallback if dialog fails
        }
    }

    private void InsertTemplateVariable(string? variable)
    {
        if (string.IsNullOrWhiteSpace(variable))
            return;

        if (string.IsNullOrEmpty(WaTemplate))
        {
            WaTemplate = variable;
        }
        else
        {
            WaTemplate += " " + variable;
        }
    }

    private void InsertApiParameterVariable(string? variable)
    {
        if (string.IsNullOrWhiteSpace(variable))
            return;

        if (string.IsNullOrWhiteSpace(WaApiParameters))
        {
            WaApiParameters = variable;
        }
        else
        {
            WaApiParameters += ", " + variable;
        }
    }

    private void InsertBirthdayTemplateVariable(string? variable)
    {
        if (string.IsNullOrWhiteSpace(variable))
            return;

        if (string.IsNullOrEmpty(BirthdayMessage))
        {
            BirthdayMessage = variable;
        }
        else
        {
            BirthdayMessage += " " + variable;
        }
    }

    private void InsertAnniversaryTemplateVariable(string? variable)
    {
        if (string.IsNullOrWhiteSpace(variable))
            return;

        if (string.IsNullOrEmpty(AnniversaryMessage))
        {
            AnniversaryMessage = variable;
        }
        else
        {
            AnniversaryMessage += " " + variable;
        }
    }

    public async Task SyncWhatsAppRepliesAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = "Syncing WhatsApp delivery statuses and replies...";

        try
        {
            var result = await _whatsAppService.SyncRepliesAndStatusesAsync();
            if (result.TotalCount > 0)
            {
                var parts = new List<string>();
                if (result.SyncedStatusesCount > 0) parts.Add($"{result.SyncedStatusesCount} status(es)");
                if (result.SyncedRepliesCount > 0) parts.Add($"{result.SyncedRepliesCount} reply(s)");
                var summary = string.Join(" and ", parts);

                ShowToast($"Successfully synced {summary}.", ToastType.Success);
                StatusMessage = $"Synced {summary} at {DateTime.Now:HH:mm:ss}";
            }
            else
            {
                ShowToast("No new WhatsApp status updates or replies found on the server.", ToastType.Info);
                StatusMessage = $"No new updates found at {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to sync: {ex.Message}", ToastType.Error);
            StatusMessage = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BrowseWebServerExe()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select ConceptJewelleryPro.API.exe",
            Filter = "API Server (ConceptJewelleryPro.API.exe)|ConceptJewelleryPro.API.exe|All Executables (*.exe)|*.exe"
        };
        if (!string.IsNullOrWhiteSpace(WebServerExePath) && File.Exists(WebServerExePath))
        {
            try { dlg.InitialDirectory = Path.GetDirectoryName(WebServerExePath); } catch { }
        }
        if (dlg.ShowDialog() == true)
        {
            WebServerExePath = dlg.FileName;
        }
    }

    private void AutoDetectWebServerExe()
    {
        var detected = _webServerService.AutoDetectExePath();
        if (!string.IsNullOrWhiteSpace(detected))
        {
            WebServerExePath = detected;
            ShowToast($"Detected server executable: {detected}", ToastType.Success);
        }
        else
        {
            ShowToast("ConceptJewelleryPro.API.exe was not found automatically. Please browse manually.", ToastType.Warning);
        }
    }

    private async Task StartWebServerAsync()
    {
        IsBusy = true;
        StatusMessage = "Starting Web Server...";
        try
        {
            var res = await _webServerService.StartServerAsync(WebServerExePath, WebServerHost, WebServerPort);
            ShowToast(res.Message, res.Success ? ToastType.Success : ToastType.Error);
            await RefreshWebServerStatusAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to start server: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StopWebServerAsync()
    {
        IsBusy = true;
        StatusMessage = "Stopping Web Server...";
        try
        {
            var res = await _webServerService.StopServerAsync(WebServerExePath, WebServerPort);
            ShowToast(res.Message, res.Success ? ToastType.Success : ToastType.Error);
            await RefreshWebServerStatusAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to stop server: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InstallAutoStartAsync()
    {
        IsBusy = true;
        StatusMessage = "Configuring always-on background service...";
        try
        {
            var res = await _webServerService.InstallAutoStartServiceAsync(WebServerExePath, WebServerHost, WebServerPort);
            ShowToast(res.Message, res.Success ? ToastType.Success : ToastType.Error);
            await RefreshWebServerStatusAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Installation error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RemoveAutoStartAsync()
    {
        IsBusy = true;
        StatusMessage = "Stopping and completely removing background service...";
        try
        {
            var res = await _webServerService.RemoveAutoStartServiceAsync(WebServerExePath, WebServerPort);
            ShowToast(res.Message, res.Success ? ToastType.Success : ToastType.Error);
            await RefreshWebServerStatusAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Removal error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenPublishFolder()
    {
        var path = WebServerExePath;
        var dir = !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? Path.GetDirectoryName(path)
            : (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? path : null);

        if (string.IsNullOrWhiteSpace(dir))
        {
            var detected = _webServerService.AutoDetectExePath();
            if (!string.IsNullOrWhiteSpace(detected) && File.Exists(detected))
            {
                dir = Path.GetDirectoryName(detected);
            }
        }

        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
        {
            try
            {
                Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowToast($"Could not open directory: {ex.Message}", ToastType.Error);
            }
        }
        else
        {
            ShowToast("Publish directory not found. Please browse to or auto-detect the executable first.", ToastType.Warning);
        }
    }

    private void OpenWebPortal()
    {
        var port = string.IsNullOrWhiteSpace(WebServerPort) ? "5080" : WebServerPort.Trim();
        var url = $"http://localhost:{port}/";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowToast($"Could not launch browser: {ex.Message}", ToastType.Error);
        }
    }

    private void OpenSwaggerDocs()
    {
        var port = string.IsNullOrWhiteSpace(WebServerPort) ? "5080" : WebServerPort.Trim();
        var url = $"http://localhost:{port}/swagger";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowToast($"Could not launch browser: {ex.Message}", ToastType.Error);
        }
    }

    private async Task RefreshWebServerStatusAsync()
    {
        try
        {
            var status = await _webServerService.GetStatusAsync(WebServerHost, WebServerPort);
            IsWebServerRunning = status.IsRunning;
            WebServerStatusText = status.StatusText;
            IsAutoStartInstalled = status.IsAutoStartInstalled;
            AutoStartStatusText = status.AutoStartStatusText;
        }
        catch { }
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastType = type;
        ToastMessage = message;
    }
}

