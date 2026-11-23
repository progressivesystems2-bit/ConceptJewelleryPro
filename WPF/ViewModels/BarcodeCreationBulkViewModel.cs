using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum BarcodeBulkVoucherMode
{
    View,
    Add,
    Modify
}

public sealed class BarcodeCreationBulkViewModel : ViewModelBase, IRefreshable
{
    private readonly IBarcodeCreationRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IPurityRepository _purityRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly IBoxMasterRepository? _boxMasterRepository;
    private readonly string _currentUser;

    private BarcodeBulkVoucherMode _formMode = BarcodeBulkVoucherMode.View;
    private BarcodeCreationHeader? _selectedRecentVoucher;
    private BarcodeCreationHeader? _originalVoucherSnapshot;
    private BarcodeSubformItem? _selectedLine;
    private bool _isBusy;
    private bool _suppressRecentSync;

    private string _vouNo = string.Empty;
    private int _nos;
    private DateTime _vouDate = DateTime.Today;
    private string _artisianName = string.Empty; // Supplier
    private string _suppBillNo = string.Empty;
    private string _status = "Saved";
    private string _entryUser = string.Empty;
    private DateTime _entryDate = DateTime.Today;
    private DateTime _entryTime = DateTime.Now;
    private string _computerName = Environment.MachineName;

    private decimal _totalQty;
    private decimal _totalGrossWeight;
    private decimal _totalNetWeight;
    private decimal _totalStoneWeight;
    private decimal _totalStoneValue;
    private decimal _totalMaking;
    private decimal _totalMRP;
    private int _totalItemsCount;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public BarcodeCreationBulkViewModel(
        IBarcodeCreationRepository repository,
        IAccountRepository accountRepository,
        IItemRepository itemRepository,
        IPurityRepository purityRepository,
        string currentUser,
        IAuditRepository? auditRepository = null,
        IBoxMasterRepository? boxMasterRepository = null)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _auditRepository = auditRepository;
        _boxMasterRepository = boxMasterRepository;
        _currentUser = currentUser;

        NewVoucherCommand = new AsyncRelayCommand(NewVoucherAsync, () => CanNew);
        SaveVoucherCommand = new AsyncRelayCommand(SaveVoucherAsync, () => CanSave);
        ModifyVoucherCommand = new RelayCommand(ModifyVoucher, () => CanModify);
        DeleteVoucherCommand = new AsyncRelayCommand(DeleteVoucherAsync, () => CanDelete);
        CancelVoucherCommand = new RelayCommand(CancelVoucher, () => CanCancel);
        AddLineCommand = new RelayCommand(AddLine, () => IsFormEnabled);
        RemoveLineCommand = new RelayCommand(RemoveLine, () => IsFormEnabled && SelectedLine is not null);
        PrintVoucherCommand = new RelayCommand(PrintVoucher, () => HasActiveVoucher);
        RefreshRecentCommand = new AsyncRelayCommand(LoadRecentVouchersAsync);
        RefreshLookupsCommand = new AsyncRelayCommand(RefreshLookupsAsync);

        Lines.CollectionChanged += Lines_CollectionChanged;

