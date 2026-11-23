using System.Collections.ObjectModel;
using System.Windows;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum StockTransferFormMode
{
    View,
    Add,
    Modify
}

public sealed class StockTransferViewModel : ViewModelBase, IConfirmCloseDocument
{
    private readonly IStockTransferRepository _repository;
    private readonly string _currentUser;

    private StockTransferFormMode _formMode = StockTransferFormMode.View;
    private StockTransferItem? _selectedItem;
    private StockTransfer? _selectedTransfer;
    private bool _isBusy;
    private int _selectedTabIndex;
    private string _searchText = string.Empty;
    private DateTime? _filterFromDate = DateTime.Today.AddDays(-30);
    private DateTime? _filterToDate = DateTime.Today;

    // Header Fields
    private string _transferNo = string.Empty;
    private DateTime _transferDate = DateTime.Today;
    private string _fromLocation = "Showroom";
    private string _toLocation = "Damage";
    private string _transferType = "Manual Transfer";
    private string _remarks = string.Empty;
    private string _scanBarcodeText = string.Empty;

    // Live Totals
    private int _totalCount;
    private decimal _totalQty;
    private decimal _totalGrossWt;
    private decimal _totalStoneWt;
    private decimal _totalNetWt;
    private decimal _totalStoneValue;
    private decimal _totalMaking;
    private decimal _totalMRP;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public StockTransferViewModel(
        IStockTransferRepository repository,
        string currentUser = "admin")
    {
        _repository = repository;
        _currentUser = currentUser;

        NewCommand = new AsyncRelayCommand(NewTransferAsync, () => CanNew);
        SaveCommand = new AsyncRelayCommand(SaveTransferAsync, () => CanSave);
        ModifyCommand = new RelayCommand(ModifyTransfer, () => CanModify);
        DeleteCommand = new AsyncRelayCommand(DeleteTransferAsync, () => CanDelete);
        CancelCommand = new RelayCommand(CancelTransfer, () => CanCancel);
        RefreshCommand = new AsyncRelayCommand(LoadTransfersAsync);
        ScanBarcodeCommand = new RelayCommand(param => _ = ScanBarcodeAsync(param));
        RemoveItemCommand = new RelayCommand(param => RemoveItem(param as StockTransferItem ?? SelectedItem));
        ClearItemsCommand = new RelayCommand(ClearItems);
        PrintCommand = new RelayCommand(PrintTransfer);

        _ = InitializeAsync();
    }

    public string Title => "Stock Transfer";

    public ObservableCollection<string> Locations { get; } = [];
    public ObservableCollection<StockTransferItem> Items { get; } = [];
    public ObservableCollection<StockTransfer> TransfersList { get; } = [];
    public ObservableCollection<StockTransfer> FilteredTransfersList { get; } = [];

    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand ModifyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ScanBarcodeCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand ClearItemsCommand { get; }
    public RelayCommand PrintCommand { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public StockTransferFormMode FormMode
    {
        get => _formMode;
        set
        {
            if (SetProperty(ref _formMode, value))
            {
                OnPropertyChanged(nameof(IsFormEnabled));
                OnPropertyChanged(nameof(CanNew));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(ModeBadgeText));
                OnPropertyChanged(nameof(ModeBadgeBackground));
            }
        }
    }

    public bool IsFormEnabled => FormMode != StockTransferFormMode.View;
    public bool CanNew => FormMode == StockTransferFormMode.View && !IsBusy;
    public bool CanSave => FormMode != StockTransferFormMode.View && !IsBusy;
    public bool CanModify => FormMode == StockTransferFormMode.View && !string.IsNullOrWhiteSpace(TransferNo) && !IsBusy;
    public bool CanDelete => FormMode == StockTransferFormMode.View && !string.IsNullOrWhiteSpace(TransferNo) && !IsBusy;
    public bool CanCancel => FormMode != StockTransferFormMode.View;

    public string ModeBadgeText => FormMode switch
    {
        StockTransferFormMode.Add => "NEW TRANSFER",
        StockTransferFormMode.Modify => "EDITING",
        _ => "VIEWING"
    };

    public string ModeBadgeBackground => FormMode switch
    {
        StockTransferFormMode.Add => "#059669",
        StockTransferFormMode.Modify => "#D97706",
        _ => "#4B5563"
    };

    public string TransferNo
    {
        get => _transferNo;
        set => SetProperty(ref _transferNo, value);
    }

    public DateTime TransferDate
    {
        get => _transferDate;
        set => SetProperty(ref _transferDate, value);
    }

    public string FromLocation
    {
        get => _fromLocation;
        set => SetProperty(ref _fromLocation, value);
    }

