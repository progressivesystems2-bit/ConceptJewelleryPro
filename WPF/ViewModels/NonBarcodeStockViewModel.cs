using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum NonBarcodeFormMode
{
    View,
    Add,
    Modify
}

public sealed class NonBarcodeStockViewModel : ViewModelBase, IRefreshable
{
    private readonly INonBarcodeRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IItemRepository _itemRepository;
    private readonly string _currentUser;

    private NonBarcodeFormMode _formMode = NonBarcodeFormMode.View;
    private NonBarcode? _selectedRecentRecord;
    private NonBarcodeSubform? _selectedLine;
    private bool _isBusy;
    private bool _suppressRecentSync;

    private int _nos;
    private string _vouNo = string.Empty;
    private DateTime _vouDate = DateTime.Today;
    private string _supplierName = string.Empty;
    private string _entryUser = string.Empty;
    private DateTime _entryDate = DateTime.Today;
    private DateTime _entryTime = DateTime.Now;
    private string _status = "Active";
    private string _computerName = Environment.MachineName;

    private decimal _totalQty;
    private decimal _totalWeight;
    private int _totalItemsCount;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public NonBarcodeStockViewModel(
        INonBarcodeRepository repository,
        IAccountRepository accountRepository,
        IItemRepository itemRepository,
        string currentUser)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _itemRepository = itemRepository;
        _currentUser = currentUser;

        NewStockCommand = new AsyncRelayCommand(NewStockAsync, () => CanNewStock);
        SaveStockCommand = new AsyncRelayCommand(SaveStockAsync, () => CanSaveStock);
        ModifyStockCommand = new RelayCommand(ModifyStock, () => CanModifyStock);
        DeleteStockCommand = new AsyncRelayCommand(DeleteStockAsync, () => CanDeleteStock);
        CancelStockCommand = new RelayCommand(CancelStock, () => CanCancelStock);
        AddLineCommand = new RelayCommand(AddLine, () => IsFormEnabled);
        RemoveLineCommand = new RelayCommand(RemoveLine, () => IsFormEnabled && SelectedLine is not null);
        PrintStockCommand = new RelayCommand(PrintStock, () => HasActiveRecord);
        RefreshRecentCommand = new AsyncRelayCommand(LoadRecentRecordsAsync);
        RefreshLookupsCommand = new AsyncRelayCommand(RefreshLookupsAsync);

        Lines.CollectionChanged += Lines_CollectionChanged;

