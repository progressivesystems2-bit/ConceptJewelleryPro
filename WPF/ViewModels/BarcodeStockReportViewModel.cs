using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum BarcodeReportTab
{
    StockInHand,
    StockHeadGroupWise,
    StockSupplierWise,
    ItemWiseStock,
    ItemWiseSales,
    ItemWiseSalesReturn,
    BarcodeMovement,
    ItemMismatch,
    StockCheck,
    SearchOptions
}

public class ScannedBarcodeItem
{
    public int ScanIndex { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal NetWt { get; set; }
    public decimal GrossWt { get; set; }
    public string Purity { get; set; } = string.Empty;
    public string BoxNo { get; set; } = string.Empty;
    public bool IsInStock { get; set; } = true;
    public string Status { get; set; } = "Found";
    public DateTime ScannedAt { get; set; } = DateTime.Now;
    public BarcodeStockReportItem? MatchedItem { get; set; }
}

public sealed class BarcodeStockReportViewModel : ViewModelBase, IRefreshable, IDocumentActivationHandler
{
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private readonly IBarcodeReportRepository _repository;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IBarcodeCreationRepository? _barcodeCreationRepository;
    private readonly IBarcodePrintService? _barcodePrintService;
    private readonly IBoxMasterRepository? _boxMasterRepository;
    private readonly IStockTransferRepository? _stockTransferRepository;
    private readonly IBillRepository? _billRepository;
    private string? _savePath;
    private Dictionary<string, string>? _cachedImageIndex;
    private bool _isInitializing;
    private bool _isDirty;
    public bool IsActive { get; private set; }
    private readonly Dictionary<BarcodeReportTab, string> _loadedTabFilterKeys = new();

    private BarcodeReportTab _selectedTab = BarcodeReportTab.StockInHand;
    private string _selectedItemHead = "*";
    private string _selectedSubGroup = "*";
    private string _selectedPurity = "*";
    private string _selectedItemName = "*";
    private string _selectedSupplier = "*";
    private string _selectedBoxNo = "*";
    private DateTime? _fromDate = DateTime.Today.Month >= 4
        ? new DateTime(DateTime.Today.Year, 4, 1)
        : new DateTime(DateTime.Today.Year - 1, 4, 1);
    private DateTime? _toDate = DateTime.Today;
    private string _searchText = string.Empty;
    private string _scanBarcodeText = string.Empty;
    private decimal _updateMrpRate;
    private string? _selectedStockCheckBox = "- Select Box -";
    private BarcodeStockReportItem? _selectedStockItem;

    // Stock Check tracking
    private BarcodeStockReportItem? _selectedScannedStockItem;
    private BarcodeStockReportItem? _selectedPendingStockItem;
    private bool? _isAllScannedSelected = false;
    private bool _isUpdatingAllScannedSelected;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _highlightCtsByBarcode = new(StringComparer.OrdinalIgnoreCase);
    private IEnumerable<BarcodeStockReportItem>? _lastFilteredScannedItems;
    private IEnumerable<BarcodeStockReportItem>? _lastFilteredPendingItems;

    public void HighlightScannedItem(BarcodeStockReportItem item)
    {
        if (string.IsNullOrEmpty(item.Barcode)) return;

        if (_highlightCtsByBarcode.TryGetValue(item.Barcode, out var existingCts))
        {
            try
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }
            catch { }
        }

        var cts = new CancellationTokenSource();
        _highlightCtsByBarcode[item.Barcode] = cts;
        item.IsNewlyScanned = true;

