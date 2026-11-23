using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.Application.Services;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Printing;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Views;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum BarcodeFormMode
{
    View,
    Add,
    Modify
}

public sealed class BarcodeCreationViewModel : ViewModelBase, IConfirmCloseDocument, IDisposable, IRefreshable
{
    private readonly IBarcodeCreationRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IPurityRepository _purityRepository;
    private readonly IBarcodePrintService _barcodePrintService;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly DispatcherTimer _clockTimer;

    private BarcodeFormMode _formMode = BarcodeFormMode.View;
    private BarcodeSubformItem? _selectedRecentBarcode;
    private BarcodeSubformItem? _originalBarcodeSnapshot;
    private bool _isBusy;
    private bool _suppressSync;

    // Fields matching the user screenshot:
    // Left Box
    private string _typePcWt = "WT";
    private string _itemName = string.Empty;
    private string _purity = string.Empty;
    private string _description = string.Empty;
    private string _huid = string.Empty;
    private string _size = "-";
    private decimal _qty = 1m;
    private decimal _grossWeight;
    private decimal _lessWeight;
    private decimal _netWeight;

    // Middle Box
    private decimal _making;
    private string _makingType = "GRAM";
    private decimal _stoneValue;
    private decimal _costTounch = 100m;
    private decimal _costPerGram;
    private decimal _costMC;
    private decimal _costPrice;
    private decimal _costRate;
    private decimal _minRate;
    private decimal _profitMultiple = 1.65m;
    private string _costMultiplier = "1.65";
    private decimal _mrpPrice;
    private string _supplier = "OPN";
    private DateTime? _invoiceDate = DateTime.Today;
    private string _invoiceNo = string.Empty;
    private bool _isCalculating;

    // Right Box
    private string _stickerFormat = "StickerWt";
    private string _barcodePrintEngine = "PRN";
    private string _photoPath = string.Empty;
    private string _boxNo = string.Empty;
    private string _godown = string.Empty;
    private string _currentClockText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

    // Barcode Identification
    private string _barcode = string.Empty;
    private int _nos;
    private int _barcodeAutoNo;
    private string _filterText = string.Empty;

    private readonly WeightMachineService _weightService = new();

    // Weight machine live reading
    private decimal _liveWeight;
    private bool    _weightIsStable;
    private bool    _weightIsConnected;
    private bool    _weightEnabled;
    private string  _weightStatusText = "Disabled";

    private readonly IAiImageEnhancementService _aiEnhancementService;
    private bool _isEnhancingPhoto;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    private readonly IBoxMasterRepository? _boxMasterRepository;

    public BarcodeCreationViewModel(
        IBarcodeCreationRepository repository,
        IAccountRepository accountRepository,
        IItemRepository itemRepository,
        IPurityRepository purityRepository,
        IBarcodePrintService barcodePrintService,
        string currentUser,
        IAuditRepository? auditRepository = null,
        IAiImageEnhancementService? aiImageEnhancementService = null,
        IBoxMasterRepository? boxMasterRepository = null)
        : this(repository, accountRepository, itemRepository, purityRepository, barcodePrintService, null, currentUser, auditRepository, aiImageEnhancementService, boxMasterRepository)
    {
    }

    public BarcodeCreationViewModel(
        IBarcodeCreationRepository repository,
        IAccountRepository accountRepository,
        IItemRepository itemRepository,
        IPurityRepository purityRepository,
        IBarcodePrintService barcodePrintService,
        ISettingsRepository? settingsRepository,
        string currentUser,
        IAuditRepository? auditRepository = null,
        IAiImageEnhancementService? aiImageEnhancementService = null,
        IBoxMasterRepository? boxMasterRepository = null)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _barcodePrintService = barcodePrintService;
        _settingsRepository = settingsRepository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;
        _aiEnhancementService = aiImageEnhancementService ?? new ConceptJewelleryPro.Infrastructure.Services.AiImageEnhancementService();
        _boxMasterRepository = boxMasterRepository;

        NewCommand = new AsyncRelayCommand(NewBarcodeAsync, () => CanNew);
        SaveCommand = new AsyncRelayCommand(SaveBarcodeAsync, () => CanSave);
        ModifyCommand = new RelayCommand(ModifyBarcode, () => CanModify);
        DeleteCommand = new AsyncRelayCommand(DeleteBarcodeAsync, () => CanDelete);
        CancelCommand = new RelayCommand(CancelBarcode, () => CanCancel);
        PrintStickerCommand = new AsyncRelayCommand(() => PrintStickerAsync(), () => HasActiveBarcode);
        SnapPhotoCommand = new AsyncRelayCommand(SnapOrBrowsePhotoAsync, () => IsFormEnabled);
        ClearPhotoCommand = new RelayCommand(() => PhotoPath = string.Empty, () => IsFormEnabled && !string.IsNullOrEmpty(PhotoPath));
        AiEnhancePhotoCommand = new AsyncRelayCommand(AiEnhancePhotoAsync, () => IsFormEnabled && HasPhoto && !IsEnhancingPhoto);
        OpenAiEnhanceSettingsCommand = new RelayCommand(OpenAiEnhanceSettings, () => IsFormEnabled && HasPhoto && !IsEnhancingPhoto);
        OpenBoxMasterCommand = new RelayCommand(OpenBoxMaster);
        RefreshCommand = new AsyncRelayCommand(LoadRecentBarcodesAsync);
        RefreshLookupsCommand = new AsyncRelayCommand(RefreshLookupsAsync);
        SearchAndLoadBarcodeCommand = new RelayCommand(param => _ = SearchAndLoadBarcodeAsync(param?.ToString()));
        PullWeightCommand = new RelayCommand(PullWeight, CanPullWeight);

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => CurrentClockText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        _clockTimer.Start();