        _ = InitializeAsync();
    }

    public string Title => "Barcode Creation (Bulk)";

    public ObservableCollection<BarcodeCreationHeader> RecentVouchers { get; } = [];
    public ObservableCollection<BarcodeSubformItem> Lines { get; } = [];
    public ObservableCollection<Account> Suppliers { get; } = [];
    public ObservableCollection<Item> AvailableItems { get; } = [];
    public ObservableCollection<PurityMaster> AvailablePurities { get; } = [];
    public ObservableCollection<string> StickerFormats { get; } = [];
    public IReadOnlyList<string> TypeOptions { get; } = ["-", "WT", "PC"];
    public IReadOnlyList<string> MakingTypeOptions { get; } = ["GRAM", "PC", "%"];

    public AsyncRelayCommand NewVoucherCommand { get; }
    public AsyncRelayCommand SaveVoucherCommand { get; }
    public RelayCommand ModifyVoucherCommand { get; }
    public AsyncRelayCommand DeleteVoucherCommand { get; }
    public RelayCommand CancelVoucherCommand { get; }
    public RelayCommand AddLineCommand { get; }
    public RelayCommand RemoveLineCommand { get; }
    public RelayCommand PrintVoucherCommand { get; }
    public AsyncRelayCommand RefreshRecentCommand { get; }
    public AsyncRelayCommand RefreshLookupsCommand { get; }

    public BarcodeBulkVoucherMode FormMode
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
                RefreshCommands();
            }
        }
    }

    public bool IsFormEnabled => FormMode is BarcodeBulkVoucherMode.Add or BarcodeBulkVoucherMode.Modify;
    public bool CanNew => FormMode == BarcodeBulkVoucherMode.View && !IsBusy;
    public bool CanSave => IsFormEnabled && !IsBusy;
    public bool CanCancel => FormMode != BarcodeBulkVoucherMode.View && !IsBusy;
    public bool CanModify => FormMode == BarcodeBulkVoucherMode.View && SelectedRecentVoucher is not null && !IsBusy;
    public bool CanDelete => FormMode == BarcodeBulkVoucherMode.View && SelectedRecentVoucher is not null && !IsBusy;
    public bool HasActiveVoucher => !string.IsNullOrWhiteSpace(VouNo);

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

    public BarcodeCreationHeader? SelectedRecentVoucher
    {
        get => _selectedRecentVoucher;
        set
        {
            if (SetProperty(ref _selectedRecentVoucher, value))
            {
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                RefreshCommands();

                if (!_suppressRecentSync && value is not null)
                {
                    _ = LoadVoucherAsync(value.VouNo);
                }
            }
        }
    }

    public BarcodeSubformItem? SelectedLine
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

    public string VouNo
    {
        get => _vouNo;
        set
        {
            if (SetProperty(ref _vouNo, value))
            {
                OnPropertyChanged(nameof(HasActiveVoucher));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public DateTime VouDate
    {
        get => _vouDate;
        set => SetProperty(ref _vouDate, value);
    }

    public string ArtisianName
    {
        get => _artisianName;
        set => SetProperty(ref _artisianName, value);
    }

    public string SuppBillNo
    {
        get => _suppBillNo;
        set => SetProperty(ref _suppBillNo, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
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

    public string ComputerName
    {
        get => _computerName;
        set => SetProperty(ref _computerName, value);
    }

    public int TotalItemsCount
    {
        get => _totalItemsCount;
        set => SetProperty(ref _totalItemsCount, value);
    }

    public decimal TotalQty
    {
        get => _totalQty;
        set => SetProperty(ref _totalQty, value);
    }

    public decimal TotalGrossWeight
    {
        get => _totalGrossWeight;
        set => SetProperty(ref _totalGrossWeight, value);
    }

    public decimal TotalStoneWeight
    {
        get => _totalStoneWeight;
        set => SetProperty(ref _totalStoneWeight, value);
    }

    public decimal TotalNetWeight
    {
        get => _totalNetWeight;
        set => SetProperty(ref _totalNetWeight, value);
    }

    public decimal TotalStoneValue
    {
        get => _totalStoneValue;
        set => SetProperty(ref _totalStoneValue, value);
    }

    public decimal TotalMaking
    {
        get => _totalMaking;
        set => SetProperty(ref _totalMaking, value);
    }

    public decimal TotalMRP
    {
        get => _totalMRP;
        set => SetProperty(ref _totalMRP, value);
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
            var vouchersTask = _repository.GetRecentVouchersAsync(30);

            await Task.WhenAll(accountsTask, itemsTask, puritiesTask, stickersTask, vouchersTask);

            var accounts = await accountsTask;
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

            var recent = await vouchersTask;
            _suppressRecentSync = true;
            RecentVouchers.Clear();
            foreach (var v in recent)
            {
                RecentVouchers.Add(v);
            }
            _suppressRecentSync = false;

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

            var currentArtisian = ArtisianName;

            var accountsTask = _accountRepository.GetAllAsync();
            var itemsTask = _itemRepository.GetAllAsync();
            var puritiesTask = _purityRepository.GetAllAsync();
            var stickersTask = _repository.GetStickerFormatsAsync();

            await Task.WhenAll(accountsTask, itemsTask, puritiesTask, stickersTask);

            // 1. Suppliers
            var accounts = await accountsTask;
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
                if (!string.IsNullOrWhiteSpace(s) &&
                    !StickerFormats.Any(x => string.Equals(x, s, StringComparison.OrdinalIgnoreCase)))
                {
                    StickerFormats.Add(s);
                }
            }

            if (!string.IsNullOrWhiteSpace(currentArtisian))
            {
                ArtisianName = currentArtisian;
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

    public async Task LoadRecentVouchersAsync()
    {
        try
        {
            var recent = await _repository.GetRecentVouchersAsync(30);
            _suppressRecentSync = true;
            RecentVouchers.Clear();
            foreach (var v in recent)
            {
                RecentVouchers.Add(v);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load recent vouchers: {ex.Message}", ToastType.Error);
        }
        finally
        {
            _suppressRecentSync = false;
        }
    }

    public async Task LoadVoucherAsync(string vouNo)
    {
        try
        {
            IsBusy = true;
            var record = await _repository.GetVoucherByVouNoAsync(vouNo);
            if (record is null)
            {
                ShowToast($"Voucher {vouNo} not found.", ToastType.Warning);
                return;
            }

            PopulateFromVoucher(record);
            FormMode = BarcodeBulkVoucherMode.View;
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

    private async Task NewVoucherAsync()
    {
        try
        {
            IsBusy = true;
            var nextNos = await _repository.GetNextVoucherNosAsync();

            Nos = nextNos;
            VouNo = $"BC-{nextNos}";
            VouDate = DateTime.Today;
            ArtisianName = string.Empty;
            SuppBillNo = string.Empty;
            Status = "Saved";
            EntryUser = string.IsNullOrWhiteSpace(_currentUser) ? "admin" : _currentUser;
            EntryDate = DateTime.Today;
            EntryTime = DateTime.Now;
            ComputerName = Environment.MachineName;

            ClearLines();
            AddBlankLine();

            _suppressRecentSync = true;
            SelectedRecentVoucher = null;
            _suppressRecentSync = false;

            FormMode = BarcodeBulkVoucherMode.Add;
            RecalculateTotals();
            ShowToast($"Ready to enter new voucher: {VouNo}", ToastType.Info);
        }
        catch (Exception ex)
        {
            ShowToast($"Error starting new voucher: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyVoucher()
    {
        if (SelectedRecentVoucher is null)
        {
            ShowToast("Select a voucher to modify.", ToastType.Warning);
            return;
        }

        FormMode = BarcodeBulkVoucherMode.Modify;
        ShowToast($"Editing voucher {VouNo}.", ToastType.Info);
    }

    private async Task SaveVoucherAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("Voucher number is required.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ArtisianName))
        {
            ShowToast("Please enter or select a Supplier / Artisan Name.", ToastType.Warning);
            return;
        }

        var validLines = Lines.Where(l => !IsBlankLine(l)).ToList();
        if (validLines.Count == 0)
        {
            ShowToast("Please add at least one item to the voucher.", ToastType.Warning);
            return;
        }

        foreach (var line in validLines)
        {
            if (string.IsNullOrWhiteSpace(line.ItemName))
            {
                ShowToast("Each item row must have an Item Name.", ToastType.Warning);
                return;
            }

            line.VouNo = VouNo;
            line.SupplierName = ArtisianName;
            line.SuppInvNo = SuppBillNo;
            line.PDate = VouDate;
            if (line.PunchedOn == default) line.PunchedOn = DateTime.Now;
        }

        try
        {
            IsBusy = true;
            var isUpdate = FormMode == BarcodeBulkVoucherMode.Modify;

            var voucher = new BarcodeCreationHeader
            {
                VouNo = VouNo.Trim(),
                Nos = Nos,
                VouDate = VouDate,
                ArtisianName = ArtisianName.Trim(),
                SuppBillNo = SuppBillNo.Trim(),
                Status = "Saved",
                EntryUser = string.IsNullOrWhiteSpace(EntryUser) ? _currentUser : EntryUser,
                EntryDate = EntryDate == default ? DateTime.Today : EntryDate,
                EntryTime = DateTime.Now,
                ComputerName = string.IsNullOrWhiteSpace(ComputerName) ? Environment.MachineName : ComputerName,
                Items = validLines
            };

            await _repository.SaveBulkVoucherAsync(voucher, isUpdate);

            if (isUpdate && _auditRepository != null)
            {
                await AuditService.LogBarcodeBulkVoucherModificationAsync(_auditRepository, _originalVoucherSnapshot, voucher, _currentUser);
            }

            ShowToast($"Voucher {VouNo} saved with {validLines.Count} barcode items!", ToastType.Success);
            BarcodeEvents.NotifyBarcodesChanged();
            await LoadRecentVouchersAsync();

            _suppressRecentSync = true;
            SelectedRecentVoucher = RecentVouchers.FirstOrDefault(v => v.VouNo == voucher.VouNo);
            _suppressRecentSync = false;

            FormMode = BarcodeBulkVoucherMode.View;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save voucher: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteVoucherAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("No voucher selected for deletion.", ToastType.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete Voucher '{VouNo}' and all its barcodes?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var target = SelectedRecentVoucher ?? _originalVoucherSnapshot ?? new BarcodeCreationHeader
            {
                VouNo = VouNo,
                ArtisianName = ArtisianName,
                SuppBillNo = SuppBillNo,
                Items = Lines.Where(l => !IsBlankLine(l)).ToList()
            };
            await _repository.DeleteVoucherAsync(VouNo);

            if (_auditRepository != null && target != null)
            {
                await AuditService.LogBarcodeBulkVoucherDeletionAsync(_auditRepository, target, _currentUser);
            }

            ShowToast($"Voucher {VouNo} deleted successfully.", ToastType.Success);
            BarcodeEvents.NotifyBarcodesChanged();

            await LoadRecentVouchersAsync();
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

    private void CancelVoucher()
    {
        if (SelectedRecentVoucher is not null)
        {
            _ = LoadVoucherAsync(SelectedRecentVoucher.VouNo);
        }
        else
        {
            PrepareBlankViewState();
        }

        FormMode = BarcodeBulkVoucherMode.View;
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
        SelectedLine = Lines.FirstOrDefault();
        if (Lines.Count == 0)
        {
            AddBlankLine();
        }
        RecalculateTotals();
    }

    private void PrintVoucher()
    {
        if (!HasActiveVoucher) return;
        ShowToast($"Print summary for Voucher {VouNo} ({TotalItemsCount} items, Net Wt: {TotalNetWeight:F3}g)", ToastType.Info);
    }

    private void PopulateFromVoucher(BarcodeCreationHeader voucher)
    {
        VouNo = voucher.VouNo;
        Nos = voucher.Nos;
        VouDate = voucher.VouDate;
        ArtisianName = voucher.ArtisianName;
        SuppBillNo = voucher.SuppBillNo;
        Status = voucher.Status;
        EntryUser = voucher.EntryUser;
        EntryDate = voucher.EntryDate;
        EntryTime = voucher.EntryTime;
        ComputerName = voucher.ComputerName;

        _originalVoucherSnapshot = new BarcodeCreationHeader
        {
            VouNo = voucher.VouNo,
            Nos = voucher.Nos,
            VouDate = voucher.VouDate,
            ArtisianName = voucher.ArtisianName,
            SuppBillNo = voucher.SuppBillNo,
            Status = voucher.Status,
            EntryUser = voucher.EntryUser,
            EntryDate = voucher.EntryDate,
            EntryTime = voucher.EntryTime,
            ComputerName = voucher.ComputerName,
            Items = voucher.Items.Select(x => x.Clone()).ToList()
        };

        ClearLines();
        foreach (var item in voucher.Items)
        {
            var itemClone = item.Clone();
            SubscribeLine(itemClone);
            Lines.Add(itemClone);
        }

        RecalculateTotals();
    }

    private void PrepareBlankViewState()
    {
        VouNo = string.Empty;
        Nos = 0;
        VouDate = DateTime.Today;
        ArtisianName = string.Empty;
        SuppBillNo = string.Empty;
        Status = "Saved";
        EntryUser = _currentUser;
        EntryDate = DateTime.Today;
        EntryTime = DateTime.Now;
        ComputerName = Environment.MachineName;

        ClearLines();
        FormMode = BarcodeBulkVoucherMode.View;
        RecalculateTotals();
    }

    private void AddBlankLine()
    {
        var item = new BarcodeSubformItem
        {
            VouNo = VouNo,
            BType = "WT",
            Qty = 1,
            GrossWt = 0m,
            StoneWt = 0m,
            NetWt = 0m,
            Making = 0m,
            MakingType = "GRAM",
            Size = "-",
            SupplierName = ArtisianName,
            SuppInvNo = SuppBillNo,
            PDate = VouDate,
            PunchedOn = DateTime.Now,
            Sti = "StickerWt"
        };
        SubscribeLine(item);
        Lines.Add(item);
        SelectedLine = item;
    }

    private void ClearLines()
    {
        foreach (var line in Lines)
        {
            UnsubscribeLine(line);
        }
        Lines.Clear();
    }

    private void SubscribeLine(BarcodeSubformItem item)
    {
        item.PropertyChanged += Line_PropertyChanged;
    }

    private void UnsubscribeLine(BarcodeSubformItem item)
    {
        item.PropertyChanged -= Line_PropertyChanged;
    }

    private void Line_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BarcodeSubformItem.GrossWt) or
            nameof(BarcodeSubformItem.StoneWt) or
            nameof(BarcodeSubformItem.NetWt) or
            nameof(BarcodeSubformItem.Qty) or
            nameof(BarcodeSubformItem.StoneValue) or
            nameof(BarcodeSubformItem.Making) or
            nameof(BarcodeSubformItem.MRP) or
            nameof(BarcodeSubformItem.ItemName))
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
        TotalGrossWeight = activeLines.Sum(l => l.GrossWt);
        TotalStoneWeight = activeLines.Sum(l => l.StoneWt);
        TotalNetWeight = activeLines.Sum(l => l.NetWt);
        TotalStoneValue = activeLines.Sum(l => l.StoneValue);
        TotalMaking = activeLines.Sum(l => l.Making);
        TotalMRP = activeLines.Sum(l => l.MRP);
    }

    private static bool IsBlankLine(BarcodeSubformItem item)
    {
        return string.IsNullOrWhiteSpace(item.ItemName) && item.GrossWt == 0 && item.Qty == 0;
    }

    private void RefreshCommands()
    {
        NewVoucherCommand.RaiseCanExecuteChanged();
        SaveVoucherCommand.RaiseCanExecuteChanged();
        DeleteVoucherCommand.RaiseCanExecuteChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