    public string ToLocation
    {
        get => _toLocation;
        set => SetProperty(ref _toLocation, value);
    }

    public string TransferType
    {
        get => _transferType;
        set => SetProperty(ref _transferType, value);
    }

    public string Remarks
    {
        get => _remarks;
        set => SetProperty(ref _remarks, value);
    }

    public string ScanBarcodeText
    {
        get => _scanBarcodeText;
        set => SetProperty(ref _scanBarcodeText, value);
    }

    public StockTransferItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public StockTransfer? SelectedTransfer
    {
        get => _selectedTransfer;
        set
        {
            if (SetProperty(ref _selectedTransfer, value) && value != null)
            {
                _ = LoadTransferDetailsAsync(value.TransferNo);
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
                ApplyTransferFilter();
            }
        }
    }

    public DateTime? FilterFromDate
    {
        get => _filterFromDate;
        set
        {
            if (SetProperty(ref _filterFromDate, value))
            {
                _ = LoadTransfersAsync();
            }
        }
    }

    public DateTime? FilterToDate
    {
        get => _filterToDate;
        set
        {
            if (SetProperty(ref _filterToDate, value))
            {
                _ = LoadTransfersAsync();
            }
        }
    }

    // Totals
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

    public decimal TotalMRP
    {
        get => _totalMRP;
        private set => SetProperty(ref _totalMRP, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanNew));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
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

    public void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastMessage = message;
        ToastType = type;
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var locs = await _repository.GetLocationsAsync();
            Locations.Clear();
            foreach (var l in locs) Locations.Add(l);

            if (!Locations.Contains("Showroom")) Locations.Insert(0, "Showroom");
            if (!Locations.Contains("Damage")) Locations.Add("Damage");

            await LoadTransfersAsync();