        _ = InitializeAsync();
    }

    public string Title => "Non-Barcode Stock";

    public ObservableCollection<NonBarcode> RecentRecords { get; } = [];
    public ObservableCollection<NonBarcodeSubform> Lines { get; } = [];
    public ObservableCollection<Account> Suppliers { get; } = [];
    public ObservableCollection<Item> AvailableItems { get; } = [];
    public ObservableCollection<string> AvailableItemNames { get; } = [];

    public AsyncRelayCommand NewStockCommand { get; }
    public AsyncRelayCommand SaveStockCommand { get; }
    public RelayCommand ModifyStockCommand { get; }
    public AsyncRelayCommand DeleteStockCommand { get; }
    public RelayCommand CancelStockCommand { get; }
    public RelayCommand AddLineCommand { get; }
    public RelayCommand RemoveLineCommand { get; }
    public RelayCommand PrintStockCommand { get; }
    public AsyncRelayCommand RefreshRecentCommand { get; }
    public AsyncRelayCommand RefreshLookupsCommand { get; }

    public NonBarcodeFormMode FormMode
    {
        get => _formMode;
        private set
        {
            if (SetProperty(ref _formMode, value))
            {
                OnPropertyChanged(nameof(IsFormEnabled));
                OnPropertyChanged(nameof(IsReadOnly));
                OnPropertyChanged(nameof(CanNewStock));
                OnPropertyChanged(nameof(CanSaveStock));
                OnPropertyChanged(nameof(CanModifyStock));
                OnPropertyChanged(nameof(CanDeleteStock));
                OnPropertyChanged(nameof(CanCancelStock));
                RefreshCommands();
            }
        }
    }

    public bool IsFormEnabled => FormMode is NonBarcodeFormMode.Add or NonBarcodeFormMode.Modify;
    public bool IsReadOnly => !IsFormEnabled;
    public bool CanNewStock => FormMode == NonBarcodeFormMode.View && !IsBusy;
    public bool CanSaveStock => IsFormEnabled && !IsBusy;
    public bool CanCancelStock => FormMode != NonBarcodeFormMode.View && !IsBusy;
    public bool CanModifyStock => FormMode == NonBarcodeFormMode.View && SelectedRecentRecord is not null && !IsBusy;
    public bool CanDeleteStock => FormMode == NonBarcodeFormMode.View && SelectedRecentRecord is not null && !IsBusy;
    public bool HasActiveRecord => !string.IsNullOrWhiteSpace(VouNo);

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

    public NonBarcode? SelectedRecentRecord
    {
        get => _selectedRecentRecord;
        set
        {
            if (SetProperty(ref _selectedRecentRecord, value))
            {
                OnPropertyChanged(nameof(CanModifyStock));
                OnPropertyChanged(nameof(CanDeleteStock));
                RefreshCommands();

                if (!_suppressRecentSync && value is not null)
                {
                    _ = LoadRecordAsync(value.VouNo);
                }
            }
        }
    }

    public NonBarcodeSubform? SelectedLine
    {
        get => _selectedLine;
        set
        {
            if (SetProperty(ref _selectedLine, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public string VouNo
    {
        get => _vouNo;
        set
        {
            if (SetProperty(ref _vouNo, value))
            {
                OnPropertyChanged(nameof(HasActiveRecord));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public DateTime VouDate
    {
        get => _vouDate;
        set => SetProperty(ref _vouDate, value);
    }

    public string SupplierName
    {
        get => _supplierName;
        set => SetProperty(ref _supplierName, value);
    }

    public string EntryUser
    {
        get => _entryUser;
        set => SetProperty(ref _entryUser, value);
    }

    public DateTime EntryDate
    {
        get => _entryDate;
        set => SetProperty(ref _entryDate, value);
    }

    public DateTime EntryTime
    {
        get => _entryTime;
        set => SetProperty(ref _entryTime, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string ComputerName
    {
        get => _computerName;
        set => SetProperty(ref _computerName, value);
    }

    public decimal TotalQty
    {
        get => _totalQty;
        set => SetProperty(ref _totalQty, value);
    }

    public decimal TotalWeight
    {
        get => _totalWeight;
        set => SetProperty(ref _totalWeight, value);
    }

    public int TotalItemsCount
    {
        get => _totalItemsCount;
        set => SetProperty(ref _totalItemsCount, value);
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            var accounts = await _accountRepository.GetAllAsync();
            Suppliers.Clear();
            var filteredAccounts = accounts
                .Where(acc =>
                {
                    var cat = acc.HeadCategory?.Trim() ?? string.Empty;
                    var inOut = acc.InOut?.Trim() ?? string.Empty;
                    return cat.Contains("Creditor", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Artis", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Karighar", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Debtor", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Job", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Supplier", StringComparison.OrdinalIgnoreCase) ||
                           inOut.StartsWith("Out", StringComparison.OrdinalIgnoreCase) ||
                           string.IsNullOrWhiteSpace(cat);
                })
                .OrderBy(a => a.AccountName)
                .ToList();

            if (filteredAccounts.Count == 0)
            {
                filteredAccounts = accounts
                    .Where(a => !string.Equals(a.HeadCategory, "Bank Accounts", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Cash-in-hand", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Duties & Taxes", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Capital Account", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(a => a.AccountName)
                    .ToList();
            }

            foreach (var acc in filteredAccounts)
            {
                Suppliers.Add(acc);
            }

            var items = await _itemRepository.GetAllAsync();
            AvailableItems.Clear();
            AvailableItemNames.Clear();

            var distinctNames = items
                .Select(i => i.ItemName?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s);

            foreach (var item in items)
            {
                AvailableItems.Add(item);
            }

            foreach (var name in distinctNames)
            {
                AvailableItemNames.Add(name!);
            }

            await LoadRecentRecordsAsync();
            PrepareBlankViewState();
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

    public async Task RefreshAsync() => await RefreshLookupsAsync();

    public async Task RefreshLookupsAsync()
    {
        try
        {
            IsBusy = true;

            var currentSupplier = SupplierName;

            var accounts = await _accountRepository.GetAllAsync();
            Suppliers.Clear();
            var filteredAccounts = accounts
                .Where(acc =>
                {
                    var cat = acc.HeadCategory?.Trim() ?? string.Empty;
                    var inOut = acc.InOut?.Trim() ?? string.Empty;
                    return cat.Contains("Creditor", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Artis", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Karighar", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Debtor", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Job", StringComparison.OrdinalIgnoreCase) ||
                           cat.Contains("Supplier", StringComparison.OrdinalIgnoreCase) ||
                           inOut.StartsWith("Out", StringComparison.OrdinalIgnoreCase) ||
                           string.IsNullOrWhiteSpace(cat);
                })
                .OrderBy(a => a.AccountName)
                .ToList();

            if (filteredAccounts.Count == 0)
            {
                filteredAccounts = accounts
                    .Where(a => !string.Equals(a.HeadCategory, "Bank Accounts", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Cash-in-hand", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Duties & Taxes", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Capital Account", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(a => a.AccountName)
                    .ToList();
            }

            foreach (var acc in filteredAccounts)
            {
                Suppliers.Add(acc);
            }

            var items = await _itemRepository.GetAllAsync();
            AvailableItems.Clear();
            AvailableItemNames.Clear();

            var distinctNames = items
                .Select(i => i.ItemName?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s);

            foreach (var item in items)
            {
                AvailableItems.Add(item);
            }

            foreach (var name in distinctNames)
            {
                AvailableItemNames.Add(name!);
            }

            if (!string.IsNullOrWhiteSpace(currentSupplier))
            {
                SupplierName = currentSupplier;
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

    public async Task LoadRecentRecordsAsync()
    {
        try
        {
            var recent = await _repository.GetRecentAsync(50);
            _suppressRecentSync = true;
            RecentRecords.Clear();
            foreach (var item in recent)
            {
                RecentRecords.Add(item);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load recent records: {ex.Message}", ToastType.Error);
        }
        finally
        {
            _suppressRecentSync = false;
        }
    }

    public async Task LoadRecordAsync(string vouNo)
    {
        try
        {
            IsBusy = true;
            var record = await _repository.GetByVouNoAsync(vouNo);
            if (record is null)
            {
                ShowToast($"Voucher {vouNo} not found.", ToastType.Warning);
                return;
            }

            PopulateFromRecord(record);
            FormMode = NonBarcodeFormMode.View;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load voucher: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task NewStockAsync()
    {
        try
        {
            IsBusy = true;
            var nextNos = await _repository.GetNextNosAsync();

            Nos = nextNos;
            VouNo = $"NB-{nextNos}";
            VouDate = DateTime.Today;
            SupplierName = string.Empty;
            EntryUser = string.IsNullOrWhiteSpace(_currentUser) ? "admin" : _currentUser;
            EntryDate = DateTime.Today;
            EntryTime = DateTime.Now;
            Status = "Active";
            ComputerName = Environment.MachineName;

            ClearLines();
            AddBlankLine();

            _suppressRecentSync = true;
            SelectedRecentRecord = null;
            _suppressRecentSync = false;

            FormMode = NonBarcodeFormMode.Add;
            RecalculateTotals();
            ShowToast("New non-barcode stock entry ready.", ToastType.Info);
        }
        catch (Exception ex)
        {
            ShowToast($"Error starting new entry: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyStock()
    {
        if (SelectedRecentRecord is null)
        {
            ShowToast("Select a voucher to modify.", ToastType.Warning);
            return;
        }

        FormMode = NonBarcodeFormMode.Modify;
        ShowToast($"Editing voucher {VouNo}.", ToastType.Info);
    }

    private async Task SaveStockAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("Voucher number is required.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SupplierName))
        {
            ShowToast("Please select or enter a Supplier Name.", ToastType.Warning);
            return;
        }

        var validLines = Lines.Where(l => !IsBlankLine(l)).ToList();
        if (validLines.Count == 0)
        {
            ShowToast("Please add at least one stock item.", ToastType.Warning);
            return;
        }

        foreach (var line in validLines)
        {
            if (string.IsNullOrWhiteSpace(line.ItemName))
            {
                ShowToast("Each item row must have an Item Name.", ToastType.Warning);
                return;
            }

            if (line.Qty <= 0 && line.Weight <= 0)
            {
                ShowToast($"Row with item '{line.ItemName}' must have a valid Quantity or Weight.", ToastType.Warning);
                return;
            }

            line.VouNo = VouNo;
            if (line.PunchTime == default)
            {
                line.PunchTime = DateTime.Now;
            }
        }

        try
        {
            IsBusy = true;
            var isUpdate = FormMode == NonBarcodeFormMode.Modify;

            var stock = new NonBarcode
            {
                VouNo = VouNo.Trim(),
                Nos = Nos,
                VouDate = VouDate,
                SupplierName = SupplierName.Trim(),
                EntryUser = string.IsNullOrWhiteSpace(EntryUser) ? _currentUser : EntryUser,
                EntryDate = EntryDate == default ? DateTime.Today : EntryDate,
                EntryTime = DateTime.Now,
                Status = "Active",
                ComputerName = string.IsNullOrWhiteSpace(ComputerName) ? Environment.MachineName : ComputerName,
                Lines = validLines
            };

            await _repository.SaveAsync(stock, isUpdate);

            ShowToast($"Stock voucher {VouNo} saved successfully!", ToastType.Success);
            await LoadRecentRecordsAsync();

            _suppressRecentSync = true;
            SelectedRecentRecord = RecentRecords.FirstOrDefault(r => string.Equals(r.VouNo, stock.VouNo, StringComparison.OrdinalIgnoreCase));
            _suppressRecentSync = false;

            FormMode = NonBarcodeFormMode.View;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save stock: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteStockAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("No voucher selected for deletion.", ToastType.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete Voucher '{VouNo}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsBusy = true;
            await _repository.DeleteAsync(VouNo);
            ShowToast($"Voucher {VouNo} deleted successfully.", ToastType.Success);

            await LoadRecentRecordsAsync();
            PrepareBlankViewState();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to delete voucher: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelStock()
    {
        if (SelectedRecentRecord is not null)
        {
            _ = LoadRecordAsync(SelectedRecentRecord.VouNo);
        }
        else
        {
            PrepareBlankViewState();
        }

        FormMode = NonBarcodeFormMode.View;
        ShowToast("Operation cancelled.", ToastType.Info);
    }

    private void AddLine()
    {
        AddBlankLine();
        RecalculateTotals();
    }

    private void RemoveLine()
    {
        if (SelectedLine is null)
            return;

        UnsubscribeLine(SelectedLine);
        Lines.Remove(SelectedLine);

        if (Lines.Count == 0 && IsFormEnabled)
        {
            AddBlankLine();
        }

        RecalculateTotals();
    }

    private void PrintStock()
    {
        if (!HasActiveRecord)
            return;

        ShowToast($"Print summary for Voucher {VouNo} (Total: {TotalQty} pcs, {TotalWeight:F3}g)", ToastType.Info);
    }

    private void PopulateFromRecord(NonBarcode record)
    {
        Nos = record.Nos;
        VouNo = record.VouNo;
        VouDate = record.VouDate;
        SupplierName = record.SupplierName;
        EntryUser = record.EntryUser;
        EntryDate = record.EntryDate;
        EntryTime = record.EntryTime;
        Status = record.Status;
        ComputerName = record.ComputerName;

        ClearLines();
        foreach (var line in record.Lines)
        {
            var lineClone = line.Clone();
            SubscribeLine(lineClone);
            Lines.Add(lineClone);
        }

        RecalculateTotals();
    }

    private void PrepareBlankViewState()
    {
        Nos = 0;
        VouNo = string.Empty;
        VouDate = DateTime.Today;
        SupplierName = string.Empty;
        EntryUser = _currentUser;
        EntryDate = DateTime.Today;
        EntryTime = DateTime.Now;
        Status = "Active";
        ComputerName = Environment.MachineName;

        ClearLines();
        FormMode = NonBarcodeFormMode.View;
        RecalculateTotals();
    }

    private void AddBlankLine()
    {
        var line = new NonBarcodeSubform
        {
            VouNo = VouNo,
            Qty = 1,
            Weight = 0m,
            PunchTime = DateTime.Now
        };
        SubscribeLine(line);
        Lines.Add(line);
        SelectedLine = line;
    }

    private void ClearLines()
    {
        foreach (var line in Lines)
        {
            UnsubscribeLine(line);
        }
        Lines.Clear();
    }

    private void SubscribeLine(NonBarcodeSubform line)
    {
        line.PropertyChanged += Line_PropertyChanged;
    }

    private void UnsubscribeLine(NonBarcodeSubform line)
    {
        line.PropertyChanged -= Line_PropertyChanged;
    }

    private void Line_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NonBarcodeSubform.Qty) or nameof(NonBarcodeSubform.Weight) or nameof(NonBarcodeSubform.ItemName))
        {
            RecalculateTotals();
        }
    }

    private void Lines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        var activeLines = Lines.Where(l => !IsBlankLine(l)).ToList();
        TotalItemsCount = activeLines.Count;
        TotalQty = activeLines.Sum(l => l.Qty);
        TotalWeight = activeLines.Sum(l => l.Weight);
    }

    private static bool IsBlankLine(NonBarcodeSubform line)
    {
        return string.IsNullOrWhiteSpace(line.ItemName) && line.Qty == 0 && line.Weight == 0;
    }

    private void RefreshCommands()
    {
        NewStockCommand.RaiseCanExecuteChanged();
        SaveStockCommand.RaiseCanExecuteChanged();
        DeleteStockCommand.RaiseCanExecuteChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