        _ = Task.Delay(2000, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                _highlightCtsByBarcode.TryRemove(item.Barcode, out _);
                try { cts.Dispose(); } catch { }
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    item.IsNewlyScanned = false;
                });
            }
        }, TaskScheduler.Default);
    }

    // Stock Check Totals - Scanned
    private int _scannedTotalCount;
    private decimal _scannedTotalQty;
    private decimal _scannedTotalGrossWt;
    private decimal _scannedTotalStoneWt;
    private decimal _scannedTotalNetWt;
    private decimal _scannedTotalStoneValue;
    private decimal _scannedTotalMRP;
    private string _scannedRecordCountDisplay = "Record: 0 of 0";

    // Stock Check Totals - Pending
    private int _pendingTotalCount;
    private decimal _pendingTotalQty;
    private decimal _pendingTotalCost;
    private decimal _pendingTotalNetWt;
    private decimal _pendingTotalMRP;
    private decimal _pendingTotalMaking;
    private string _pendingRecordCountDisplay = "Record: 0 of 0";
    private string _stockCheckProgressDisplay = "Scanned: 0 | Pending: 0 | Total: 0";

    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    // Totals
    private int _totalCount;
    private decimal _totalQty;
    private decimal _totalGrossWt;
    private decimal _totalStoneWt;
    private decimal _totalNetWt;
    private decimal _totalStoneValue;
    private decimal _totalMaking;
    private decimal _totalAmount;

    public BarcodeStockReportViewModel(
        IBarcodeReportRepository repository,
        ISettingsRepository? settingsRepository = null,
        IBarcodeCreationRepository? barcodeCreationRepository = null,
        IBarcodePrintService? barcodePrintService = null,
        IBoxMasterRepository? boxMasterRepository = null,
        IStockTransferRepository? stockTransferRepository = null,
        IBillRepository? billRepository = null)
    {
        _repository = repository;
        _settingsRepository = settingsRepository;
        _barcodeCreationRepository = barcodeCreationRepository;
        _barcodePrintService = barcodePrintService;
        _boxMasterRepository = boxMasterRepository;
        _stockTransferRepository = stockTransferRepository;
        _billRepository = billRepository;

        RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);
        ResetFiltersCommand = new AsyncRelayCommand(ResetFiltersAsync);
        PrintCommand = new RelayCommand(PrintReport);
        PrintCapacityCommand = new RelayCommand(PrintCapacityReport);
        ExportToExcelCommand = new RelayCommand(ExportToCsv);
        SelectTabCommand = new RelayCommand(param =>
        {
            BarcodeReportTab? targetTab = param switch
            {
                BarcodeReportTab t => t,
                string s when Enum.TryParse<BarcodeReportTab>(s, true, out var pt) => pt,
                _ => null
            };

            if (targetTab.HasValue)
            {
                if (SelectedTab == targetTab.Value)
                {
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
                else
                {
                    SelectedTab = targetTab.Value;
                }
            }
        });
        ScanBarcodeCommand = new RelayCommand(ProcessScannedBarcode);
        RemoveScannedBarcodeCommand = new RelayCommand(RemoveScannedBarcode);
        ClearScannedBarcodesCommand = new RelayCommand(ClearScannedBarcodes);
        RemoveScannedItemCommand = new RelayCommand(param => RemoveScannedItem(param as BarcodeStockReportItem ?? SelectedScannedStockItem));
        RemoveAllScannedCommand = new RelayCommand(RemoveAllScannedItems);
        ExportNotFoundCommand = new RelayCommand(ExportNotFoundToCsv);
        UpdateMrpAndPrintCommand = new AsyncRelayCommand(UpdateMrpAndPrintAsync);
        AssignBoxCommand = new RelayCommand(_ => AssignSelectedBoxToItem());
        AdjustStockCommand = new AsyncRelayCommand(AdjustStockAsync);
        RefreshFiltersCommand = new AsyncRelayCommand(InitializeAsync);
        CopyPhoneCommand = new RelayCommand(param => CopyPhone(param?.ToString()));
        CopyTextCommand = new RelayCommand(param => CopyText(param?.ToString()));
        OpenCustomerHistoryCommand = new RelayCommand(param => OpenCustomerHistory(param as BarcodeStockReportItem ?? SelectedStockItem));

        ScannedStockItems.CollectionChanged += ScannedStockItems_CollectionChanged;
        BarcodeEvents.BarcodesChanged += OnBarcodesChanged;

        _ = InitializeAsync();
    }

    public string Title => "Barcode Stock Reports";

    public ObservableRangeCollection<string> ItemHeads { get; } = ["*"];
    public ObservableRangeCollection<string> SubGroups { get; } = ["*"];
    public ObservableRangeCollection<string> Purities { get; } = ["*"];
    public ObservableRangeCollection<string> ItemNames { get; } = ["*"];
    public ObservableRangeCollection<string> Suppliers { get; } = ["*"];
    public ObservableRangeCollection<string> BoxNos { get; } = ["*"];

    public ObservableRangeCollection<BarcodeStockReportItem> Items { get; } = [];
    public ObservableRangeCollection<BarcodeGroupSummary> GroupSummaries { get; } = [];
    public ObservableRangeCollection<BarcodeMovementItem> MovementItems { get; } = [];
    public ObservableRangeCollection<string> ScannedBarcodes { get; } = [];

    // Stock Check Collections
    public ObservableRangeCollection<BarcodeStockReportItem> ScannedStockItems { get; } = [];
    public ObservableRangeCollection<BarcodeStockReportItem> PendingStockItems { get; } = [];
    public ObservableRangeCollection<BarcodeStockReportItem> NotFoundItems { get; } = [];
    public ObservableRangeCollection<ScannedBarcodeItem> ScannedItems { get; } = [];
    public ObservableRangeCollection<string> StockCheckBoxes { get; } = ["- Select Box -"];

    // 3-Tier HeadGroup Drilldown Collections
    public ObservableRangeCollection<BarcodeGroupSummary> HeadGroupWiseList { get; } = [];
    public ObservableRangeCollection<BarcodeGroupSummary> SubGroupWiseList { get; } = [];
    public ObservableRangeCollection<BarcodeStockReportItem> HeadGroupBarcodeList { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ResetFiltersCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand PrintCapacityCommand { get; }
    public RelayCommand ExportToExcelCommand { get; }
    public RelayCommand SelectTabCommand { get; }
    public RelayCommand ScanBarcodeCommand { get; }
    public RelayCommand RemoveScannedBarcodeCommand { get; }
    public RelayCommand ClearScannedBarcodesCommand { get; }
    public RelayCommand RemoveScannedItemCommand { get; }
    public RelayCommand RemoveAllScannedCommand { get; }
    public RelayCommand ExportNotFoundCommand { get; }
    public AsyncRelayCommand UpdateMrpAndPrintCommand { get; }
    public RelayCommand AssignBoxCommand { get; }
    public AsyncRelayCommand AdjustStockCommand { get; }
    public AsyncRelayCommand RefreshFiltersCommand { get; }
    public ICommand RefreshLookupsCommand => RefreshFiltersCommand;
    public RelayCommand CopyPhoneCommand { get; }
    public RelayCommand CopyTextCommand { get; }
    public RelayCommand OpenCustomerHistoryCommand { get; }

    public async Task RefreshAsync() => await RefreshAllAsync();

    public async Task OnActivatedAsync()
    {
        IsActive = true;
        if (_isDirty)
        {
            _isDirty = false;
            await RefreshAllAsync();
        }
    }

    public Task OnDeactivatedAsync()
    {
        IsActive = false;
        return Task.CompletedTask;
    }

    private void OnBarcodesChanged()
    {
        InvalidateFilterCache();
        _repository.InvalidateMasterCache();
        _isDirty = true;

        if (IsActive)
        {
            _isDirty = false;
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(async () =>
            {
                await RefreshAllAsync();
            });
        }
    }

    public BarcodeReportTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsStockInHandActive));
                OnPropertyChanged(nameof(IsHeadGroupWiseActive));
                OnPropertyChanged(nameof(IsSupplierWiseActive));
                OnPropertyChanged(nameof(IsItemWiseStockActive));
                OnPropertyChanged(nameof(IsSalesActive));
                OnPropertyChanged(nameof(IsSalesReturnActive));
                OnPropertyChanged(nameof(IsMovementActive));
                OnPropertyChanged(nameof(IsMismatchActive));
                OnPropertyChanged(nameof(IsStockCheckActive));
                OnPropertyChanged(nameof(IsSearchOptionsActive));
                OnPropertyChanged(nameof(IsDetailedGridVisible));
                OnPropertyChanged(nameof(IsStockCheckVisible));
                OnPropertyChanged(nameof(IsGroupGridVisible));
                OnPropertyChanged(nameof(IsMovementGridVisible));
                OnPropertyChanged(nameof(IsHeadGroupWiseVisible));
                OnPropertyChanged(nameof(IsDateRangeTab));
                OnPropertyChanged(nameof(DatePickerLabel));

                if (!_isInitializing)
                {
                    var reload = _isDirty;
                    if (_isDirty)
                    {
                        _isDirty = false;
                        InvalidateFilterCache();
                        _repository.InvalidateMasterCache();
                    }
                    _ = LoadCurrentTabReportAsync(forceReload: reload);
                }
            }
        }
    }

    public bool IsStockInHandActive => SelectedTab == BarcodeReportTab.StockInHand;
    public bool IsHeadGroupWiseActive => SelectedTab == BarcodeReportTab.StockHeadGroupWise;
    public bool IsSupplierWiseActive => SelectedTab == BarcodeReportTab.StockSupplierWise;
    public bool IsItemWiseStockActive => SelectedTab == BarcodeReportTab.ItemWiseStock;
    public bool IsSalesActive => SelectedTab == BarcodeReportTab.ItemWiseSales;
    public bool IsSalesReturnActive => SelectedTab == BarcodeReportTab.ItemWiseSalesReturn;
    public bool IsMovementActive => SelectedTab == BarcodeReportTab.BarcodeMovement;
    public bool IsMismatchActive => SelectedTab == BarcodeReportTab.ItemMismatch;
    public bool IsStockCheckActive => SelectedTab == BarcodeReportTab.StockCheck;
    public bool IsSearchOptionsActive => SelectedTab == BarcodeReportTab.SearchOptions;

    public bool IsDetailedGridVisible => SelectedTab is BarcodeReportTab.StockInHand or BarcodeReportTab.ItemWiseSales or BarcodeReportTab.ItemWiseSalesReturn or BarcodeReportTab.ItemMismatch or BarcodeReportTab.SearchOptions;
    public bool IsStockCheckVisible => SelectedTab == BarcodeReportTab.StockCheck;
    public bool IsGroupGridVisible => SelectedTab is BarcodeReportTab.StockSupplierWise or BarcodeReportTab.ItemWiseStock;
    public bool IsMovementGridVisible => SelectedTab == BarcodeReportTab.BarcodeMovement;
    public bool IsHeadGroupWiseVisible => SelectedTab == BarcodeReportTab.StockHeadGroupWise;

    public bool IsDateRangeTab => SelectedTab is BarcodeReportTab.ItemWiseSales or BarcodeReportTab.ItemWiseSalesReturn;
    public string DatePickerLabel => IsDateRangeTab ? "To Date:" : "As of Date:";

    private string GetCurrentFilterKey()
    {
        return $"{SelectedItemHead}|{SelectedSubGroup}|{SelectedPurity}|{SelectedItemName}|{SelectedSupplier}|{SelectedBoxNo}|{FromDate:yyyyMMdd}|{ToDate:yyyyMMdd}|{SearchText?.Trim()}";
    }

    private void InvalidateFilterCache()
    {
        _loadedTabFilterKeys.Clear();
    }

    public string SelectedItemHead
    {
        get => _selectedItemHead;
        set
        {
            if (SetProperty(ref _selectedItemHead, value))
            {
                if (!_isInitializing)
                {
                    InvalidateFilterCache();
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
            }
        }
    }

    public string SelectedSubGroup
    {
        get => _selectedSubGroup;
        set
        {
            if (SetProperty(ref _selectedSubGroup, value))
            {
                if (!_isInitializing)
                {
                    InvalidateFilterCache();
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
            }
        }
    }

    public string SelectedPurity
    {
        get => _selectedPurity;
        set
        {
            if (SetProperty(ref _selectedPurity, value))
            {
                if (!_isInitializing)
                {
                    InvalidateFilterCache();
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
            }
        }
    }

    public string SelectedItemName
    {
        get => _selectedItemName;
        set
        {
            if (SetProperty(ref _selectedItemName, value))
            {
                if (!_isInitializing)
                {
                    InvalidateFilterCache();
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
            }
        }
    }

    public string SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (SetProperty(ref _selectedSupplier, value))
            {
                if (!_isInitializing)
                {
                    InvalidateFilterCache();
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
            }
        }
    }

    public string SelectedBoxNo
    {
        get => _selectedBoxNo;
        set
        {
            if (SetProperty(ref _selectedBoxNo, value))
            {
                if (!_isInitializing)
                {
                    InvalidateFilterCache();
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
            }
        }
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                if (!_isInitializing)
                {
                    InvalidateFilterCache();
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                if (!_isInitializing)
                {
                    InvalidateFilterCache();
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                if (!_isInitializing)
                {
                    InvalidateFilterCache();
                    _ = LoadCurrentTabReportAsync(forceReload: true);
                }
            }
        }
    }

    public string ScanBarcodeText
    {
        get => _scanBarcodeText;
        set => SetProperty(ref _scanBarcodeText, value);
    }

    public BarcodeStockReportItem? SelectedScannedStockItem
    {
        get => _selectedScannedStockItem;
        set => SetProperty(ref _selectedScannedStockItem, value);
    }

    public BarcodeStockReportItem? SelectedPendingStockItem
    {
        get => _selectedPendingStockItem;
        set => SetProperty(ref _selectedPendingStockItem, value);
    }

    public decimal UpdateMrpRate
    {
        get => _updateMrpRate;
        set => SetProperty(ref _updateMrpRate, value);
    }

    public string? SelectedStockCheckBox
    {
        get => _selectedStockCheckBox;
        set => SetProperty(ref _selectedStockCheckBox, value);
    }

    public bool? IsAllScannedSelected
    {
        get => _isAllScannedSelected;
        set
        {
            if (SetProperty(ref _isAllScannedSelected, value))
            {
                if (!_isUpdatingAllScannedSelected && value.HasValue)
                {
                    foreach (var item in ScannedStockItems)
                    {
                        item.IsSelected = value.Value;
                    }
                    OnPropertyChanged(nameof(SelectedScannedCount));
                    OnPropertyChanged(nameof(HasScannedSelection));
                }
            }
        }
    }

    public int SelectedScannedCount => ScannedStockItems.Count(x => x.IsSelected);
    public bool HasScannedSelection => SelectedScannedCount > 0;

    // Scanned Totals
    public int ScannedTotalCount
    {
        get => _scannedTotalCount;
        private set => SetProperty(ref _scannedTotalCount, value);
    }

    public decimal ScannedTotalQty
    {
        get => _scannedTotalQty;
        private set => SetProperty(ref _scannedTotalQty, value);
    }

    public decimal ScannedTotalGrossWt
    {
        get => _scannedTotalGrossWt;
        private set => SetProperty(ref _scannedTotalGrossWt, value);
    }

    public decimal ScannedTotalStoneWt
    {
        get => _scannedTotalStoneWt;
        private set => SetProperty(ref _scannedTotalStoneWt, value);
    }

    public decimal ScannedTotalNetWt
    {
        get => _scannedTotalNetWt;
        private set => SetProperty(ref _scannedTotalNetWt, value);
    }

    public decimal ScannedTotalStoneValue
    {
        get => _scannedTotalStoneValue;
        private set => SetProperty(ref _scannedTotalStoneValue, value);
    }

    public decimal ScannedTotalMRP
    {
        get => _scannedTotalMRP;
        private set => SetProperty(ref _scannedTotalMRP, value);
    }

    public string ScannedRecordCountDisplay
    {
        get => _scannedRecordCountDisplay;
        private set => SetProperty(ref _scannedRecordCountDisplay, value);
    }

    // Pending Totals
    public int PendingTotalCount
    {
        get => _pendingTotalCount;
        private set => SetProperty(ref _pendingTotalCount, value);
    }

    public decimal PendingTotalQty
    {
        get => _pendingTotalQty;
        private set => SetProperty(ref _pendingTotalQty, value);
    }

    public decimal PendingTotalCost
    {
        get => _pendingTotalCost;
        private set => SetProperty(ref _pendingTotalCost, value);
    }

    public decimal PendingTotalNetWt
    {
        get => _pendingTotalNetWt;
        private set => SetProperty(ref _pendingTotalNetWt, value);
    }

    public decimal PendingTotalMRP
    {
        get => _pendingTotalMRP;
        private set => SetProperty(ref _pendingTotalMRP, value);
    }

    public decimal PendingTotalMaking
    {
        get => _pendingTotalMaking;
        private set => SetProperty(ref _pendingTotalMaking, value);
    }

    public string PendingRecordCountDisplay
    {
        get => _pendingRecordCountDisplay;
        private set => SetProperty(ref _pendingRecordCountDisplay, value);
    }

    public string StockCheckProgressDisplay
    {
        get => _stockCheckProgressDisplay;
        private set => SetProperty(ref _stockCheckProgressDisplay, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public decimal TotalQty
    {
        get => _totalQty;
        private set => SetProperty(ref _totalQty, value);
    }

    public decimal TotalGrossWt
    {
        get => _totalGrossWt;
        private set => SetProperty(ref _totalGrossWt, value);
    }

    public decimal TotalStoneWt
    {
        get => _totalStoneWt;
        private set => SetProperty(ref _totalStoneWt, value);
    }

    public decimal TotalNetWt
    {
        get => _totalNetWt;
        private set => SetProperty(ref _totalNetWt, value);
    }

    public decimal TotalStoneValue
    {
        get => _totalStoneValue;
        private set => SetProperty(ref _totalStoneValue, value);
    }

    public decimal TotalMaking
    {
        get => _totalMaking;
        private set => SetProperty(ref _totalMaking, value);
    }

    public decimal TotalAmount
    {
        get => _totalAmount;
        private set => SetProperty(ref _totalAmount, value);
    }

    // 3-Tier HeadGroup Drilldown Selection & Totals
    private BarcodeGroupSummary? _selectedHeadGroupSummary;
    public BarcodeGroupSummary? SelectedHeadGroupSummary
    {
        get => _selectedHeadGroupSummary;
        set
        {
            if (SetProperty(ref _selectedHeadGroupSummary, value))
            {
                if (value != null && !string.IsNullOrWhiteSpace(value.HeadGroup))
                {
                    _ = LoadSubGroupSummariesAsync(value.HeadGroup);
                }
                else
                {
                    SubGroupWiseList.Clear();
                    HeadGroupBarcodeList.Clear();
                    RecalculateSubGroupTotals([]);
                    RecalculateBarcodeDetailTotals([]);
                }
            }
        }
    }

    private BarcodeGroupSummary? _selectedSubGroupSummary;
    public BarcodeGroupSummary? SelectedSubGroupSummary
    {
        get => _selectedSubGroupSummary;
        set
        {
            if (SetProperty(ref _selectedSubGroupSummary, value))
            {
                if (value != null && !string.IsNullOrWhiteSpace(value.SubGroup))
                {
                    _ = LoadHeadGroupBarcodeDetailsAsync(SelectedHeadGroupSummary?.HeadGroup ?? value.HeadGroup, value.SubGroup);
                }
                else
                {
                    HeadGroupBarcodeList.Clear();
                    RecalculateBarcodeDetailTotals([]);
                }
            }
        }
    }

    // Level 1 Totals
    private decimal _headGroupTotalQty;
    public decimal HeadGroupTotalQty { get => _headGroupTotalQty; private set => SetProperty(ref _headGroupTotalQty, value); }
    private decimal _headGroupTotalGrossWt;
    public decimal HeadGroupTotalGrossWt { get => _headGroupTotalGrossWt; private set => SetProperty(ref _headGroupTotalGrossWt, value); }
    private decimal _headGroupTotalStoneWt;
    public decimal HeadGroupTotalStoneWt { get => _headGroupTotalStoneWt; private set => SetProperty(ref _headGroupTotalStoneWt, value); }
    private decimal _headGroupTotalNetWt;
    public decimal HeadGroupTotalNetWt { get => _headGroupTotalNetWt; private set => SetProperty(ref _headGroupTotalNetWt, value); }
    private decimal _headGroupTotalStoneValue;
    public decimal HeadGroupTotalStoneValue { get => _headGroupTotalStoneValue; private set => SetProperty(ref _headGroupTotalStoneValue, value); }
    private decimal _headGroupTotalMaking;
    public decimal HeadGroupTotalMaking { get => _headGroupTotalMaking; private set => SetProperty(ref _headGroupTotalMaking, value); }

    // Level 2 Totals
    private string _subGroupCountDisplay = "Total Sub Group = 0";
    public string SubGroupCountDisplay { get => _subGroupCountDisplay; private set => SetProperty(ref _subGroupCountDisplay, value); }
    private decimal _subGroupTotalQty;
    public decimal SubGroupTotalQty { get => _subGroupTotalQty; private set => SetProperty(ref _subGroupTotalQty, value); }
    private decimal _subGroupTotalGrossWt;
    public decimal SubGroupTotalGrossWt { get => _subGroupTotalGrossWt; private set => SetProperty(ref _subGroupTotalGrossWt, value); }
    private decimal _subGroupTotalStoneWt;
    public decimal SubGroupTotalStoneWt { get => _subGroupTotalStoneWt; private set => SetProperty(ref _subGroupTotalStoneWt, value); }
    private decimal _subGroupTotalNetWt;
    public decimal SubGroupTotalNetWt { get => _subGroupTotalNetWt; private set => SetProperty(ref _subGroupTotalNetWt, value); }
    private decimal _subGroupTotalStoneValue;
    public decimal SubGroupTotalStoneValue { get => _subGroupTotalStoneValue; private set => SetProperty(ref _subGroupTotalStoneValue, value); }
    private decimal _subGroupTotalMaking;
    public decimal SubGroupTotalMaking { get => _subGroupTotalMaking; private set => SetProperty(ref _subGroupTotalMaking, value); }

    // Level 3 Totals
    private string _barcodeCountDisplay = "Total Barcode = 0";
    public string BarcodeCountDisplay { get => _barcodeCountDisplay; private set => SetProperty(ref _barcodeCountDisplay, value); }
    private decimal _barcodeTotalQty;
    public decimal BarcodeTotalQty { get => _barcodeTotalQty; private set => SetProperty(ref _barcodeTotalQty, value); }
    private decimal _barcodeTotalGrossWt;
    public decimal BarcodeTotalGrossWt { get => _barcodeTotalGrossWt; private set => SetProperty(ref _barcodeTotalGrossWt, value); }
    private decimal _barcodeTotalStoneWt;
    public decimal BarcodeTotalStoneWt { get => _barcodeTotalStoneWt; private set => SetProperty(ref _barcodeTotalStoneWt, value); }
    private decimal _barcodeTotalNetWt;
    public decimal BarcodeTotalNetWt { get => _barcodeTotalNetWt; private set => SetProperty(ref _barcodeTotalNetWt, value); }
    private decimal _barcodeTotalStoneValue;
    public decimal BarcodeTotalStoneValue { get => _barcodeTotalStoneValue; private set => SetProperty(ref _barcodeTotalStoneValue, value); }
    private decimal _barcodeTotalMaking;
    public decimal BarcodeTotalMaking { get => _barcodeTotalMaking; private set => SetProperty(ref _barcodeTotalMaking, value); }

    public BarcodeStockReportItem? SelectedStockItem
    {
        get => _selectedStockItem;
        set => SetProperty(ref _selectedStockItem, value);
    }

    public string RecordCountDisplay => $"Record: 1 of {TotalCount}";

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

    public async Task RefreshAllAsync()
    {
        _isDirty = false;
        InvalidateFilterCache();
        _repository.InvalidateMasterCache();
        await RefreshMastersAsync();
        // If ToDate was Today from previous session, keep it current with today
        if (ToDate.HasValue && ToDate.Value.Date < DateTime.Today)
        {
            ToDate = DateTime.Today;
        }
        await LoadCurrentTabReportAsync(forceReload: true);
        ShowToast("Barcode stock report refreshed.", ToastType.Info);
    }

    public async Task ResetFiltersAsync()
    {
        _isInitializing = true;
        try
        {
            _selectedItemHead = "*";
            _selectedSubGroup = "*";
            _selectedPurity = "*";
            _selectedItemName = "*";
            _selectedSupplier = "*";
            _selectedBoxNo = "*";
            _searchText = string.Empty;
            _fromDate = DateTime.Today.Month >= 4
                ? new DateTime(DateTime.Today.Year, 4, 1)
                : new DateTime(DateTime.Today.Year - 1, 4, 1);
            _toDate = DateTime.Today;

            OnPropertyChanged(nameof(SelectedItemHead));
            OnPropertyChanged(nameof(SelectedSubGroup));
            OnPropertyChanged(nameof(SelectedPurity));
            OnPropertyChanged(nameof(SelectedItemName));
            OnPropertyChanged(nameof(SelectedSupplier));
            OnPropertyChanged(nameof(SelectedBoxNo));
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(FromDate));
            OnPropertyChanged(nameof(ToDate));
        }
        finally
        {
            _isInitializing = false;
        }

        InvalidateFilterCache();
        _repository.InvalidateMasterCache();
        await RefreshMastersAsync();
        await LoadCurrentTabReportAsync(forceReload: true);
        ShowToast("Filters reset to show all current stock.", ToastType.Info);
    }

    public async Task RefreshMastersAsync()
    {
        try
        {
            var (heads, subs, purs, items, supps, boxes) = await Task.Run(() => _repository.GetFilterMastersAsync());

            var prevHead = SelectedItemHead;
            var prevSub = SelectedSubGroup;
            var prevPurity = SelectedPurity;
            var prevItem = SelectedItemName;
            var prevSupp = SelectedSupplier;
            var prevBox = SelectedBoxNo;

            ItemHeads.ReplaceRange(heads);
            SubGroups.ReplaceRange(subs);
            Purities.ReplaceRange(purs);
            ItemNames.ReplaceRange(items);
            Suppliers.ReplaceRange(supps);
            BoxNos.ReplaceRange(boxes);

            if (heads.Contains(prevHead)) SelectedItemHead = prevHead;
            else SelectedItemHead = "*";

            if (subs.Contains(prevSub)) SelectedSubGroup = prevSub;
            else SelectedSubGroup = "*";

            if (purs.Contains(prevPurity)) SelectedPurity = prevPurity;
            else SelectedPurity = "*";

            if (items.Contains(prevItem)) SelectedItemName = prevItem;
            else SelectedItemName = "*";

            if (supps.Contains(prevSupp)) SelectedSupplier = prevSupp;
            else SelectedSupplier = "*";

            if (boxes.Contains(prevBox)) SelectedBoxNo = prevBox;
            else SelectedBoxNo = "*";

            var stockBoxes = new List<string> { "- Select Box -" };
            if (_boxMasterRepository != null)
            {
                try
                {
                    var allBoxes = await _boxMasterRepository.GetAllAsync();
                    foreach (var b in allBoxes.Where(x => !string.IsNullOrWhiteSpace(x.BoxNo)).OrderBy(x => x.BoxNo))
                    {
                        if (!stockBoxes.Contains(b.BoxNo, StringComparer.OrdinalIgnoreCase))
                        {
                            stockBoxes.Add(b.BoxNo);
                        }
                    }
                }
                catch { }
            }
            foreach (var b in boxes)
            {
                if (!string.IsNullOrWhiteSpace(b) && b != "*" && !stockBoxes.Contains(b, StringComparer.OrdinalIgnoreCase))
                {
                    stockBoxes.Add(b);
                }
            }
            var prevStockBox = SelectedStockCheckBox;
            StockCheckBoxes.ReplaceRange(stockBoxes);
            if (prevStockBox != null && stockBoxes.Contains(prevStockBox))
            {
                SelectedStockCheckBox = prevStockBox;
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to refresh filters: {ex.Message}", ToastType.Error);
        }
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        _isInitializing = true;
        try
        {
            if (_settingsRepository != null && string.IsNullOrEmpty(_savePath))
            {
                _savePath = await Task.Run(() => _settingsRepository.GetValueAsync(AppSetting.SavePathKey));
            }

            _repository.InvalidateMasterCache();
            await RefreshMastersAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to initialize report filters: {ex.Message}", ToastType.Error);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
        }

        await LoadCurrentTabReportAsync(forceReload: true);
    }

    public async Task LoadCurrentTabReportAsync(bool forceReload = false)
    {
        var currentTab = SelectedTab;
        var filterKey = GetCurrentFilterKey();

        if (!forceReload && _loadedTabFilterKeys.TryGetValue(currentTab, out var loadedKey) && loadedKey == filterKey)
        {
            // Already loaded with identical parameters! Zero database queries, instant tab switch!
            return;
        }

        IsBusy = true;
        try
        {
            var filter = new BarcodeStockReportFilter
            {
                ItemHead = SelectedItemHead,
                SubGroup = SelectedSubGroup,
                Purity = SelectedPurity,
                ItemName = SelectedItemName,
                SupplierName = SelectedSupplier,
                BoxNo = SelectedBoxNo == "*" ? null : SelectedBoxNo,
                FromDate = FromDate,
                ToDate = ToDate,
                SearchText = SearchText
            };

            var savePath = _savePath;
            var searchText = SearchText;

            switch (currentTab)
            {
                case BarcodeReportTab.StockInHand or BarcodeReportTab.SearchOptions:
                {
                    var stock = await Task.Run(async () =>
                    {
                        var list = await _repository.GetStockInHandAsync(filter);
                        var imageIndex = GetBarcodeImageIndex(savePath);
                        foreach (var s in list)
                        {
                            s.ImagePath = ResolveBarcodeImagePath(s.Barcode, s.Photo, imageIndex);
                        }
                        return list;
                    });
                    Items.ReplaceRange(stock);
                    RecalculateStockTotals(stock);
                    break;
                }

                case BarcodeReportTab.StockHeadGroupWise:
                    await LoadHeadGroupSummariesAsync(filter);
                    break;

                case BarcodeReportTab.StockSupplierWise:
                {
                    var supplierGroups = await Task.Run(() => _repository.GetStockSupplierWiseAsync(filter));
                    GroupSummaries.ReplaceRange(supplierGroups);
                    RecalculateGroupTotals(supplierGroups);
                    break;
                }

                case BarcodeReportTab.ItemWiseStock:
                {
                    var itemGroups = await Task.Run(() => _repository.GetItemWiseStockAsync(filter));
                    GroupSummaries.ReplaceRange(itemGroups);
                    RecalculateGroupTotals(itemGroups);
                    break;
                }

                case BarcodeReportTab.ItemWiseSales:
                {
                    var sales = await Task.Run(async () =>
                    {
                        var list = await _repository.GetItemWiseSalesAsync(filter);
                        var imageIndex = GetBarcodeImageIndex(savePath);
                        foreach (var s in list)
                        {
                            s.ImagePath = ResolveBarcodeImagePath(s.Barcode, s.Photo, imageIndex);
                        }
                        return list;
                    });
                    Items.ReplaceRange(sales);
                    RecalculateStockTotals(sales);
                    break;
                }

                case BarcodeReportTab.ItemWiseSalesReturn:
                {
                    var returns = await Task.Run(async () =>
                    {
                        var list = await _repository.GetItemWiseSalesReturnAsync(filter);
                        var imageIndex = GetBarcodeImageIndex(savePath);
                        foreach (var r in list)
                        {
                            r.ImagePath = ResolveBarcodeImagePath(r.Barcode, r.Photo, imageIndex);
                        }
                        return list;
                    });
                    Items.ReplaceRange(returns);
                    RecalculateStockTotals(returns);
                    break;
                }

                case BarcodeReportTab.BarcodeMovement:
                {
                    var movements = await Task.Run(() => _repository.GetBarcodeMovementAsync(searchText, filter));
                    MovementItems.ReplaceRange(movements);
                    RecalculateMovementTotals(movements);
                    break;
                }

                case BarcodeReportTab.ItemMismatch:
                {
                    var mismatches = await Task.Run(async () =>
                    {
                        var list = await _repository.GetItemMismatchesAsync(filter);
                        var imageIndex = GetBarcodeImageIndex(savePath);
                        foreach (var mm in list)
                        {
                            mm.ImagePath = ResolveBarcodeImagePath(mm.Barcode, mm.Photo, imageIndex);
                        }
                        return list;
                    });
                    Items.ReplaceRange(mismatches);
                    RecalculateStockTotals(mismatches);
                    break;
                }

                case BarcodeReportTab.StockCheck:
                {
                    var allStock = await Task.Run(async () =>
                    {
                        var list = await _repository.GetStockInHandAsync(filter);
                        var imageIndex = GetBarcodeImageIndex(savePath);
                        foreach (var s in list)
                        {
                            s.ImagePath = ResolveBarcodeImagePath(s.Barcode, s.Photo, imageIndex);
                        }
                        return list;
                    });

                    // Fetch persisted scanned items from database so they persist across sessions
                    var persistedScanned = await Task.Run(async () =>
                    {
                        var list = await _repository.GetScannedStockCheckItemsAsync();
                        var imageIndex = GetBarcodeImageIndex(savePath);
                        foreach (var s in list)
                        {
                            s.ImagePath = ResolveBarcodeImagePath(s.Barcode, s.Photo, imageIndex);
                        }
                        return list;
                    });

                    ScannedStockItems.ReplaceRange(persistedScanned);

                    // Retain existing scanned barcodes
                    var scannedCodes = new HashSet<string>(ScannedStockItems.Select(x => x.Barcode), StringComparer.OrdinalIgnoreCase);

                    // Pending are all items matching the active filter that have not yet been scanned
                    var pendingList = allStock.Where(x => !scannedCodes.Contains(x.Barcode)).ToList();
                    PendingStockItems.ReplaceRange(pendingList);

                    RecalculateStockCheckTotals();

                    if (PendingStockItems.Count > 0 && SelectedPendingStockItem == null)
                    {
                        SelectedPendingStockItem = PendingStockItems[0];
                    }
                    if (ScannedStockItems.Count > 0 && SelectedScannedStockItem == null)
                    {
                        SelectedScannedStockItem = ScannedStockItems[0];
                    }
                    break;
                }
            }

            _loadedTabFilterKeys[currentTab] = filterKey;

            if (Items.Count > 0 && SelectedStockItem == null)
            {
                SelectedStockItem = Items[0];
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load report: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Dictionary<string, string> GetBarcodeImageIndex(string? savePath = null)
    {
        if (_cachedImageIndex is not null) return _cachedImageIndex;
        _cachedImageIndex = BuildBarcodeImageIndex(savePath ?? _savePath);
        return _cachedImageIndex;
    }

    private static Dictionary<string, string> BuildBarcodeImageIndex(string? savePath)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(savePath)) return index;

        var barcodeDir = Path.Combine(savePath, "barcode");
        if (!Directory.Exists(barcodeDir))
        {
            barcodeDir = Path.Combine(savePath, "barcodes");
            if (!Directory.Exists(barcodeDir))
            {
                barcodeDir = savePath;
            }
        }

        if (!Directory.Exists(barcodeDir)) return index;

        try
        {
            foreach (var file in Directory.EnumerateFiles(barcodeDir))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                if (!index.ContainsKey(nameWithoutExt))
                {
                    index[nameWithoutExt] = file;
                }

                var nameWithExt = Path.GetFileName(file);
                if (!index.ContainsKey(nameWithExt))
                {
                    index[nameWithExt] = file;
                }
            }
        }
        catch
        {
            // Ignore directory read exceptions
        }

        return index;
    }

    private static string? ResolveBarcodeImagePath(string barcode, string? photo, Dictionary<string, string> imageIndex)
    {
        if (imageIndex.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(photo))
        {
            var pName = Path.GetFileName(photo);
            if (imageIndex.TryGetValue(pName, out var foundPhoto)) return foundPhoto;
            var pNoExt = Path.GetFileNameWithoutExtension(photo);
            if (imageIndex.TryGetValue(pNoExt, out var foundPhotoNoExt)) return foundPhotoNoExt;
        }

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var code = barcode.Trim();
            if (imageIndex.TryGetValue(code, out var foundBarcode)) return foundBarcode;
        }

        return null;
    }

    private static bool MatchBarcode(string? barcode, string inputCode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(inputCode)) return false;
        var b = barcode.Trim();
        var c = inputCode.Trim();
        if (string.Equals(b, c, StringComparison.OrdinalIgnoreCase)) return true;
        if (int.TryParse(c, out var n))
        {
            if (string.Equals(b, n.ToString("D6"), StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(b, n.ToString(), StringComparison.OrdinalIgnoreCase)) return true;
        }
        if (int.TryParse(b, out var nb))
        {
            if (string.Equals(nb.ToString(), c, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(nb.ToString("D6"), c, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private async void ProcessScannedBarcode(object? parameter = null)
    {
        var rawCode = parameter as string;
        var code = (string.IsNullOrWhiteSpace(rawCode) ? ScanBarcodeText : rawCode)?.Trim();
        ScanBarcodeText = string.Empty;
        if (string.IsNullOrWhiteSpace(code)) return;

        if (SelectedTab == BarcodeReportTab.StockCheck)
        {
            // Check if already in ScannedStockItems
            var alreadyScanned = ScannedStockItems.FirstOrDefault(x => MatchBarcode(x.Barcode, code));
            if (alreadyScanned != null)
            {
                SelectedScannedStockItem = alreadyScanned;
                HighlightScannedItem(alreadyScanned);
                ShowToast($"Barcode {alreadyScanned.Barcode} has already been scanned!", ToastType.Warning);
                return;
            }

            // Check if in PendingStockItems
            var pendingItem = PendingStockItems.FirstOrDefault(x => MatchBarcode(x.Barcode, code));
            if (pendingItem != null)
            {
                PendingStockItems.Remove(pendingItem);
                pendingItem.LastScan = DateTime.Now;
                AssignBoxIfSelected(pendingItem);
                ScannedStockItems.Insert(0, pendingItem);
                SelectedScannedStockItem = pendingItem;
                HighlightScannedItem(pendingItem);
                RecalculateStockCheckTotals(useCachedFilter: true);
                _ = _repository.SaveScannedStockCheckItemAsync(pendingItem);
                var boxInfo = !string.IsNullOrWhiteSpace(pendingItem.BoxNo) ? $" [Box: {pendingItem.BoxNo}]" : "";
                ShowToast($"Scanned: {pendingItem.Barcode} - {pendingItem.ItemName}{boxInfo}", ToastType.Success);
                return;
            }

            // Fast direct indexed lookup from database (outside active filter)
            try
            {
                var match = await _repository.LookupBarcodeAsync(code);
                if (match != null)
                {
                    var savePath = _savePath;
                    var imageIndex = GetBarcodeImageIndex(savePath);
                    match.ImagePath = ResolveBarcodeImagePath(match.Barcode, match.Photo, imageIndex);
                    match.LastScan = DateTime.Now;
                    AssignBoxIfSelected(match);

                    ScannedStockItems.Insert(0, match);
                    SelectedScannedStockItem = match;
                    HighlightScannedItem(match);
                    RecalculateStockCheckTotals(useCachedFilter: true);
                    _ = _repository.SaveScannedStockCheckItemAsync(match);
                    var boxInfo = !string.IsNullOrWhiteSpace(match.BoxNo) ? $" [Box: {match.BoxNo}]" : "";
                    ShowToast($"Scanned: {match.Barcode} ({match.ItemName}){boxInfo}", ToastType.Success);
                }
                else
                {
                    ShowToast($"Barcode {code} not found in stock!", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Error scanning barcode: {ex.Message}", ToastType.Error);
            }
            return;
        }

        if (!ScannedBarcodes.Contains(code, StringComparer.OrdinalIgnoreCase))
        {
            ScannedBarcodes.Add(code);
            ShowToast($"Scanned: {code} ({ScannedBarcodes.Count} scanned)", ToastType.Success);
        }
        InvalidateFilterCache();
        _ = LoadCurrentTabReportAsync(forceReload: true);
    }

    private void RemoveScannedItem(BarcodeStockReportItem? item)
    {
        if (item == null) return;
        if (!string.IsNullOrEmpty(item.Barcode) && _highlightCtsByBarcode.TryRemove(item.Barcode, out var cts))
        {
            try { cts.Cancel(); cts.Dispose(); } catch { }
        }
        item.IsNewlyScanned = false;
        if (ScannedStockItems.Remove(item))
        {
            _ = _repository.RemoveScannedStockCheckItemAsync(item.Barcode);
            if (ItemMatchesFilter(item) && !PendingStockItems.Any(p => string.Equals(p.Barcode, item.Barcode, StringComparison.OrdinalIgnoreCase)))
            {
                PendingStockItems.Add(item);
            }
            RecalculateStockCheckTotals(useCachedFilter: true);
            ShowToast($"Removed {item.Barcode} from scanned items.", ToastType.Info);
        }
    }

    private void RemoveAllScannedItems()
    {
        if (ScannedStockItems.Count == 0) return;

        foreach (var kvp in _highlightCtsByBarcode)
        {
            try { kvp.Value.Cancel(); kvp.Value.Dispose(); } catch { }
        }
        _highlightCtsByBarcode.Clear();

        _ = _repository.ClearScannedStockCheckItemsAsync();
        foreach (var item in ScannedStockItems.ToList())
        {
            item.IsNewlyScanned = false;
            if (ItemMatchesFilter(item) && !PendingStockItems.Any(p => string.Equals(p.Barcode, item.Barcode, StringComparison.OrdinalIgnoreCase)))
            {
                PendingStockItems.Add(item);
            }
        }
        ScannedStockItems.Clear();
        SelectedScannedStockItem = null;
        RecalculateStockCheckTotals(useCachedFilter: true);
        ShowToast("All scanned barcodes removed and returned to pending.", ToastType.Info);
    }

    private async Task UpdateMrpAndPrintAsync()
    {
        var item = SelectedScannedStockItem ?? ScannedStockItems.FirstOrDefault();
        if (item == null)
        {
            ShowToast("Please select an item in the Scanned Barcodes grid.", ToastType.Warning);
            return;
        }

        if (UpdateMrpRate < 0 || UpdateMrpRate > 1000)
        {
            ShowToast("Rate must be a value between 0 and 1000.", ToastType.Warning);
            return;
        }

        var calculatedMrp = Math.Round(item.NetWt * UpdateMrpRate, 2);
        item.MRP = calculatedMrp;

        try
        {
            await _repository.UpdateMrpAsync(item.Barcode, calculatedMrp);
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to update MRP in database: {ex.Message}", ToastType.Error);
        }

        if (_barcodeCreationRepository != null)
        {
            try
            {
                await _barcodeCreationRepository.UpdateBarcodeAdvancedAsync(item.Barcode, calculatedMrp, null, false, "StickerPC");
            }
            catch { }
        }

        RecalculateStockCheckTotals();

        try
        {
            if (_barcodePrintService != null)
            {
                BarcodeSubformItem? subformItem = null;
                if (_barcodeCreationRepository != null)
                {
                    try
                    {
                        subformItem = await _barcodeCreationRepository.GetBarcodeByCodeAsync(item.Barcode);
                    }
                    catch { }
                }

                if (subformItem == null)
                {
                    subformItem = new BarcodeSubformItem
                    {
                        Barcode = item.Barcode,
                        ItemName = item.ItemName,
                        Qty = item.Qty,
                        GrossWt = item.GrossWt,
                        StoneWt = item.StoneWt,
                        NetWt = item.NetWt,
                        StoneValue = item.StoneValue,
                        Making = item.Making,
                        Purity = item.Purity,
                        SupplierName = item.SupplierName,
                        CostPrice = item.CostPrice,
                        MRP = calculatedMrp,
                        BType = string.IsNullOrWhiteSpace(item.BType) ? "PC" : item.BType,
                        Description = item.Description,
                        Size = item.Size,
                        BoxNo = item.BoxNo,
                        Sti = "StickerPC"
                    };
                }
                else
                {
                    subformItem.MRP = calculatedMrp;
                    subformItem.Sti = "StickerPC";
                    if (string.IsNullOrWhiteSpace(subformItem.BType)) subformItem.BType = "PC";
                }

                await _barcodePrintService.PrintBarcodeStickerAsync(subformItem, "StickerPC");
                ShowToast($"Updated MRP to ₹{calculatedMrp:N0} and sent StickerPC to printer for #{item.Barcode}", ToastType.Success);
            }
            else
            {
                ShowToast($"Updated MRP to ₹{calculatedMrp:N0} for #{item.Barcode} (Printer service not configured)", ToastType.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"MRP updated, but printing failed: {ex.Message}", ToastType.Error);
        }
    }

    private void AssignBoxIfSelected(BarcodeStockReportItem item)
    {
        if (!string.IsNullOrWhiteSpace(SelectedStockCheckBox) &&
            !string.Equals(SelectedStockCheckBox, "- Select Box -", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(SelectedStockCheckBox, "- None -", StringComparison.OrdinalIgnoreCase) &&
            SelectedStockCheckBox != "*")
        {
            var box = SelectedStockCheckBox.Trim();
            item.BoxNo = box;
            _ = _repository.UpdateBoxNoAsync(item.Barcode, box);
        }
    }

    private void ScannedStockItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (BarcodeStockReportItem item in e.OldItems)
            {
                item.PropertyChanged -= OnScannedItemPropertyChanged;
            }
        }
        if (e.NewItems != null)
        {
            foreach (BarcodeStockReportItem item in e.NewItems)
            {
                item.PropertyChanged += OnScannedItemPropertyChanged;
            }
        }
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in ScannedStockItems)
            {
                item.PropertyChanged -= OnScannedItemPropertyChanged;
                item.PropertyChanged += OnScannedItemPropertyChanged;
            }
        }
        UpdateIsAllScannedSelectedState();
    }

    private void OnScannedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BarcodeStockReportItem.IsSelected))
        {
            UpdateIsAllScannedSelectedState();
        }
    }

    private void UpdateIsAllScannedSelectedState()
    {
        OnPropertyChanged(nameof(SelectedScannedCount));
        OnPropertyChanged(nameof(HasScannedSelection));

        if (ScannedStockItems.Count == 0)
        {
            _isUpdatingAllScannedSelected = true;
            IsAllScannedSelected = false;
            _isUpdatingAllScannedSelected = false;
            return;
        }

        int count = ScannedStockItems.Count(x => x.IsSelected);
        _isUpdatingAllScannedSelected = true;
        if (count == ScannedStockItems.Count)
        {
            IsAllScannedSelected = true;
        }
        else if (count == 0)
        {
            IsAllScannedSelected = false;
        }
        else
        {
            IsAllScannedSelected = null;
        }
        _isUpdatingAllScannedSelected = false;
    }

    private async void AssignSelectedBoxToItem()
    {
        var checkedItems = ScannedStockItems.Where(i => i.IsSelected).ToList();
        var targetItems = checkedItems.Count > 0
            ? checkedItems
            : (SelectedScannedStockItem != null
                ? new List<BarcodeStockReportItem> { SelectedScannedStockItem }
                : (ScannedStockItems.Count == 1 ? new List<BarcodeStockReportItem> { ScannedStockItems[0] } : new List<BarcodeStockReportItem>()));

        if (targetItems.Count == 0)
        {
            ShowToast("Please check or select item(s) in Scanned Barcodes to assign Box.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedStockCheckBox) ||
            string.Equals(SelectedStockCheckBox, "- Select Box -", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(SelectedStockCheckBox, "- None -", StringComparison.OrdinalIgnoreCase) ||
            SelectedStockCheckBox == "*")
        {
            ShowToast("Please select a valid Box from the dropdown first.", ToastType.Warning);
            return;
        }

        var box = SelectedStockCheckBox.Trim();
        foreach (var item in targetItems)
        {
            item.BoxNo = box;
        }

        try
        {
            await _repository.UpdateBoxNosAsync(targetItems.Select(x => x.Barcode), box);
            if (targetItems.Count == 1)
            {
                ShowToast($"Barcode #{targetItems[0].Barcode} assigned to Box '{box}'.", ToastType.Success);
            }
            else
            {
                ShowToast($"{targetItems.Count} items assigned to Box '{box}'.", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to assign Box: {ex.Message}", ToastType.Error);
        }
    }

    private async Task AdjustStockAsync()
    {
        if (PendingStockItems.Count == 0)
        {
            ShowToast("No missing or pending items to adjust!", ToastType.Warning);
            return;
        }

        if (_stockTransferRepository == null)
        {
            ShowToast("Stock Transfer repository is not configured.", ToastType.Error);
            return;
        }

        var count = PendingStockItems.Count;
        var totalNet = PendingTotalNetWt;

        var confirm = MessageBox.Show(
            $"Are you sure you want to adjust {count} missing barcode(s) (Total Net Wt: {totalNet:N3}g) by transferring them from 'Showroom' to 'Damage'?",
            "Confirm Stock Adjustment",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var transferNo = await _stockTransferRepository.GetNextTransferNoAsync();
            var filterInfo = $"Head: {SelectedItemHead}, Sub: {SelectedSubGroup}, Purity: {SelectedPurity}";
            var transfer = new StockTransfer
            {
                TransferNo = transferNo,
                TransferDate = DateTime.Today,
                FromLocation = "Showroom",
                ToLocation = "Damage",
                TransferType = "Stock Check Adjustment",
                Remarks = $"Stock Check Adjustment - {DateTime.Now:dd/MM/yyyy HH:mm} - {filterInfo}",
                EntryDate = DateTime.Now,
                Status = "Saved"
            };

            var transferItems = PendingStockItems.Select(p => new StockTransferItem
            {
                TransferNo = transferNo,
                Barcode = p.Barcode,
                ItemName = p.ItemName,
                Qty = p.Qty,
                GrossWt = p.GrossWt,
                StoneWt = p.StoneWt,
                NetWt = p.NetWt,
                StoneValue = p.StoneValue,
                Making = p.Making,
                MRP = p.MRP,
                Purity = p.Purity,
                BoxNo = p.BoxNo,
                Description = p.Description,
                CostPrice = p.CostPrice
            }).ToList();

            await _stockTransferRepository.SaveTransferAsync(transfer, transferItems);

            PendingStockItems.Clear();
            RecalculateStockCheckTotals();

            ShowToast($"Adjusted {count} missing items to 'Damage' via Stock Transfer #{transferNo}!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to adjust stock: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ItemMatchesFilter(BarcodeStockReportItem item)
    {
        if (SelectedItemHead != "*" && !string.Equals(item.HeadGroup, SelectedItemHead, StringComparison.OrdinalIgnoreCase)) return false;
        if (SelectedSubGroup != "*" && !string.Equals(item.SubGroup, SelectedSubGroup, StringComparison.OrdinalIgnoreCase)) return false;
        if (SelectedPurity != "*" && !string.Equals(item.Purity, SelectedPurity, StringComparison.OrdinalIgnoreCase)) return false;
        if (SelectedItemName != "*" && !string.Equals(item.ItemName, SelectedItemName, StringComparison.OrdinalIgnoreCase)) return false;
        if (SelectedBoxNo != "*" && !string.Equals(item.BoxNo, SelectedBoxNo, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public void RecalculateStockCheckTotals(
        IEnumerable<BarcodeStockReportItem>? filteredScannedItems = null,
        IEnumerable<BarcodeStockReportItem>? filteredPendingItems = null,
        bool useCachedFilter = false)
    {
        if (filteredScannedItems != null)
        {
            _lastFilteredScannedItems = filteredScannedItems;
        }
        else if (!useCachedFilter)
        {
            _lastFilteredScannedItems = null;
        }

        if (filteredPendingItems != null)
        {
            _lastFilteredPendingItems = filteredPendingItems;
        }
        else if (!useCachedFilter)
        {
            _lastFilteredPendingItems = null;
        }

        // Scanned Totals
        var scannedList = (_lastFilteredScannedItems ?? ScannedStockItems).ToList();
        ScannedTotalCount = scannedList.Count;
        ScannedTotalQty = scannedList.Sum(x => x.Qty);
        ScannedTotalGrossWt = scannedList.Sum(x => x.GrossWt);
        ScannedTotalStoneWt = scannedList.Sum(x => x.StoneWt);
        ScannedTotalNetWt = scannedList.Sum(x => x.NetWt);
        ScannedTotalStoneValue = scannedList.Sum(x => x.StoneValue);
        ScannedTotalMRP = scannedList.Sum(x => x.MRP);
        ScannedRecordCountDisplay = $"Record: {(scannedList.Count > 0 ? 1 : 0)} of {scannedList.Count}";

        // Pending Totals
        var pendingList = (_lastFilteredPendingItems ?? PendingStockItems).ToList();
        PendingTotalCount = pendingList.Count;
        PendingTotalQty = pendingList.Sum(x => x.Qty);
        PendingTotalCost = pendingList.Sum(x => x.CostPrice);
        PendingTotalNetWt = pendingList.Sum(x => x.NetWt);
        PendingTotalMRP = pendingList.Sum(x => x.MRP);
        PendingTotalMaking = pendingList.Sum(x => x.Making);
        PendingRecordCountDisplay = $"Record: {(pendingList.Count > 0 ? 1 : 0)} of {pendingList.Count}";

        StockCheckProgressDisplay = $"Scanned: {ScannedStockItems.Count} | Pending: {PendingStockItems.Count} | Total: {ScannedStockItems.Count + PendingStockItems.Count}";
    }

    private async Task LoadHeadGroupSummariesAsync(BarcodeStockReportFilter filter)
    {
        var prevHead = SelectedHeadGroupSummary?.HeadGroup;
        var list = await Task.Run(() => _repository.GetHeadGroupSummariesAsync(filter));
        HeadGroupWiseList.ReplaceRange(list);
        RecalculateHeadGroupTotals(list);

        if (HeadGroupWiseList.Count > 0)
        {
            var match = !string.IsNullOrEmpty(prevHead)
                ? HeadGroupWiseList.FirstOrDefault(x => string.Equals(x.HeadGroup, prevHead, StringComparison.OrdinalIgnoreCase))
                : null;
            SelectedHeadGroupSummary = match ?? HeadGroupWiseList[0];
        }
        else
        {
            SelectedHeadGroupSummary = null;
        }
    }

    private async Task LoadSubGroupSummariesAsync(string headGroup)
    {
        var prevSub = SelectedSubGroupSummary?.SubGroup;
        var filter = new BarcodeStockReportFilter
        {
            ItemHead = SelectedItemHead,
            SubGroup = SelectedSubGroup,
            Purity = SelectedPurity,
            ItemName = SelectedItemName,
            SupplierName = SelectedSupplier,
            BoxNo = SelectedBoxNo == "*" ? null : SelectedBoxNo,
            FromDate = null,
            ToDate = ToDate,
            SearchText = SearchText
        };

        var list = await Task.Run(() => _repository.GetSubGroupSummariesAsync(headGroup, filter));
        SubGroupWiseList.ReplaceRange(list);
        RecalculateSubGroupTotals(list);

        if (SubGroupWiseList.Count > 0)
        {
            var match = !string.IsNullOrEmpty(prevSub)
                ? SubGroupWiseList.FirstOrDefault(x => string.Equals(x.SubGroup, prevSub, StringComparison.OrdinalIgnoreCase))
                : null;
            SelectedSubGroupSummary = match ?? SubGroupWiseList[0];
        }
        else
        {
            SelectedSubGroupSummary = null;
        }
    }

    private async Task LoadHeadGroupBarcodeDetailsAsync(string headGroup, string subGroup)
    {
        var filter = new BarcodeStockReportFilter
        {
            ItemHead = SelectedItemHead,
            SubGroup = SelectedSubGroup,
            Purity = SelectedPurity,
            ItemName = SelectedItemName,
            SupplierName = SelectedSupplier,
            BoxNo = SelectedBoxNo == "*" ? null : SelectedBoxNo,
            FromDate = null,
            ToDate = ToDate,
            SearchText = SearchText
        };

        var savePath = _savePath;
        var list = await Task.Run(async () =>
        {
            var data = await _repository.GetHeadGroupBarcodeDetailsAsync(headGroup, subGroup, filter);
            var imageIndex = GetBarcodeImageIndex(savePath);
            foreach (var b in data)
            {
                b.ImagePath = ResolveBarcodeImagePath(b.Barcode, b.Photo, imageIndex);
            }
            return data;
        });

        HeadGroupBarcodeList.ReplaceRange(list);
        RecalculateBarcodeDetailTotals(list);
    }

    private void RecalculateHeadGroupTotals(IReadOnlyList<BarcodeGroupSummary> list)
    {
        HeadGroupTotalQty = list.Sum(x => x.TotalQty);
        HeadGroupTotalGrossWt = list.Sum(x => x.TotalGrossWt);
        HeadGroupTotalStoneWt = list.Sum(x => x.TotalStoneWt);
        HeadGroupTotalNetWt = list.Sum(x => x.TotalNetWt);
        HeadGroupTotalStoneValue = list.Sum(x => x.TotalStoneValue);
        HeadGroupTotalMaking = list.Sum(x => x.TotalMaking);

        TotalCount = list.Sum(x => x.ItemCount);
        TotalQty = HeadGroupTotalQty;
        TotalGrossWt = HeadGroupTotalGrossWt;
        TotalStoneWt = HeadGroupTotalStoneWt;
        TotalNetWt = HeadGroupTotalNetWt;
        TotalStoneValue = HeadGroupTotalStoneValue;
        TotalMaking = HeadGroupTotalMaking;
        OnPropertyChanged(nameof(RecordCountDisplay));
    }

    private void RecalculateSubGroupTotals(IReadOnlyList<BarcodeGroupSummary> list)
    {
        SubGroupCountDisplay = $"Total Sub Group = {list.Count}";
        SubGroupTotalQty = list.Sum(x => x.TotalQty);
        SubGroupTotalGrossWt = list.Sum(x => x.TotalGrossWt);
        SubGroupTotalStoneWt = list.Sum(x => x.TotalStoneWt);
        SubGroupTotalNetWt = list.Sum(x => x.TotalNetWt);
        SubGroupTotalStoneValue = list.Sum(x => x.TotalStoneValue);
        SubGroupTotalMaking = list.Sum(x => x.TotalMaking);
    }

    private void RecalculateBarcodeDetailTotals(IReadOnlyList<BarcodeStockReportItem> list)
    {
        BarcodeCountDisplay = $"Total Barcode = {list.Count}";
        BarcodeTotalQty = list.Sum(x => x.Qty);
        BarcodeTotalGrossWt = list.Sum(x => x.GrossWt);
        BarcodeTotalStoneWt = list.Sum(x => x.StoneWt);
        BarcodeTotalNetWt = list.Sum(x => x.NetWt);
        BarcodeTotalStoneValue = list.Sum(x => x.StoneValue);
        BarcodeTotalMaking = list.Sum(x => x.Making);
    }

    private void RecalculateStockTotals(IReadOnlyList<BarcodeStockReportItem> list)
    {
        TotalCount = list.Count;
        TotalQty = list.Sum(x => x.Qty);
        TotalGrossWt = list.Sum(x => x.GrossWt);
        TotalStoneWt = list.Sum(x => x.StoneWt);
        TotalNetWt = list.Sum(x => x.NetWt);
        TotalStoneValue = list.Sum(x => x.StoneValue);
        TotalMaking = list.Sum(x => x.Making);
        TotalAmount = list.Sum(x => x.Amount);
        OnPropertyChanged(nameof(RecordCountDisplay));
    }

    private void RecalculateGroupTotals(IReadOnlyList<BarcodeGroupSummary> list)
    {
        TotalCount = list.Sum(x => x.ItemCount);
        TotalQty = list.Sum(x => x.TotalQty);
        TotalGrossWt = list.Sum(x => x.TotalGrossWt);
        TotalStoneWt = list.Sum(x => x.TotalStoneWt);
        TotalNetWt = list.Sum(x => x.TotalNetWt);
        TotalStoneValue = list.Sum(x => x.TotalStoneValue);
        TotalMaking = list.Sum(x => x.TotalMaking);
        TotalAmount = list.Sum(x => x.TotalAmount);
        OnPropertyChanged(nameof(RecordCountDisplay));
    }

    private void RecalculateMovementTotals(IReadOnlyList<BarcodeMovementItem> list)
    {
        TotalCount = list.Count;
        TotalQty = list.Sum(x => x.Qty);
        TotalGrossWt = list.Sum(x => x.GrossWt);
        TotalStoneWt = 0m;
        TotalNetWt = list.Sum(x => x.NetWt);
        TotalStoneValue = 0m;
        TotalMaking = 0m;
        TotalAmount = list.Sum(x => x.Amount);
        OnPropertyChanged(nameof(RecordCountDisplay));
    }

    private void PrintReport()
    {
        ShowToast($"Preparing {SelectedTab} Print Preview...", ToastType.Info);
        // Direct print dialog or preview window
    }

    private void PrintCapacityReport()
    {
        ShowToast("Printing Capacity / Stock Summary...", ToastType.Info);
    }

    private void ExportToCsv()
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"BarcodeStockReport_{SelectedTab}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                if (IsDetailedGridVisible)
                {
                    if (SelectedTab == BarcodeReportTab.ItemWiseSales)
                    {
                        sb.AppendLine("Barcode,Item Name,Bill No,Bill Date,Customer Name,Mobile No,Qty,Gross Wt,Stone Wt,Net Wt,Stone Value,Making,Making/g,Purity,Sub Group,Head Group");
                        foreach (var i in Items)
                        {
                            sb.AppendLine($"\"{i.Barcode}\",\"{i.ItemName}\",\"{i.VouNo}\",\"{i.BillDate:dd/MM/yyyy}\",\"{i.CustomerName}\",\"{i.Phone}\",{i.Qty},{i.GrossWt:F3},{i.StoneWt:F3},{i.NetWt:F3},{i.StoneValue:F2},{i.Making:F2},{i.MakingPerGram:F2},\"{i.Purity}\",\"{i.SubGroup}\",\"{i.HeadGroup}\"");
                        }
                    }
                    else
                    {
                        sb.AppendLine("Barcode,Item Name,Qty,Gross Wt,Stone Wt,Net Wt,Stone Value,Making,Purity,Sub Group,Head Group,Supplier");
                        foreach (var i in Items)
                        {
                            sb.AppendLine($"\"{i.Barcode}\",\"{i.ItemName}\",{i.Qty},{i.GrossWt:F3},{i.StoneWt:F3},{i.NetWt:F3},{i.StoneValue:F2},{i.Making:F2},\"{i.Purity}\",\"{i.SubGroup}\",\"{i.HeadGroup}\",\"{i.SupplierName}\"");
                        }
                    }
                }
                else if (IsStockCheckVisible)
                {
                    sb.AppendLine("STATUS,Barcode,Item Name,Size,Qty,Gross Wt,Stone Wt,Net Wt,Cost,MRP,Making,Purity,Sub Group,TimeStamp");
                    foreach (var s in ScannedStockItems)
                    {
                        sb.AppendLine($"\"SCANNED\",\"{s.Barcode}\",\"{s.ItemName}\",\"{s.Size}\",{s.Qty},{s.GrossWt:F3},{s.StoneWt:F3},{s.NetWt:F3},{s.CostPrice:F2},{s.MRP:F2},{s.Making:F2},\"{s.Purity}\",\"{s.SubGroup}\",\"{s.TimeStamp:yyyy-MM-dd HH:mm:ss}\"");
                    }
                    foreach (var p in PendingStockItems)
                    {
                        sb.AppendLine($"\"PENDING\",\"{p.Barcode}\",\"{p.ItemName}\",\"{p.Size}\",{p.Qty},{p.GrossWt:F3},{p.StoneWt:F3},{p.NetWt:F3},{p.CostPrice:F2},{p.MRP:F2},{p.Making:F2},\"{p.Purity}\",\"{p.SubGroup}\",\"\"");
                    }
                }
                else if (IsGroupGridVisible)
                {
                    sb.AppendLine("Group / Name,Purity,Count,Qty,Gross Wt,Stone Wt,Net Wt,Stone Value,Making");
                    foreach (var g in GroupSummaries)
                    {
                        sb.AppendLine($"\"{g.GroupKey}\",\"{g.Purity}\",{g.ItemCount},{g.TotalQty},{g.TotalGrossWt:F3},{g.TotalStoneWt:F3},{g.TotalNetWt:F3},{g.TotalStoneValue:F2},{g.TotalMaking:F2}");
                    }
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                ShowToast($"Exported successfully to {Path.GetFileName(sfd.FileName)}!", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Export failed: {ex.Message}", ToastType.Error);
        }
    }

    private void CopyPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            ShowToast("No mobile number available for this record.", ToastType.Warning);
            return;
        }

        try
        {
            Clipboard.SetText(phone.Trim());
            ShowToast($"Copied mobile number {phone.Trim()} to clipboard!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to copy to clipboard: {ex.Message}", ToastType.Error);
        }
    }

    private void CopyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            Clipboard.SetText(text.Trim());
            ShowToast($"Copied '{text.Trim()}' to clipboard!", ToastType.Success);
        }
        catch { }
    }

    private void OpenCustomerHistory(BarcodeStockReportItem? item)
    {
        if (item == null) return;
        var custName = item.CustomerName;
        var phone = item.Phone;
        if (string.IsNullOrWhiteSpace(custName) && string.IsNullOrWhiteSpace(phone))
        {
            ShowToast("No customer name or phone available for this item.", ToastType.Warning);
            return;
        }

        var window = new Views.CustomerHistoryWindow(custName, phone, _billRepository)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    private void RemoveScannedBarcode(object? parameter)
    {
        if (parameter is string s && ScannedBarcodes.Contains(s))
        {
            ScannedBarcodes.Remove(s);
            InvalidateFilterCache();
            _ = LoadCurrentTabReportAsync(forceReload: true);
        }
    }

    private void ClearScannedBarcodes()
    {
        ScannedBarcodes.Clear();
        InvalidateFilterCache();
        _ = LoadCurrentTabReportAsync(forceReload: true);
    }

    private void ExportNotFoundToCsv()
    {
        ShowToast("No items to export.", ToastType.Info);
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