        _ = InitializeAsync();
    }

    public string Title => "Barcode Creation";
    public string VoucherNumberDisplay => "Voucher: 9 (Opening Stock)";

    public event EventHandler? RequestFocusType;

    public ObservableCollection<BarcodeSubformItem> RecentBarcodes { get; } = [];
    public ObservableCollection<Account> Suppliers { get; } = [];
    public ObservableCollection<Item> AvailableItems { get; } = [];
    public ObservableCollection<PurityMaster> AvailablePurities { get; } = [];
    public ObservableCollection<string> FilteredPurities { get; } = [];
    public ObservableCollection<string> StickerFormats { get; } = [];
    public ObservableCollection<BoxMaster> AvailableBoxes { get; } = [];
    public IReadOnlyList<string> TypeOptions { get; } = ["-", "WT", "PC"];
    public IReadOnlyList<string> MakingTypeOptions { get; } = ["GRAM", "PC", "%"];
    public IReadOnlyList<string> BarcodePrintEngineOptions { get; } = ["PRN", "FRX"];
    public IReadOnlyList<string> SizeOptions { get; } = ["-", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "22", "24", "26", "28", "30", "14\"", "16\"", "18\"", "20\"", "22\"", "24\""];

    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand ModifyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand PrintStickerCommand { get; }
    public AsyncRelayCommand SnapPhotoCommand { get; }
    public RelayCommand ClearPhotoCommand { get; }
    public AsyncRelayCommand AiEnhancePhotoCommand { get; }
    public RelayCommand OpenAiEnhanceSettingsCommand { get; }
    public RelayCommand OpenBoxMasterCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand RefreshLookupsCommand { get; }
    public RelayCommand SearchAndLoadBarcodeCommand { get; }
    public RelayCommand PullWeightCommand { get; }

    public bool IsEnhancingPhoto
    {
        get => _isEnhancingPhoto;
        private set
        {
            if (SetProperty(ref _isEnhancingPhoto, value))
            {
                OnPropertyChanged(nameof(CanAiEnhance));
                RefreshCommands();
            }
        }
    }

    public bool CanAiEnhance => IsFormEnabled && HasPhoto && !IsEnhancingPhoto;

    // ── Weight machine live state ────────────────────────────────────
    public bool WeightEnabled
    {
        get => _weightEnabled;
        private set => SetProperty(ref _weightEnabled, value);
    }

    public bool WeightIsConnected
    {
        get => _weightIsConnected;
        private set => SetProperty(ref _weightIsConnected, value);
    }

    public bool WeightIsStable
    {
        get => _weightIsStable;
        private set => SetProperty(ref _weightIsStable, value);
    }

    public decimal LiveWeight
    {
        get => _liveWeight;
        private set => SetProperty(ref _liveWeight, value);
    }

    public string WeightStatusText
    {
        get => _weightStatusText;
        private set => SetProperty(ref _weightStatusText, value);
    }
    // ────────────────────────────────────────────────────────────────

    public BarcodeFormMode FormMode
    {
        get => _formMode;
        private set
        {
            if (SetProperty(ref _formMode, value))
            {
                OnPropertyChanged(nameof(IsFormEnabled));
                OnPropertyChanged(nameof(CanNew));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(IsCreating));
                OnPropertyChanged(nameof(IsModifying));
                OnPropertyChanged(nameof(IsViewingActiveBarcode));
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(BarcodeStatusModeText));
                OnPropertyChanged(nameof(BarcodeStatusBadgeText));
                OnPropertyChanged(nameof(BarcodeStatusSubtitle));
                RefreshCommands();
            }
        }
    }

    public bool IsFormEnabled => FormMode is BarcodeFormMode.Add or BarcodeFormMode.Modify;
    public bool CanNew => FormMode == BarcodeFormMode.View && !IsBusy;
    public bool CanSave => IsFormEnabled && !IsBusy;
    public bool CanCancel => FormMode != BarcodeFormMode.View && !IsBusy;
    public bool CanModify => FormMode == BarcodeFormMode.View && SelectedRecentBarcode is not null && !IsBusy;
    public bool CanDelete => FormMode == BarcodeFormMode.View && SelectedRecentBarcode is not null && !IsBusy;
    public bool HasActiveBarcode => !string.IsNullOrWhiteSpace(Barcode);
    public bool IsCreating => FormMode == BarcodeFormMode.Add;
    public bool IsModifying => FormMode == BarcodeFormMode.Modify;
    public bool IsViewingActiveBarcode => FormMode == BarcodeFormMode.View && HasActiveBarcode;
    public bool IsIdle => FormMode == BarcodeFormMode.View && !HasActiveBarcode;

    public string CurrentBarcodeDisplay =>
        string.IsNullOrWhiteSpace(Barcode) ? "------" : Barcode.Trim();

    public string BarcodeStatusModeText => FormMode switch
    {
        BarcodeFormMode.Add => "CREATING NEW BARCODE",
        BarcodeFormMode.Modify => "MODIFYING BARCODE",
        _ => HasActiveBarcode ? "VIEWING BARCODE" : "READY"
    };

    public string BarcodeStatusBadgeText => FormMode switch
    {
        BarcodeFormMode.Add => "NEW",
        BarcodeFormMode.Modify => "MODIFYING",
        _ => HasActiveBarcode ? "VIEW" : "IDLE"
    };

    public string BarcodeStatusSubtitle => FormMode switch
    {
        BarcodeFormMode.Add => "Auto-assigned next barcode number • Enter details and press Save (Alt+S)",
        BarcodeFormMode.Modify => "Editing existing barcode record • Update fields and press Save (Alt+S) or Cancel (Alt+C)",
        _ => HasActiveBarcode
            ? "Viewing saved barcode record (Read-only) • Click 'Modify (Alt+M)' to edit"
            : "Click '+ Add New (Alt+A)' to generate next barcode or select an item from the table below"
    };

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
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

    public string Barcode
    {
        get => _barcode;
        set
        {
            if (SetProperty(ref _barcode, value))
            {
                OnPropertyChanged(nameof(HasActiveBarcode));
                OnPropertyChanged(nameof(CurrentBarcodeDisplay));
                OnPropertyChanged(nameof(IsViewingActiveBarcode));
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(BarcodeStatusModeText));
                OnPropertyChanged(nameof(BarcodeStatusBadgeText));
                OnPropertyChanged(nameof(BarcodeStatusSubtitle));
                RefreshCommands();
            }
        }
    }

    public int Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public int BarcodeAutoNo
    {
        get => _barcodeAutoNo;
        set => SetProperty(ref _barcodeAutoNo, value);
    }

    public string TypePcWt
    {
        get => _typePcWt;
        set
        {
            var raw = value?.Trim() ?? string.Empty;
            var cleanVal = raw.ToUpperInvariant();
            if (cleanVal.Contains("PC")) cleanVal = "PC";
            else if (cleanVal.Contains("WT")) cleanVal = "WT";
            else if (cleanVal == "-") cleanVal = "-";
            else if (!string.IsNullOrEmpty(cleanVal)) cleanVal = "WT";

            if (SetProperty(ref _typePcWt, cleanVal))
            {
                RecalculateFromNetWeight();
                ApplyDefaultStickerFormat();
            }
            else
            {
                ApplyDefaultStickerFormat();
            }
        }
    }

    public string ItemName
    {
        get => _itemName;
        set
        {
            if (value is null) return;
            if (SetProperty(ref _itemName, value))
            {
                AutoFillItemDefaults(value);
            }
        }
    }

    public string Purity
    {
        get => _purity;
        set
        {
            if (value is null) return;
            if (SetProperty(ref _purity, value))
            {
                OnPurityChanged(value);
            }
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string HUID
    {
        get => _huid;
        set => SetProperty(ref _huid, value);
    }

    public string Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    public decimal Qty
    {
        get => _qty;
        set
        {
            if (SetProperty(ref _qty, value))
            {
                if (string.Equals(TypePcWt, "PC", StringComparison.OrdinalIgnoreCase))
                {
                    RecalculateFromNetWeight();
                }
            }
        }
    }

    public decimal GrossWeight
    {
        get => _grossWeight;
        set
        {
            if (SetProperty(ref _grossWeight, value))
            {
                CalculateNetWeight();
            }
        }
    }

    public decimal LessWeight
    {
        get => _lessWeight;
        set
        {
            if (SetProperty(ref _lessWeight, value))
            {
                CalculateNetWeight();
            }
        }
    }

    public decimal NetWeight
    {
        get => _netWeight;
        set => SetProperty(ref _netWeight, value);
    }

    public decimal Making
    {
        get => _making;
        set => SetProperty(ref _making, value);
    }

    public string MakingType
    {
        get => _makingType;
        set => SetProperty(ref _makingType, value);
    }

    public decimal StoneValue
    {
        get => _stoneValue;
        set => SetProperty(ref _stoneValue, value);
    }

    public decimal CostTounch
    {
        get => _costTounch;
        set
        {
            if (SetProperty(ref _costTounch, value))
            {
                RecalculateFromCostTounch();
            }
        }
    }

    public decimal CostPerGram
    {
        get => _costPerGram;
        set
        {
            if (SetProperty(ref _costPerGram, value))
            {
                RecalculateFromCostPerGram();
            }
        }
    }

    public decimal CostMC
    {
        get => _costMC;
        set
        {
            if (SetProperty(ref _costMC, value))
            {
                RecalculateFromCostMC();
            }
        }
    }

    public decimal CostPrice
    {
        get => _costPrice;
        set
        {
            if (SetProperty(ref _costPrice, value))
            {
                RecalculateFromCostPrice();
            }
        }
    }

    public decimal CostRate
    {
        get => _costRate;
        set
        {
            if (SetProperty(ref _costRate, value))
            {
                RecalculateFromCostRate();
            }
        }
    }

    public decimal MinRate
    {
        get => _minRate;
        set
        {
            if (SetProperty(ref _minRate, value))
            {
                RecalculateFromMinRate();
            }
        }
    }

    public decimal ProfitMultiple
    {
        get => _profitMultiple;
        set
        {
            if (SetProperty(ref _profitMultiple, value))
            {
                _costMultiplier = value.ToString("0.00");
                OnPropertyChanged(nameof(CostMultiplier));
                RecalculateFromProfitMultiple();
            }
        }
    }

    public string CostMultiplier
    {
        get => _costMultiplier;
        set
        {
            if (SetProperty(ref _costMultiplier, value))
            {
                if (decimal.TryParse(value, out var parsed))
                {
                    _profitMultiple = parsed;
                    OnPropertyChanged(nameof(ProfitMultiple));
                    RecalculateFromProfitMultiple();
                }
            }
        }
    }

    public decimal MrpPrice
    {
        get => _mrpPrice;
        set
        {
            if (SetProperty(ref _mrpPrice, value))
            {
                RecalculateFromMrpPrice();
            }
        }
    }

    public string Supplier
    {
        get => _supplier;
        set
        {
            if (value is null) return;
            SetProperty(ref _supplier, value);
        }
    }

    public DateTime? InvoiceDate
    {
        get => _invoiceDate;
        set => SetProperty(ref _invoiceDate, value);
    }

    public string InvoiceNo
    {
        get => _invoiceNo;
        set => SetProperty(ref _invoiceNo, value);
    }

    public string StickerFormat
    {
        get => _stickerFormat;
        set => SetProperty(ref _stickerFormat, value);
    }

    public string BarcodePrintEngine
    {
        get => _barcodePrintEngine;
        set
        {
            var normalized = BarcodePrintService.NormalizeEngine(value);
            if (SetProperty(ref _barcodePrintEngine, normalized))
            {
                try
                {
                    var store = new LocalPrinterSettingsStore();
                    var local = store.Load();
                    local.BarcodePrintEngine = normalized;
                    store.Save(local);
                }
                catch { }

                if (_settingsRepository != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try { await _settingsRepository.SaveAsync(AppSetting.BarcodePrintEngineKey, normalized); } catch { }
                    });
                }
            }
        }
    }

    public void ApplyDefaultStickerFormat()
    {
        var targetFormat = string.Equals(_typePcWt, "PC", StringComparison.OrdinalIgnoreCase)
            ? "StickerPC"
            : "StickerWt";

        var match = StickerFormats.FirstOrDefault(s => string.Equals(s, targetFormat, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            StickerFormats.Insert(0, targetFormat);
            match = targetFormat;
        }
        _stickerFormat = match;
        OnPropertyChanged(nameof(StickerFormat));
    }

    public string PhotoPath
    {
        get => _photoPath;
        set
        {
            if (SetProperty(ref _photoPath, value))
            {
                OnPropertyChanged(nameof(HasPhoto));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);

    public string BoxNo
    {
        get => _boxNo;
        set => SetProperty(ref _boxNo, value);
    }

    public string Godown
    {
        get => _godown;
        set => SetProperty(ref _godown, value);
    }

    public string CurrentClockText
    {
        get => _currentClockText;
        set => SetProperty(ref _currentClockText, value);
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                ApplyFilter();
            }
        }
    }

    private string _searchBarcodeText = string.Empty;
    public string SearchBarcodeText
    {
        get => _searchBarcodeText;
        set => SetProperty(ref _searchBarcodeText, value);
    }

    public BarcodeSubformItem? SelectedRecentBarcode
    {
        get => _selectedRecentBarcode;
        set
        {
            if (SetProperty(ref _selectedRecentBarcode, value))
            {
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                RefreshCommands();

                if (!_suppressSync && value is not null)
                {
                    PopulateFromItem(value);
                    FormMode = BarcodeFormMode.View;
                }
            }
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;

            var accountsTask = _accountRepository.GetAllAsync();
            var itemsTask = _itemRepository.GetAllAsync();
            var puritiesTask = _purityRepository.GetAllAsync();
            var stickersTask = _repository.GetStickerFormatsAsync();
            var barcodesTask = _repository.GetOpeningBarcodesAsync(30);

            await Task.WhenAll(accountsTask, itemsTask, puritiesTask, stickersTask, barcodesTask);

            var accounts = await accountsTask;
            Suppliers.Clear();

            // Default opening stock supplier
            Suppliers.Add(new Account { AccountName = "OPN", HeadCategory = "Opening Stock" });

            foreach (var acc in accounts)
            {
                if (acc.HeadCategory?.Contains("Creditor", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (!Suppliers.Any(s => string.Equals(s.AccountName, acc.AccountName, StringComparison.OrdinalIgnoreCase)))
                    {
                        Suppliers.Add(acc);
                    }
                }
            }

            var items = await itemsTask;
            AvailableItems.Clear();
            foreach (var item in items)
            {
                AvailableItems.Add(item);
            }

            var purities = await puritiesTask;
            AvailablePurities.Clear();
            foreach (var p in purities)
            {
                AvailablePurities.Add(p);
            }

            var stickers = await stickersTask;
            StickerFormats.Clear();

            // Ensure StickerWt and StickerPC are always top choices
            StickerFormats.Add("StickerWt");
            StickerFormats.Add("StickerPC");

            foreach (var s in stickers)
            {
                if (!string.IsNullOrWhiteSpace(s) &&
                    !StickerFormats.Any(x => string.Equals(x, s, StringComparison.OrdinalIgnoreCase)))
                {
                    StickerFormats.Add(s);
                }
            }

            var prnDir = Path.Combine(AppContext.BaseDirectory, "PrintTemplates", "BarcodePRN");
            if (Directory.Exists(prnDir))
            {
                foreach (var prnFile in Directory.GetFiles(prnDir, "*.prn"))
                {
                    var name = Path.GetFileNameWithoutExtension(prnFile);
                    if (!string.IsNullOrWhiteSpace(name) && !StickerFormats.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        StickerFormats.Add(name);
                    }
                }
            }

            var frxDir = Path.Combine(AppContext.BaseDirectory, "PrintTemplates", "BarcodeFRX");
            if (Directory.Exists(frxDir))
            {
                foreach (var frxFile in Directory.GetFiles(frxDir, "*.frx"))
                {
                    var name = Path.GetFileNameWithoutExtension(frxFile);
                    if (!string.IsNullOrWhiteSpace(name) && !StickerFormats.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        StickerFormats.Add(name);
                    }
                }
            }

            try
            {
                var localPrinterSettings = new LocalPrinterSettingsStore().Load();
                if (!string.IsNullOrWhiteSpace(localPrinterSettings.BarcodePrintEngine))
                {
                    _barcodePrintEngine = BarcodePrintService.NormalizeEngine(localPrinterSettings.BarcodePrintEngine);
                    OnPropertyChanged(nameof(BarcodePrintEngine));
                }
                else if (_settingsRepository != null)
                {
                    var dbEngine = await _settingsRepository.GetValueAsync(AppSetting.BarcodePrintEngineKey);
                    if (!string.IsNullOrWhiteSpace(dbEngine))
                    {
                        _barcodePrintEngine = BarcodePrintService.NormalizeEngine(dbEngine);
                        OnPropertyChanged(nameof(BarcodePrintEngine));
                    }
                }
            }
            catch { }

            ApplyDefaultStickerFormat();

            var barcodes = await barcodesTask;
            _suppressSync = true;
            RecentBarcodes.Clear();
            foreach (var item in barcodes)
            {
                RecentBarcodes.Add(item);
            }
            _suppressSync = false;

            if (_boxMasterRepository is not null)
            {
                try
                {
                    var boxes = await _boxMasterRepository.GetAllAsync();
                    AvailableBoxes.Clear();
                    foreach (var b in boxes)
                    {
                        AvailableBoxes.Add(b);
                    }
                }
                catch { }
            }

            PrepareBlankForm();
        }
        catch (Exception ex)
        {
            ShowToast($"Initialization failed: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }

        // ── Weight Machine ─────────────────────────────────────────────
        // Started AFTER IsBusy = false so the New button is immediately
        // available. COM port open runs on a background thread anyway.
        _ = StartWeightServiceIfEnabledAsync();
        // ──────────────────────────────────────────────────────────────
    }

    public async Task LoadRecentBarcodesAsync()
    {
        try
        {
            var list = await _repository.GetOpeningBarcodesAsync(30);
            _suppressSync = true;
            RecentBarcodes.Clear();
            foreach (var item in list)
            {
                RecentBarcodes.Add(item);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load barcodes: {ex.Message}", ToastType.Error);
        }
        finally
        {
            _suppressSync = false;
        }
    }

    private BarcodeSubformItem? _lastCreatedItem;

    private async Task NewBarcodeAsync()
    {
        try
        {
            BarcodeSubformItem? lastItem = _lastCreatedItem ?? SelectedRecentBarcode ?? RecentBarcodes.FirstOrDefault();

            if (lastItem is null)
            {
                var recentList = await _repository.GetOpeningBarcodesAsync(1);
                lastItem = recentList.FirstOrDefault();
            }

            if (lastItem is null && !string.IsNullOrWhiteSpace(ItemName))
            {
                lastItem = BuildCurrentBarcodeItem();
            }

            bool repeatLast = false;

            if (lastItem is not null)
            {
                var result = MessageBox.Show(
                    "Repeat Last Details?",
                    "Barcode Creation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                repeatLast = (result == MessageBoxResult.Yes);
            }

            IsBusy = true;
            var nextBarcode = await _repository.GetNextBarcodeNumberAsync();
            var nextAuto = await _repository.GetNextBarcodeAutoNoAsync();

            if (repeatLast && lastItem is not null)
            {
                PopulateFromItem(lastItem);
                Barcode = nextBarcode;
                Nos = int.TryParse(nextBarcode, out var n) ? n : 0;
                BarcodeAutoNo = nextAuto;
                GrossWeight = 0m;
                LessWeight = 0m;
                NetWeight = 0m;
                FormMode = BarcodeFormMode.Add;
                ShowToast($"Loaded details from last barcode for: {Barcode}", ToastType.Info);
            }
            else
            {
                PrepareBlankForm();
                Barcode = nextBarcode;
                Nos = int.TryParse(nextBarcode, out var n) ? n : 0;
                BarcodeAutoNo = nextAuto;
                FormMode = BarcodeFormMode.Add;
                ShowToast($"Ready to create new barcode: {Barcode}", ToastType.Info);
            }

            _suppressSync = true;
            SelectedRecentBarcode = null;
            _suppressSync = false;

            RequestFocusType?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowToast($"Error generating barcode: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SearchAndLoadBarcodeAsync(string? barcodeToSearch = null)
    {
        var code = (barcodeToSearch ?? SearchBarcodeText)?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            ShowToast("Please enter a barcode number to search.", ToastType.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            // 1. Try finding in database (queries both exact and D6 padded format)
            var item = await _repository.GetBarcodeByCodeAsync(code);

            // 2. Fallback to in-memory recent list
            if (item == null)
            {
                var d6 = int.TryParse(code, out var n) ? n.ToString("D6") : code;
                item = RecentBarcodes.FirstOrDefault(b =>
                    string.Equals(b.Barcode, code, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(b.Barcode, d6, StringComparison.OrdinalIgnoreCase));
            }

            if (item != null)
            {
                PopulateFromItem(item);
                FormMode = BarcodeFormMode.Modify;
                _suppressSync = true;
                SelectedRecentBarcode = RecentBarcodes.FirstOrDefault(b => b.Barcode == item.Barcode);
                _suppressSync = false;
                SearchBarcodeText = string.Empty;
                ShowToast($"Loaded barcode '{item.Barcode}' for modification.", ToastType.Success);
                RequestFocusType?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ShowToast($"Barcode '{code}' not found.", ToastType.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Error loading barcode: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyBarcode()
    {
        if (SelectedRecentBarcode is null)
        {
            ShowToast("Select a barcode to modify.", ToastType.Warning);
            return;
        }

        FormMode = BarcodeFormMode.Modify;
        ShowToast($"Editing barcode {Barcode}.", ToastType.Info);
        RequestFocusType?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(ItemName))
        {
            ShowToast("Please enter or select an Item Name.", ToastType.Warning);
            return;
        }

        if (GrossWeight <= 0 && Qty <= 0)
        {
            ShowToast("Please enter a valid Gross Weight or Quantity.", ToastType.Warning);
            return;
        }

        var choice = BarcodeSaveConfirmationDialog.Show(Barcode, ItemName);
        if (choice == BarcodeSaveChoice.Cancel)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var isUpdate = FormMode == BarcodeFormMode.Modify;

            var item = new BarcodeSubformItem
            {
                Barcode = Barcode.Trim(),
                BarcodeAutoNo = BarcodeAutoNo,
                Nos = Nos,
                VouNo = "9",
                BType = string.IsNullOrWhiteSpace(TypePcWt) || TypePcWt == "-" ? "WT" : TypePcWt,
                ItemName = ItemName.Trim(),
                Purity = Purity.Trim(),
                Description = Description.Trim(),
                HUID = HUID.Trim(),
                Size = string.IsNullOrWhiteSpace(Size) ? "-" : Size.Trim(),
                Qty = Qty,
                GrossWt = GrossWeight,
                StoneWt = LessWeight,
                NetWt = NetWeight,
                Making = Making,
                MakingType = string.IsNullOrWhiteSpace(MakingType) ? "GRAM" : MakingType,
                StoneValue = StoneValue,
                CostTounch = CostTounch,
                CostPg = CostPerGram,
                CostMaking = CostMC,
                CostPrice = CostPrice,
                CostRate = CostRate,
                MRP = MrpPrice,
                Profit = ProfitMultiple,
                SupplierName = string.IsNullOrWhiteSpace(Supplier) ? "OPN" : Supplier.Trim(),
                PartyCode = Suppliers.FirstOrDefault(s => string.Equals(s.AccountName, Supplier, StringComparison.OrdinalIgnoreCase))?.PartyCode?.Trim() is { Length: > 0 } pc ? pc : (string.IsNullOrWhiteSpace(Supplier) || string.Equals(Supplier, "OPN", StringComparison.OrdinalIgnoreCase) ? "OPN" : Supplier.Trim()),
                SuppInvNo = InvoiceNo.Trim(),
                SuppDate = InvoiceDate,
                PDate = DateTime.Today,
                PunchedOn = DateTime.Now,
                Photo = string.Empty,
                Sti = string.IsNullOrWhiteSpace(StickerFormat) ? (string.Equals(TypePcWt, "PC", StringComparison.OrdinalIgnoreCase) ? "StickerPC" : "StickerWt") : StickerFormat,
                Status = "Active",
                EntryUser = string.IsNullOrWhiteSpace(_currentUser) ? "admin" : _currentUser,
                ModifyUser = isUpdate ? _currentUser : string.Empty,
                ModifyDate = isUpdate ? DateTime.Today : null,
                ModifyTime = isUpdate ? DateTime.Now : null,
                BoxNo = BoxNo.Trim(),
                Godown = Godown.Trim()
            };

            // Ensure photo is named {Barcode}.jpg in Settings.SavePath\Barcode
            if (!string.IsNullOrWhiteSpace(PhotoPath) && File.Exists(PhotoPath))
            {
                try
                {
                    var savePath = await GetSavePathAsync();
                    var barcodeDir = Path.Combine(savePath, "Barcode");
                    if (!Directory.Exists(barcodeDir))
                    {
                        Directory.CreateDirectory(barcodeDir);
                    }

                    var desiredPath = Path.Combine(barcodeDir, $"{item.Barcode.Trim()}.jpg");
                    if (!string.Equals(Path.GetFullPath(PhotoPath), Path.GetFullPath(desiredPath), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(PhotoPath, desiredPath, overwrite: true);
                        PhotoPath = desiredPath;
                    }
                }
                catch
                {
                    // keep current PhotoPath
                }
            }

            // Do not save full image path in DB column; image path is dynamically constructed from SavePath\Barcode\{Barcode}.jpg
            item.Photo = string.Empty;

            await _repository.SaveSingleOpeningBarcodeAsync(item, isUpdate);
            _lastCreatedItem = item;

            if (isUpdate && _auditRepository != null)
            {
                await AuditService.LogBarcodeModificationAsync(_auditRepository, _originalBarcodeSnapshot, item, _currentUser);
            }

            ShowToast($"Barcode {item.Barcode} saved successfully!", ToastType.Success);
            BarcodeEvents.NotifyBarcodesChanged();

            if (choice == BarcodeSaveChoice.SaveAndPrint)
            {
                await PrintStickerAsync(item);
            }

            await LoadRecentBarcodesAsync();

            _suppressSync = true;
            SelectedRecentBarcode = RecentBarcodes.FirstOrDefault(b => b.Barcode == item.Barcode);
            _suppressSync = false;

            FormMode = BarcodeFormMode.View;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save barcode: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(Barcode))
        {
            ShowToast("No barcode selected for deletion.", ToastType.Warning);
            return;
        }

        var res = MessageBox.Show(
            $"Are you sure you want to delete Barcode '{Barcode}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var itemToDelete = SelectedRecentBarcode ?? _originalBarcodeSnapshot ?? BuildCurrentBarcodeItem();
            await _repository.DeleteBarcodeAsync(Barcode);

            if (_auditRepository != null && itemToDelete != null)
            {
                await AuditService.LogBarcodeDeletionAsync(_auditRepository, itemToDelete, _currentUser);
            }

            ShowToast($"Barcode {Barcode} deleted.", ToastType.Success);
            BarcodeEvents.NotifyBarcodesChanged();

            await LoadRecentBarcodesAsync();
            PrepareBlankForm();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to delete barcode: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelBarcode()
    {
        if (SelectedRecentBarcode is not null)
        {
            PopulateFromItem(SelectedRecentBarcode);
        }
        else
        {
            PrepareBlankForm();
        }

        FormMode = BarcodeFormMode.View;
        ShowToast("Operation cancelled.", ToastType.Info);
    }

    private void OpenBoxMaster()
    {
        if (_boxMasterRepository is null)
        {
            ShowToast("Box Master repository not configured.", ToastType.Warning);
            return;
        }

        var vm = new BoxMasterViewModel(_boxMasterRepository, _currentUser);
        var win = new BoxPickerWindow(vm);
        win.SetOwnerSafely();

        if (win.ShowDialog() == true && win.SelectedBox != null)
        {
            BoxNo = win.SelectedBox.BoxNo;
            ShowToast($"Selected Box: {win.SelectedBox.DisplayText}", ToastType.Success);
        }

        _ = RefreshBoxesAsync();
    }

    private async Task RefreshBoxesAsync()
    {
        if (_boxMasterRepository is null) return;
        try
        {
            var boxes = await _boxMasterRepository.GetAllAsync();
            AvailableBoxes.Clear();
            foreach (var b in boxes)
            {
                AvailableBoxes.Add(b);
            }
        }
        catch { }
    }

    public async Task RefreshAsync() => await RefreshLookupsAsync();

    public async Task RefreshLookupsAsync()
    {
        try
        {
            IsBusy = true;

            var currentItem = ItemName;
            var currentPurity = Purity;
            var currentSupplier = Supplier;
            var currentBox = BoxNo;

            var accountsTask = _accountRepository.GetAllAsync();
            var itemsTask = _itemRepository.GetAllAsync();
            var puritiesTask = _purityRepository.GetAllAsync();
            var stickersTask = _repository.GetStickerFormatsAsync();
            var boxesTask = _boxMasterRepository is not null ? _boxMasterRepository.GetAllAsync() : Task.FromResult<IReadOnlyList<BoxMaster>>([]);

            await Task.WhenAll(accountsTask, itemsTask, puritiesTask, stickersTask, boxesTask);

            // 1. Suppliers
            var accounts = await accountsTask;
            Suppliers.Clear();
            Suppliers.Add(new Account { AccountName = "OPN", HeadCategory = "Opening Stock" });
            foreach (var acc in accounts)
            {
                if (acc.HeadCategory?.Contains("Creditor", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (!Suppliers.Any(s => string.Equals(s.AccountName, acc.AccountName, StringComparison.OrdinalIgnoreCase)))
                    {
                        Suppliers.Add(acc);
                    }
                }
            }

            // 2. Items
            var items = await itemsTask;
            AvailableItems.Clear();
            foreach (var item in items)
            {
                AvailableItems.Add(item);
            }

            // 3. Purities
            var purities = await puritiesTask;
            AvailablePurities.Clear();
            foreach (var p in purities)
            {
                AvailablePurities.Add(p);
            }

            // 4. Stickers
            var stickers = await stickersTask;
            StickerFormats.Clear();
            StickerFormats.Add("StickerWt");
            StickerFormats.Add("StickerPC");
            foreach (var s in stickers)
            {
                if (!string.IsNullOrWhiteSpace(s) && !StickerFormats.Any(x => string.Equals(x, s, StringComparison.OrdinalIgnoreCase)))
                {
                    StickerFormats.Add(s);
                }
            }

            var prnDir = Path.Combine(AppContext.BaseDirectory, "PrintTemplates", "BarcodePRN");
            if (Directory.Exists(prnDir))
            {
                foreach (var prnFile in Directory.GetFiles(prnDir, "*.prn"))
                {
                    var name = Path.GetFileNameWithoutExtension(prnFile);
                    if (!string.IsNullOrWhiteSpace(name) && !StickerFormats.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        StickerFormats.Add(name);
                    }
                }
            }

            var frxDir = Path.Combine(AppContext.BaseDirectory, "PrintTemplates", "BarcodeFRX");
            if (Directory.Exists(frxDir))
            {
                foreach (var frxFile in Directory.GetFiles(frxDir, "*.frx"))
                {
                    var name = Path.GetFileNameWithoutExtension(frxFile);
                    if (!string.IsNullOrWhiteSpace(name) && !StickerFormats.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        StickerFormats.Add(name);
                    }
                }
            }

            // 5. Boxes
            var boxes = await boxesTask;
            AvailableBoxes.Clear();
            foreach (var b in boxes)
            {
                AvailableBoxes.Add(b);
            }

            // Restore preserved values
            if (!string.IsNullOrWhiteSpace(currentItem))
            {
                ItemName = currentItem;
                var matched = AvailableItems.FirstOrDefault(i => string.Equals(i.ItemName, currentItem, StringComparison.OrdinalIgnoreCase));
                FilterPuritiesByItem(matched);
            }
            if (!string.IsNullOrWhiteSpace(currentPurity))
            {
                Purity = currentPurity;
            }
            if (!string.IsNullOrWhiteSpace(currentSupplier))
            {
                Supplier = currentSupplier;
            }
            if (!string.IsNullOrWhiteSpace(currentBox))
            {
                BoxNo = currentBox;
            }

            ShowToast("Comboboxes refreshed successfully (F9)", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to refresh comboboxes: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PrintStickerAsync(BarcodeSubformItem? targetItem = null)
    {
        var itemToPrint = targetItem ?? BuildCurrentBarcodeItem();
        if (string.IsNullOrWhiteSpace(itemToPrint.Barcode))
        {
            ShowToast("No active barcode to print.", ToastType.Warning);
            return;
        }

        var effectiveFormat = !string.IsNullOrWhiteSpace(StickerFormat)
            ? StickerFormat
            : (string.Equals(itemToPrint.BType, "PC", StringComparison.OrdinalIgnoreCase) ? "StickerPC" : "StickerWt");

        var ext = string.Equals(BarcodePrintEngine, "FRX", StringComparison.OrdinalIgnoreCase) ? ".frx" : ".prn";

        try
        {
            await _barcodePrintService.PrintBarcodeStickerAsync(itemToPrint, effectiveFormat, targetPrinter: null, printEngine: BarcodePrintEngine);
            ShowToast($"Barcode #{itemToPrint.Barcode} sent to printer using {effectiveFormat}{ext} ({BarcodePrintEngine})!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Printing error: {ex.Message}", ToastType.Error);
        }
    }

    private BarcodeSubformItem BuildCurrentBarcodeItem(bool isUpdate = false)
    {
        return new BarcodeSubformItem
        {
            Barcode = Barcode.Trim(),
            BarcodeAutoNo = BarcodeAutoNo,
            Nos = Nos,
            VouNo = "9",
            BType = string.IsNullOrWhiteSpace(TypePcWt) || TypePcWt == "-" ? "WT" : TypePcWt,
            ItemName = ItemName.Trim(),
            Purity = Purity.Trim(),
            Description = Description.Trim(),
            HUID = HUID.Trim(),
            Size = string.IsNullOrWhiteSpace(Size) ? "-" : Size.Trim(),
            Qty = Qty,
            GrossWt = GrossWeight,
            StoneWt = LessWeight,
            NetWt = NetWeight,
            Making = Making,
            MakingType = string.IsNullOrWhiteSpace(MakingType) ? "GRAM" : MakingType,
            StoneValue = StoneValue,
            CostTounch = CostTounch,
            CostPg = CostPerGram,
            CostMaking = CostMC,
            CostPrice = CostPrice,
            CostRate = CostRate,
            MRP = MrpPrice,
            Profit = ProfitMultiple,
            SupplierName = string.IsNullOrWhiteSpace(Supplier) ? "OPN" : Supplier.Trim(),
            PartyCode = Suppliers.FirstOrDefault(s => string.Equals(s.AccountName, Supplier, StringComparison.OrdinalIgnoreCase))?.PartyCode?.Trim() is { Length: > 0 } pc2 ? pc2 : (string.IsNullOrWhiteSpace(Supplier) || string.Equals(Supplier, "OPN", StringComparison.OrdinalIgnoreCase) ? "OPN" : Supplier.Trim()),
            SuppInvNo = InvoiceNo.Trim(),
            SuppDate = InvoiceDate,
            PDate = DateTime.Today,
            PunchedOn = DateTime.Now,
            Photo = string.Empty,
            Sti = string.IsNullOrWhiteSpace(StickerFormat) ? (string.Equals(TypePcWt, "PC", StringComparison.OrdinalIgnoreCase) ? "StickerPC" : "StickerWt") : StickerFormat,
            Status = "Active",
            EntryUser = string.IsNullOrWhiteSpace(_currentUser) ? "admin" : _currentUser,
            ModifyUser = isUpdate ? _currentUser : string.Empty,
            ModifyDate = isUpdate ? DateTime.Today : null,
            ModifyTime = isUpdate ? DateTime.Now : null,
            BoxNo = BoxNo.Trim(),
            Godown = Godown.Trim()
        };
    }

    private async Task SnapOrBrowsePhotoAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Item Photo",
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var savePath = await GetSavePathAsync();
                var barcodeDir = Path.Combine(savePath, "Barcode");
                if (!Directory.Exists(barcodeDir))
                {
                    Directory.CreateDirectory(barcodeDir);
                }

                var barcodeName = !string.IsNullOrWhiteSpace(Barcode) && Barcode != "------"
                    ? Barcode.Trim()
                    : $"temp_{DateTime.Now:yyyyMMddHHmmss}";

                var targetPath = Path.Combine(barcodeDir, $"{barcodeName}.jpg");

                if (!string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(dlg.FileName, targetPath, overwrite: true);
                }

                PhotoPath = targetPath;
                ShowToast("Photo updated in SavePath\\Barcode.", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast($"Photo attached (local): {ex.Message}", ToastType.Warning);
                PhotoPath = dlg.FileName;
            }
        }
    }

    private async Task AiEnhancePhotoAsync()
    {
        if (string.IsNullOrWhiteSpace(PhotoPath) || !File.Exists(PhotoPath))
        {
            ShowToast("Please snap or attach a photo first.", ToastType.Warning);
            return;
        }

        IsEnhancingPhoto = true;
        ShowToast("AI Enhancing image (cleaning background & luster)...", ToastType.Info);

        try
        {
            var options = _aiEnhancementService.GetDefaultOptions();
            var targetPath = PhotoPath;
            var result = await _aiEnhancementService.EnhanceImageAsync(PhotoPath, targetPath, options);

            if (result.Success && !string.IsNullOrWhiteSpace(result.EnhancedImagePath))
            {
                // Force UI to refresh image cache
                var currentPath = PhotoPath;
                PhotoPath = string.Empty;
                PhotoPath = currentPath;
                ShowToast($"✨ AI Enhanced in {result.Duration.TotalMilliseconds:F0}ms! Background neutralized.", ToastType.Success);
            }
            else
            {
                ShowToast(result.ErrorMessage ?? "AI Enhancement failed.", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"AI Enhance error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsEnhancingPhoto = false;
            RefreshCommands();
        }
    }

    private void OpenAiEnhanceSettings()
    {
        if (string.IsNullOrWhiteSpace(PhotoPath) || !File.Exists(PhotoPath))
        {
            ShowToast("Please attach a photo to preview AI settings.", ToastType.Warning);
            return;
        }

        var vm = new AiImageEnhanceSettingsViewModel(_aiEnhancementService, PhotoPath);
        var win = new AiImageEnhanceSettingsWindow(vm);
        win.SetOwnerSafely();

        if (win.ShowDialog() == true)
        {
            var chosenOptions = vm.ToOptions();
            _aiEnhancementService.SaveDefaultOptions(chosenOptions);
            _ = ApplyChosenAiEnhancementAsync(chosenOptions);
        }
    }

    private async Task ApplyChosenAiEnhancementAsync(AiImageEnhanceOptions options)
    {
        IsEnhancingPhoto = true;
        ShowToast("Applying custom AI enhancement...", ToastType.Info);

        try
        {
            var targetPath = PhotoPath;
            var result = await _aiEnhancementService.EnhanceImageAsync(PhotoPath, targetPath, options);

            if (result.Success && !string.IsNullOrWhiteSpace(result.EnhancedImagePath))
            {
                var currentPath = PhotoPath;
                PhotoPath = string.Empty;
                PhotoPath = currentPath;
                ShowToast($"✨ AI Enhanced successfully ({result.Duration.TotalMilliseconds:F0}ms)!", ToastType.Success);
            }
            else
            {
                ShowToast(result.ErrorMessage ?? "AI Enhancement failed.", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsEnhancingPhoto = false;
            RefreshCommands();
        }
    }

    private async Task<string> GetSavePathAsync()
    {
        try
        {
            if (_settingsRepository is not null)
            {
                var val = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    return val;
                }
            }
        }
        catch
        {
            // fallback
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ConceptJewellery", "Exports");
    }

    private void CalculateNetWeight()
    {
        var diff = GrossWeight - LessWeight;
        NetWeight = diff < 0 ? 0m : diff;
        RecalculateFromNetWeight();
    }

    private decimal GetBaseWeight()
    {
        if (NetWeight > 0)
            return NetWeight;
        if (string.Equals(TypePcWt, "PC", StringComparison.OrdinalIgnoreCase) && Qty > 0)
            return Qty;
        return 0m;
    }

    private void RecalculateFromNetWeight()
    {
        if (_isCalculating) return;
        _isCalculating = true;
        try
        {
            decimal baseWt = GetBaseWeight();
            _costPrice = Math.Round(baseWt * _costPerGram, 2);
            OnPropertyChanged(nameof(CostPrice));
            RecalculateMrpInternal();
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void RecalculateFromCostRate()
    {
        if (_isCalculating) return;
        _isCalculating = true;
        try
        {
            // Cost /Gram = (Cost Rate * Cost Tounch / 100) + Cost MC
            _costPerGram = Math.Round((_costRate * _costTounch / 100m) + _costMC, 2);
            OnPropertyChanged(nameof(CostPerGram));

            decimal baseWt = GetBaseWeight();
            _costPrice = Math.Round(baseWt * _costPerGram, 2);
            OnPropertyChanged(nameof(CostPrice));

            RecalculateMrpInternal();
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void RecalculateFromCostTounch()
    {
        if (_isCalculating) return;
        _isCalculating = true;
        try
        {
            _costPerGram = Math.Round((_costRate * _costTounch / 100m) + _costMC, 2);
            OnPropertyChanged(nameof(CostPerGram));

            decimal baseWt = GetBaseWeight();
            _costPrice = Math.Round(baseWt * _costPerGram, 2);
            OnPropertyChanged(nameof(CostPrice));

            RecalculateMrpInternal();
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void RecalculateFromCostMC()
    {
        if (_isCalculating) return;
        _isCalculating = true;
        try
        {
            _costPerGram = Math.Round((_costRate * _costTounch / 100m) + _costMC, 2);
            OnPropertyChanged(nameof(CostPerGram));

            decimal baseWt = GetBaseWeight();
            _costPrice = Math.Round(baseWt * _costPerGram, 2);
            OnPropertyChanged(nameof(CostPrice));

            RecalculateMrpInternal();
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void RecalculateFromCostPerGram()
    {
        if (_isCalculating) return;
        _isCalculating = true;
        try
        {
            if (_costTounch > 0)
            {
                _costRate = Math.Round((_costPerGram - _costMC) / (_costTounch / 100m), 2);
                OnPropertyChanged(nameof(CostRate));
            }

            decimal baseWt = GetBaseWeight();
            _costPrice = Math.Round(baseWt * _costPerGram, 2);
            OnPropertyChanged(nameof(CostPrice));

            RecalculateMrpInternal();
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void RecalculateFromCostPrice()
    {
        if (_isCalculating) return;
        _isCalculating = true;
        try
        {
            decimal baseWt = GetBaseWeight();
            if (baseWt > 0)
            {
                _costPerGram = Math.Round(_costPrice / baseWt, 2);
                OnPropertyChanged(nameof(CostPerGram));
                if (_costTounch > 0)
                {
                    _costRate = Math.Round((_costPerGram - _costMC) / (_costTounch / 100m), 2);
                    OnPropertyChanged(nameof(CostRate));
                }
            }

            RecalculateMrpInternal();
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void RecalculateFromProfitMultiple()
    {
        if (_isCalculating) return;
        _isCalculating = true;
        try
        {
            RecalculateMrpInternal();
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void RecalculateFromMrpPrice()
    {
        if (_isCalculating) return;
        _isCalculating = true;
        try
        {
            if (_costPrice > 0)
            {
                _profitMultiple = Math.Round(_mrpPrice / _costPrice, 2);
                _costMultiplier = _profitMultiple.ToString("0.00");
                OnPropertyChanged(nameof(ProfitMultiple));
                OnPropertyChanged(nameof(CostMultiplier));
            }
            decimal baseWt = GetBaseWeight();
            _minRate = baseWt > 0 ? Math.Round(_mrpPrice / baseWt, 2) : 0m;
            OnPropertyChanged(nameof(MinRate));
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void RecalculateFromMinRate()
    {
        if (_isCalculating) return;
        _isCalculating = true;
        try
        {
            decimal baseWt = GetBaseWeight();
            if (baseWt > 0)
            {
                decimal rawMrp = _minRate * baseWt;
                _mrpPrice = Math.Round(rawMrp / 10m, MidpointRounding.AwayFromZero) * 10m;
                OnPropertyChanged(nameof(MrpPrice));

                if (_costPrice > 0)
                {
                    _profitMultiple = Math.Round(_mrpPrice / _costPrice, 2);
                    _costMultiplier = _profitMultiple.ToString("0.00");
                    OnPropertyChanged(nameof(ProfitMultiple));
                    OnPropertyChanged(nameof(CostMultiplier));
                }
            }
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private void RecalculateMrpInternal()
    {
        if (_costPrice > 0 && _profitMultiple > 0)
        {
            decimal rawMrp = _costPrice * _profitMultiple;
            _mrpPrice = Math.Round(rawMrp / 10m, MidpointRounding.AwayFromZero) * 10m;
        }
        else if (_costPrice == 0)
        {
            _mrpPrice = 0m;
        }
        OnPropertyChanged(nameof(MrpPrice));

        decimal baseWt = GetBaseWeight();
        if (baseWt > 0 && _mrpPrice > 0)
        {
            _minRate = Math.Round(_mrpPrice / baseWt, 2);
        }
        else if (_costPerGram > 0 && _profitMultiple > 0)
        {
            _minRate = Math.Round(_costPerGram * _profitMultiple, 2);
        }
        else
        {
            _minRate = 0m;
        }
        OnPropertyChanged(nameof(MinRate));
    }

    private void OnPurityChanged(string? purity)
    {
        if (string.IsNullOrWhiteSpace(purity))
            return;

        var matched = AvailablePurities.FirstOrDefault(p => string.Equals(p.Purity, purity, StringComparison.OrdinalIgnoreCase));
        if (matched is not null)
        {
            if (_costRate == 0 && matched.RatePerGram > 0)
            {
                CostRate = matched.RatePerGram;
            }
        }

        if (_costTounch == 0)
        {
            CostTounch = 100m;
        }
    }

    private void AutoFillItemDefaults(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            FilterPuritiesByItem(null);
            return;
        }
        var matched = AvailableItems.FirstOrDefault(i => string.Equals(i.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
        FilterPuritiesByItem(matched);
        if (matched is not null)
        {
            var defaultPurity = !string.IsNullOrWhiteSpace(matched.RawPurity)
                ? matched.RawPurity
                : (matched.Purity > 0 ? matched.Purity.ToString() : string.Empty);

            if (!string.IsNullOrWhiteSpace(defaultPurity))
            {
                if (!FilteredPurities.Any(p => string.Equals(p, defaultPurity, StringComparison.OrdinalIgnoreCase)))
                {
                    FilteredPurities.Insert(0, defaultPurity);
                }
                Purity = defaultPurity;
                OnPropertyChanged(nameof(Purity));
            }
        }
    }

    private void FilterPuritiesByItem(Item? item)
    {
        FilteredPurities.Clear();

        if (item is null || string.IsNullOrWhiteSpace(item.HeadGroup))
        {
            // No item selected — show all purities
            foreach (var p in AvailablePurities)
            {
                if (!string.IsNullOrWhiteSpace(p.Purity) &&
                    !FilteredPurities.Any(x => string.Equals(x, p.Purity, StringComparison.OrdinalIgnoreCase)))
                {
                    FilteredPurities.Add(p.Purity);
                }
            }
        }
        else
        {
            foreach (var p in AvailablePurities)
            {
                if (string.Equals(p.ItemHead, item.HeadGroup, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(p.Purity) &&
                        !FilteredPurities.Any(x => string.Equals(x, p.Purity, StringComparison.OrdinalIgnoreCase)))
                    {
                        FilteredPurities.Add(p.Purity);
                    }
                }
            }

            // If no purities matched the item's HeadGroup, fall back to showing all purities
            if (FilteredPurities.Count == 0)
            {
                foreach (var p in AvailablePurities)
                {
                    if (!string.IsNullOrWhiteSpace(p.Purity) &&
                        !FilteredPurities.Any(x => string.Equals(x, p.Purity, StringComparison.OrdinalIgnoreCase)))
                    {
                        FilteredPurities.Add(p.Purity);
                    }
                }
            }
        }
    }

    private void PopulateFromItem(BarcodeSubformItem item)
    {
        Barcode = item.Barcode;
        BarcodeAutoNo = item.BarcodeAutoNo;
        Nos = item.Nos;
        TypePcWt = string.IsNullOrWhiteSpace(item.BType) ? "WT" : item.BType;
        ItemName = item.ItemName;
        // ItemName setter calls AutoFillItemDefaults which filters purities
        // and may set a different Purity. Re-set the barcode's actual Purity.
        if (!string.IsNullOrWhiteSpace(item.Purity) &&
            !FilteredPurities.Any(p => string.Equals(p, item.Purity, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredPurities.Insert(0, item.Purity);
        }
        Purity = item.Purity;
        OnPropertyChanged(nameof(Purity));
        Description = item.Description;
        HUID = item.HUID;
        Size = string.IsNullOrWhiteSpace(item.Size) ? "-" : item.Size;
        Qty = item.Qty;
        GrossWeight = item.GrossWt;
        LessWeight = item.StoneWt;
        NetWeight = item.NetWt;
        Making = item.Making;
        MakingType = string.IsNullOrWhiteSpace(item.MakingType) ? "GRAM" : item.MakingType;
        StoneValue = item.StoneValue;

        _isCalculating = true;
        try
        {
            CostTounch = item.CostTounch;
            CostPerGram = item.CostPg;
            CostMC = item.CostMaking;
            CostPrice = item.CostPrice;
            CostRate = item.CostRate;
            ProfitMultiple = item.Profit > 0 ? item.Profit : (item.CostPrice > 0 && item.MRP > 0 ? Math.Round(item.MRP / item.CostPrice, 2) : 1.65m);
            MrpPrice = item.MRP;
            MinRate = item.NetWt > 0 && item.MRP > 0 ? Math.Round(item.MRP / item.NetWt, 2) : (item.CostPg > 0 ? Math.Round(item.CostPg * ProfitMultiple, 2) : 0m);
        }
        finally
        {
            _isCalculating = false;
        }

        Supplier = string.IsNullOrWhiteSpace(item.SupplierName) ? "OPN" : item.SupplierName;
        InvoiceDate = item.SuppDate ?? DateTime.Today;
        InvoiceNo = item.SuppInvNo;
        if (!string.IsNullOrWhiteSpace(item.Sti) &&
            (string.Equals(item.Sti, "StickerPC", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.Sti, "StickerWt", StringComparison.OrdinalIgnoreCase)))
        {
            StickerFormat = item.Sti;
        }
        else
        {
            ApplyDefaultStickerFormat();
        }
        _ = LoadPhotoForBarcodeAsync(item.Barcode);
        BoxNo = item.BoxNo;
        Godown = item.Godown;

        _originalBarcodeSnapshot = new BarcodeSubformItem
        {
            Barcode = item.Barcode,
            BarcodeAutoNo = item.BarcodeAutoNo,
            Nos = item.Nos,
            VouNo = item.VouNo,
            BType = item.BType,
            ItemName = item.ItemName,
            Purity = item.Purity,
            Description = item.Description,
            HUID = item.HUID,
            Size = item.Size,
            Qty = item.Qty,
            GrossWt = item.GrossWt,
            StoneWt = item.StoneWt,
            NetWt = item.NetWt,
            Making = item.Making,
            MakingType = item.MakingType,
            StoneValue = item.StoneValue,
            CostTounch = item.CostTounch,
            CostPg = item.CostPg,
            CostMaking = item.CostMaking,
            CostPrice = item.CostPrice,
            CostRate = item.CostRate,
            MRP = item.MRP,
            Profit = item.Profit,
            SupplierName = item.SupplierName,
            SuppInvNo = item.SuppInvNo,
            SuppDate = item.SuppDate,
            PDate = item.PDate,
            PunchedOn = item.PunchedOn,
            Photo = item.Photo,
            Sti = item.Sti,
            Status = item.Status,
            BoxNo = item.BoxNo,
            Godown = item.Godown
        };
    }

    private async Task<string> ResolveBarcodePhotoPathAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || barcode == "------")
            return string.Empty;

        try
        {
            var savePath = await GetSavePathAsync();
            var barcodeDir = Path.Combine(savePath, "Barcode");
            if (Directory.Exists(barcodeDir))
            {
                var cleanCode = barcode.Trim();
                var jpgPath = Path.Combine(barcodeDir, $"{cleanCode}.jpg");
                if (File.Exists(jpgPath)) return jpgPath;

                var jpegPath = Path.Combine(barcodeDir, $"{cleanCode}.jpeg");
                if (File.Exists(jpegPath)) return jpegPath;

                var pngPath = Path.Combine(barcodeDir, $"{cleanCode}.png");
                if (File.Exists(pngPath)) return pngPath;
            }
        }
        catch { }

        return string.Empty;
    }

    private async Task LoadPhotoForBarcodeAsync(string barcode)
    {
        var resolved = await ResolveBarcodePhotoPathAsync(barcode);
        PhotoPath = resolved;
    }

    private void PrepareBlankForm()
    {
        Barcode = string.Empty;
        BarcodeAutoNo = 0;
        Nos = 0;
        TypePcWt = "WT";
        ItemName = string.Empty;
        Purity = string.Empty;
        Description = string.Empty;
        HUID = string.Empty;
        Size = "-";
        Qty = 1m;
        GrossWeight = 0m;
        LessWeight = 0m;
        NetWeight = 0m;
        Making = 0m;
        MakingType = "GRAM";
        StoneValue = 0m;

        _isCalculating = true;
        try
        {
            CostTounch = 100m;
            CostPerGram = 0m;
            CostMC = 0m;
            CostPrice = 0m;
            CostRate = 0m;
            MinRate = 0m;
            ProfitMultiple = 1.65m;
            CostMultiplier = "1.65";
            MrpPrice = 0m;
        }
        finally
        {
            _isCalculating = false;
        }

        Supplier = "OPN";
        InvoiceDate = DateTime.Today;
        InvoiceNo = string.Empty;
        ApplyDefaultStickerFormat();
        PhotoPath = string.Empty;
        BoxNo = string.Empty;
        Godown = string.Empty;
        FormMode = BarcodeFormMode.View;
    }

    private void ApplyFilter()
    {
        // If needed to filter the recent list
    }

    private void RefreshCommands()
    {
        NewCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        SnapPhotoCommand.RaiseCanExecuteChanged();
        ClearPhotoCommand.RaiseCanExecuteChanged();
        AiEnhancePhotoCommand?.RaiseCanExecuteChanged();
        OpenAiEnhanceSettingsCommand?.RaiseCanExecuteChanged();
        PrintStickerCommand.RaiseCanExecuteChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastMessage = message;
        ToastType = type;
    }

    public bool HasUnsavedChanges()
    {
        return FormMode != BarcodeFormMode.View ||
               !string.IsNullOrWhiteSpace(ItemName) ||
               GrossWeight > 0 ||
               NetWeight > 0;
    }

    // ── Weight Machine ───────────────────────────────────────────────

    private async Task StartWeightServiceIfEnabledAsync()
    {
        if (_settingsRepository is null) return;

        try
        {
            var settings = await _settingsRepository.GetAllAsync();

            var enabledStr = settings.TryGetValue(AppSetting.WeightEnabledKey, out var e) ? e : "false";
            WeightEnabled = bool.TryParse(enabledStr, out var enabled) && enabled;

            if (!WeightEnabled)
            {
                WeightStatusText = "Scale disabled";
                return;
            }

            var commPort     = settings.TryGetValue(AppSetting.WeightCommPortKey,     out var p)  ? p  : "COM1";
            var baudRate     = settings.TryGetValue(AppSetting.WeightBaudRateKey,     out var b)  ? b  : "9600";
            var dataBits     = settings.TryGetValue(AppSetting.WeightDataBitsKey,     out var d)  ? d  : "8";
            var stopBits     = settings.TryGetValue(AppSetting.WeightStopBitsKey,     out var sb) ? sb : "1";
            var parity       = settings.TryGetValue(AppSetting.WeightParityKey,       out var pa) ? pa : "None";
            var handshake    = settings.TryGetValue(AppSetting.WeightHandshakeKey,    out var h)  ? h  : "None";
            var stablePrefix = settings.TryGetValue(AppSetting.WeightStablePrefixKey, out var sp) ? sp : "ST,GS";
            var timeoutMs    = settings.TryGetValue(AppSetting.WeightTimeoutMsKey,    out var t)  ? t  : "500";

            // Subscribe on UI dispatcher so property changes are thread-safe
            _weightService.WeightUpdated += OnWeightUpdated;

            WeightStatusText = $"Connecting to {commPort}...";

            // Start is blocking (opens port) – keep off UI thread
            await Task.Run(() =>
                _weightService.Start(commPort, baudRate, dataBits, stopBits, parity, handshake, stablePrefix, timeoutMs));
        }
        catch (Exception ex)
        {
            WeightStatusText = $"Scale error: {ex.Message}";
        }
    }

    private void OnWeightUpdated(decimal weight, bool isStable)
    {
        var connected = _weightService.IsConnected;
        var status = _weightService.StatusText;

        if (_liveWeight == weight && _weightIsStable == isStable && _weightIsConnected == connected && _weightStatusText == status)
            return;

        // Always marshal to the UI thread on background priority
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            LiveWeight        = weight;
            WeightIsStable    = isStable;
            WeightIsConnected = connected;
            WeightStatusText  = status;
        });
    }

    private void PullWeight()
    {
        GrossWeight = LiveWeight;
    }

    private bool CanPullWeight()
    {
        return IsFormEnabled && WeightIsConnected && WeightIsStable && LiveWeight > 0;
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _weightService.WeightUpdated -= OnWeightUpdated;
        _weightService.Dispose();
    }

    // ────────────────────────────────────────────────────────────────
}