            var latest = TransfersList.FirstOrDefault();
            if (latest != null)
            {
                await LoadTransferDetailsAsync(latest.TransferNo);
            }
            else
            {
                await NewTransferAsync();
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to initialize: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadTransfersAsync()
    {
        try
        {
            var list = await _repository.GetAllTransfersAsync(FilterFromDate, FilterToDate);
            TransfersList.Clear();
            foreach (var t in list) TransfersList.Add(t);
            ApplyTransferFilter();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load transfers: {ex.Message}", ToastType.Error);
        }
    }

    private void ApplyTransferFilter()
    {
        FilteredTransfersList.Clear();
        var query = TransfersList.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(x =>
                x.TransferNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.FromLocation.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.ToLocation.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Remarks.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            FilteredTransfersList.Add(item);
        }
    }

    public async Task LoadTransferDetailsAsync(string transferNo)
    {
        if (string.IsNullOrWhiteSpace(transferNo)) return;

        IsBusy = true;
        try
        {
            var header = await _repository.GetByTransferNoAsync(transferNo);
            if (header != null)
            {
                TransferNo = header.TransferNo;
                TransferDate = header.TransferDate;
                FromLocation = header.FromLocation;
                ToLocation = header.ToLocation;
                TransferType = header.TransferType;
                Remarks = header.Remarks;

                var items = await _repository.GetTransferItemsAsync(transferNo);
                Items.Clear();
                foreach (var itm in items) Items.Add(itm);

                RecalculateTotals();
                FormMode = StockTransferFormMode.View;
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load transfer details: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task NewTransferAsync()
    {
        IsBusy = true;
        try
        {
            TransferNo = await _repository.GetNextTransferNoAsync();
            TransferDate = DateTime.Today;
            FromLocation = "Showroom";
            ToLocation = "Damage";
            TransferType = "Manual Transfer";
            Remarks = string.Empty;
            ScanBarcodeText = string.Empty;
            Items.Clear();
            RecalculateTotals();
            FormMode = StockTransferFormMode.Add;
            SelectedTabIndex = 0;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to create new transfer: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyTransfer()
    {
        if (string.IsNullOrWhiteSpace(TransferNo)) return;
        FormMode = StockTransferFormMode.Modify;
        SelectedTabIndex = 0;
    }

    private void CancelTransfer()
    {
        if (SelectedTransfer != null)
        {
            _ = LoadTransferDetailsAsync(SelectedTransfer.TransferNo);
        }
        else
        {
            var first = TransfersList.FirstOrDefault();
            if (first != null)
            {
                _ = LoadTransferDetailsAsync(first.TransferNo);
            }
            else
            {
                FormMode = StockTransferFormMode.View;
            }
        }
    }

    private async Task SaveTransferAsync()
    {
        if (string.IsNullOrWhiteSpace(TransferNo))
        {
            ShowToast("Transfer Number cannot be empty.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(FromLocation))
        {
            ShowToast("Please specify From Location.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ToLocation))
        {
            ShowToast("Please specify To Location.", ToastType.Warning);
            return;
        }

        if (string.Equals(FromLocation, ToLocation, StringComparison.OrdinalIgnoreCase))
        {
            ShowToast("From Location and To Location cannot be identical.", ToastType.Warning);
            return;
        }

        if (Items.Count == 0)
        {
            ShowToast("Please add at least one barcode item to transfer.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var transfer = new StockTransfer
            {
                TransferNo = TransferNo.Trim(),
                TransferDate = TransferDate,
                FromLocation = FromLocation.Trim(),
                ToLocation = ToLocation.Trim(),
                TransferType = string.IsNullOrWhiteSpace(TransferType) ? "Manual Transfer" : TransferType.Trim(),
                Remarks = Remarks?.Trim() ?? string.Empty,
                EntryUser = _currentUser,
                EntryDate = DateTime.Now,
                Status = "Saved"
            };

            await _repository.SaveTransferAsync(transfer, Items);
            ShowToast($"Stock Transfer #{transfer.TransferNo} saved successfully!", ToastType.Success);
            BarcodeEvents.NotifyBarcodesChanged();

            await LoadTransfersAsync();
            FormMode = StockTransferFormMode.View;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save transfer: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteTransferAsync()
    {
        if (string.IsNullOrWhiteSpace(TransferNo)) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete Stock Transfer #{TransferNo}?\nThis will revert transferred items back to '{FromLocation}'.",
            "Confirm Delete Transfer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _repository.DeleteTransferAsync(TransferNo);
            ShowToast($"Stock Transfer #{TransferNo} deleted and items reverted.", ToastType.Info);
            BarcodeEvents.NotifyBarcodesChanged();

            await LoadTransfersAsync();
            var next = TransfersList.FirstOrDefault();
            if (next != null)
            {
                await LoadTransferDetailsAsync(next.TransferNo);
            }
            else
            {
                await NewTransferAsync();
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to delete transfer: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ScanBarcodeAsync(object? parameter = null)
    {
        var rawCode = parameter as string;
        var code = (string.IsNullOrWhiteSpace(rawCode) ? ScanBarcodeText : rawCode)?.Trim();
        ScanBarcodeText = string.Empty;
        if (string.IsNullOrWhiteSpace(code)) return;

        if (Items.Any(x => string.Equals(x.Barcode, code, StringComparison.OrdinalIgnoreCase)))
        {
            ShowToast($"Barcode {code} is already added to this transfer!", ToastType.Warning);
            return;
        }

        try
        {
            var item = await _repository.LookupBarcodeForTransferAsync(code);
            if (item != null)
            {
                item.TransferNo = TransferNo;
                Items.Insert(0, item);
                SelectedItem = item;
                RecalculateTotals();
                ShowToast($"Added: {item.Barcode} - {item.ItemName} ({item.NetWt:N3}g)", ToastType.Success);
            }
            else
            {
                ShowToast($"Barcode {code} not found in stock subform!", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Error looking up barcode: {ex.Message}", ToastType.Error);
        }
    }

    private void RemoveItem(StockTransferItem? item)
    {
        if (item == null) return;
        if (Items.Remove(item))
        {
            RecalculateTotals();
            ShowToast($"Removed barcode {item.Barcode}.", ToastType.Info);
        }
    }

    private void ClearItems()
    {
        if (Items.Count == 0) return;
        Items.Clear();
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        TotalCount = Items.Count;
        TotalQty = Items.Sum(x => x.Qty);
        TotalGrossWt = Math.Round(Items.Sum(x => x.GrossWt), 3);
        TotalStoneWt = Math.Round(Items.Sum(x => x.StoneWt), 3);
        TotalNetWt = Math.Round(Items.Sum(x => x.NetWt), 3);
        TotalStoneValue = Math.Round(Items.Sum(x => x.StoneValue), 2);
        TotalMaking = Math.Round(Items.Sum(x => x.Making), 2);
        TotalMRP = Math.Round(Items.Sum(x => x.MRP), 2);
    }

    private void PrintTransfer()
    {
        if (string.IsNullOrWhiteSpace(TransferNo)) return;
        ShowToast($"Print Voucher for #{TransferNo} (Total items: {TotalCount}, Net Wt: {TotalNetWt:N3}g)", ToastType.Info);
    }

    public bool HasUnsavedChanges() => FormMode != StockTransferFormMode.View;

    public string? CloseConfirmationMessage => "You have unsaved changes in Stock Transfer. Do you want to discard them and close?";
}
