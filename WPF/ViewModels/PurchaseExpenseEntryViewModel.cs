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

public enum PurchaseExpenseFormMode
{
    View,
    Add,
    Modify
}

public sealed class PurchaseExpenseEntryViewModel : ViewModelBase, IRefreshable
{
    private readonly IPurchaseExpenseRepository _purchaseExpenseRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;

    private PurchaseExpenseFormMode _formMode = PurchaseExpenseFormMode.View;
    private PurchaseExpense? _selectedRecentExpense;
    private PurchaseExpense? _originalExpenseSnapshot;
    private PurchaseExpenseLine? _selectedLine;
    private bool _isBusy;
    private bool _suppressRecentSync;
    private bool _suppressRecalc;

    private int _nos;
    private string _vouNo = string.Empty;
    private DateTime _billDate = DateTime.Today;
    private string _accountName = string.Empty;
    private string _purchaseHead = string.Empty;
    private string _gstBillFrom = string.Empty;
    private string _gstin = string.Empty;
    private string _suppBillNo = string.Empty;
    private DateTime? _suppBillDate = DateTime.Today;
    private bool _reverse;
    private decimal _reverseTaxRate;
    private decimal _totalQuantity;
    private decimal _grossTotal;
    private decimal _taxableAmount;
    private decimal _taxAmount;
    private string _exclusiveAccount = string.Empty;
    private decimal _exclusive;
    private decimal _igst;
    private decimal _cgst;
    private decimal _sgst;
    private decimal _subTotal;
    private decimal _roundedOff;
    private decimal _netTotal;
    private string _note = string.Empty;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public PurchaseExpenseEntryViewModel(
        IPurchaseExpenseRepository purchaseExpenseRepository,
        IAccountRepository accountRepository,
        IItemRepository itemRepository,
        string currentUser,
        IAuditRepository? auditRepository = null)
    {
        _purchaseExpenseRepository = purchaseExpenseRepository;
        _accountRepository = accountRepository;
        _itemRepository = itemRepository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;

        NewExpenseCommand = new AsyncRelayCommand(NewExpenseAsync, () => CanNewExpense);
        SaveExpenseCommand = new AsyncRelayCommand(SaveExpenseAsync, () => CanSaveExpense);
        ModifyExpenseCommand = new RelayCommand(ModifyExpense, () => CanModifyExpense);
        DeleteExpenseCommand = new AsyncRelayCommand(DeleteExpenseAsync, () => CanDeleteExpense);
        CancelExpenseCommand = new RelayCommand(CancelExpense, () => CanCancelExpense);
        AddLineCommand = new RelayCommand(AddLine, () => IsFormEnabled);
        RemoveLineCommand = new RelayCommand(RemoveLine, () => IsFormEnabled && SelectedLine is not null);
        PrintExpenseCommand = new RelayCommand(PrintExpense, () => HasActiveExpense);
        ListExpensesCommand = new AsyncRelayCommand(ListExpensesAsync);
        RefreshLookupsCommand = new AsyncRelayCommand(RefreshLookupsAsync);

        Lines.CollectionChanged += Lines_CollectionChanged;

        _ = InitializeAsync();
    }

    public string Title => "Purchases Expenses";

    public ObservableCollection<PurchaseExpense> RecentExpenses { get; } = [];
    public ObservableCollection<PurchaseExpenseLine> Lines { get; } = [];
    public ObservableCollection<Account> Suppliers { get; } = [];
    public ObservableCollection<Account> Accounts { get; } = [];
    public ObservableCollection<Item> AvailableItems { get; } = [];
    public ObservableCollection<decimal> TaxRates { get; } = [0m, 5m, 12m, 18m, 28m];

    public AsyncRelayCommand NewExpenseCommand { get; }
    public AsyncRelayCommand SaveExpenseCommand { get; }
    public RelayCommand ModifyExpenseCommand { get; }
    public AsyncRelayCommand DeleteExpenseCommand { get; }
    public RelayCommand CancelExpenseCommand { get; }
    public RelayCommand AddLineCommand { get; }
    public RelayCommand RemoveLineCommand { get; }
    public RelayCommand PrintExpenseCommand { get; }
    public AsyncRelayCommand ListExpensesCommand { get; }
    public AsyncRelayCommand RefreshLookupsCommand { get; }

    public PurchaseExpenseFormMode FormMode
    {
        get => _formMode;
        private set
        {
            if (SetProperty(ref _formMode, value))
            {
                OnPropertyChanged(nameof(IsFormEnabled));
                OnPropertyChanged(nameof(CanNewExpense));
                OnPropertyChanged(nameof(CanSaveExpense));
                OnPropertyChanged(nameof(CanModifyExpense));
                OnPropertyChanged(nameof(CanDeleteExpense));
                OnPropertyChanged(nameof(CanCancelExpense));
                RefreshCommands();
            }
        }
    }

    public bool IsFormEnabled => FormMode is PurchaseExpenseFormMode.Add or PurchaseExpenseFormMode.Modify;
    public bool CanNewExpense => FormMode == PurchaseExpenseFormMode.View && !IsBusy;
    public bool CanSaveExpense => IsFormEnabled && !IsBusy;
    public bool CanCancelExpense => FormMode != PurchaseExpenseFormMode.View && !IsBusy;
    public bool CanModifyExpense => FormMode == PurchaseExpenseFormMode.View && SelectedRecentExpense is not null && !IsBusy;
    public bool CanDeleteExpense => FormMode == PurchaseExpenseFormMode.View && SelectedRecentExpense is not null && !IsBusy;
    public bool HasActiveExpense => !string.IsNullOrWhiteSpace(VouNo);

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

    public PurchaseExpense? SelectedRecentExpense
    {
        get => _selectedRecentExpense;
        set
        {
            if (SetProperty(ref _selectedRecentExpense, value))
            {
                OnPropertyChanged(nameof(CanModifyExpense));
                OnPropertyChanged(nameof(CanDeleteExpense));
                RefreshCommands();

                if (!_suppressRecentSync && value is not null)
                {
                    _ = LoadExpenseAsync(value.VouNo);
                }
            }
        }
    }

    public PurchaseExpenseLine? SelectedLine
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
                OnPropertyChanged(nameof(HasActiveExpense));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public DateTime BillDate
    {
        get => _billDate;
        set => SetProperty(ref _billDate, value);
    }

    public string AccountName
    {
        get => _accountName;
        set
        {
            if (SetProperty(ref _accountName, value))
            {
                OnAccountNameChanged(value);
            }
        }
    }

    public string PurchaseHead
    {
        get => _purchaseHead;
        set => SetProperty(ref _purchaseHead, value);
    }

    public string GstBillFrom
    {
        get => _gstBillFrom;
        set => SetProperty(ref _gstBillFrom, value);
    }

    public string Gstin
    {
        get => _gstin;
        set => SetProperty(ref _gstin, value);
    }

    public string SuppBillNo
    {
        get => _suppBillNo;
        set => SetProperty(ref _suppBillNo, value);
    }

    public DateTime? SuppBillDate
    {
        get => _suppBillDate;
        set => SetProperty(ref _suppBillDate, value);
    }

    public bool Reverse
    {
        get => _reverse;
        set => SetProperty(ref _reverse, value);
    }

    public decimal ReverseTaxRate
    {
        get => _reverseTaxRate;
        set
        {
            if (SetProperty(ref _reverseTaxRate, value))
            {
                if (IsFormEnabled)
                {
                    foreach (var line in Lines.Where(l => !IsBlankLine(l)))
                    {
                        line.TaxRate = value;
                    }
                    RecalculateTotals();
                }
            }
        }
    }

    public decimal TotalQuantity
    {
        get => _totalQuantity;
        set => SetProperty(ref _totalQuantity, value);
    }

    public decimal GrossTotal
    {
        get => _grossTotal;
        set => SetProperty(ref _grossTotal, value);
    }

    public decimal TaxableAmount
    {
        get => _taxableAmount;
        set => SetProperty(ref _taxableAmount, value);
    }

    public decimal TaxAmount
    {
        get => _taxAmount;
        set => SetProperty(ref _taxAmount, value);
    }

    public string ExclusiveAccount
    {
        get => _exclusiveAccount;
        set => SetProperty(ref _exclusiveAccount, value);
    }

    public decimal Exclusive
    {
        get => _exclusive;
        set
        {
            if (SetProperty(ref _exclusive, value))
            {
                RecalculateTotals();
            }
        }
    }

    public decimal Igst
    {
        get => _igst;
        set
        {
            if (SetProperty(ref _igst, value))
            {
                RecalculateTotals();
            }
        }
    }

    public decimal Cgst
    {
        get => _cgst;
        set
        {
            if (SetProperty(ref _cgst, value))
            {
                RecalculateTotals();
            }
        }
    }

    public decimal Sgst
    {
        get => _sgst;
        set
        {
            if (SetProperty(ref _sgst, value))
            {
                RecalculateTotals();
            }
        }
    }

    public decimal SubTotal
    {
        get => _subTotal;
        set => SetProperty(ref _subTotal, value);
    }

    public decimal RoundedOff
    {
        get => _roundedOff;
        set
        {
            if (SetProperty(ref _roundedOff, value))
            {
                RecalculateTotals();
            }
        }
    }

    public decimal NetTotal
    {
        get => _netTotal;
        set => SetProperty(ref _netTotal, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            var accounts = await _accountRepository.GetAllAsync();
            Accounts.Clear();
            Suppliers.Clear();
            foreach (var acc in accounts)
            {
                Accounts.Add(acc);
            }

            var filteredSuppliers = accounts
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

            if (filteredSuppliers.Count == 0)
            {
                filteredSuppliers = accounts
                    .Where(a => !string.Equals(a.HeadCategory, "Bank Accounts", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Cash-in-hand", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Duties & Taxes", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Capital Account", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(a => a.AccountName)
                    .ToList();
            }

            foreach (var acc in filteredSuppliers)
            {
                Suppliers.Add(acc);
            }

            var items = await _itemRepository.GetAllAsync();
            AvailableItems.Clear();
            foreach (var itm in items)
            {
                AvailableItems.Add(itm);
            }

            await LoadRecentExpensesAsync();
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
            var accounts = await _accountRepository.GetAllAsync();
            Accounts.Clear();
            Suppliers.Clear();
            foreach (var acc in accounts)
            {
                Accounts.Add(acc);
            }

            var filteredSuppliers = accounts
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

            if (filteredSuppliers.Count == 0)
            {
                filteredSuppliers = accounts
                    .Where(a => !string.Equals(a.HeadCategory, "Bank Accounts", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Cash-in-hand", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Duties & Taxes", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a.HeadCategory, "Capital Account", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(a => a.AccountName)
                    .ToList();
            }

            foreach (var acc in filteredSuppliers)
            {
                Suppliers.Add(acc);
            }

            var items = await _itemRepository.GetAllAsync();
            AvailableItems.Clear();
            foreach (var itm in items)
            {
                AvailableItems.Add(itm);
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

    private void RefreshCommands()
    {
        NewExpenseCommand.RaiseCanExecuteChanged();
        SaveExpenseCommand.RaiseCanExecuteChanged();
        DeleteExpenseCommand.RaiseCanExecuteChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnAccountNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var account = Accounts.FirstOrDefault(item => string.Equals(item.AccountName, value, StringComparison.OrdinalIgnoreCase))
            ?? Suppliers.FirstOrDefault(item => string.Equals(item.AccountName, value, StringComparison.OrdinalIgnoreCase));

        if (account is null)
            return;

        if (string.IsNullOrWhiteSpace(GstBillFrom) || FormMode == PurchaseExpenseFormMode.Add)
        {
            GstBillFrom = account.AccountName;
        }

        if (!string.IsNullOrWhiteSpace(account.VatNo))
        {
            Gstin = account.VatNo;
        }
    }

    private async Task NewExpenseAsync()
    {
        try
        {
            IsBusy = true;
            FormMode = PurchaseExpenseFormMode.Add;
            ClearForm();
            SetSelectedRecentExpense(null);

            Nos = await _purchaseExpenseRepository.GetNextNosAsync();
            VouNo = $"PEX{Nos:D6}";
            BillDate = DateTime.Today;
            SuppBillDate = DateTime.Today;
            AddBlankLine();
            ShowToast($"New purchase expense voucher {VouNo} started.", ToastType.Info);
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

    private async Task SaveExpenseAsync()
    {
        if (!IsFormEnabled)
            return;

        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("Voucher number is required.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(AccountName))
        {
            ShowToast("Supplier name is required.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PurchaseHead))
        {
            ShowToast("Purchase head account is required.", ToastType.Warning);
            return;
        }

        var committedLines = GetCommittedLines();
        if (committedLines.Count == 0)
        {
            ShowToast("Add at least one item line.", ToastType.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            var expense = BuildExpense(committedLines);
            var isUpdate = FormMode == PurchaseExpenseFormMode.Modify;
            await _purchaseExpenseRepository.SaveAsync(expense, isUpdate);

            if (isUpdate && _auditRepository != null)
            {
                await AuditService.LogPurchaseExpenseModificationAsync(_auditRepository, _originalExpenseSnapshot, expense, _currentUser);
            }

            await LoadRecentExpensesAsync(expense.VouNo);
            await LoadExpenseAsync(expense.VouNo);
            FormMode = PurchaseExpenseFormMode.View;
            ShowToast($"Purchase expense voucher {expense.VouNo} saved successfully!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Save failed: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyExpense()
    {
        if (SelectedRecentExpense is null)
            return;

        FormMode = PurchaseExpenseFormMode.Modify;
        EnsureBlankLine();
        ShowToast($"Editing purchase expense {VouNo}.", ToastType.Info);
    }

    private async Task DeleteExpenseAsync()
    {
        if (SelectedRecentExpense is null)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete purchase expense {SelectedRecentExpense.VouNo}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsBusy = true;
            var target = SelectedRecentExpense ?? _originalExpenseSnapshot;
            await _purchaseExpenseRepository.DeleteAsync(target.Nos);

            if (_auditRepository != null && target != null)
            {
                await AuditService.LogPurchaseExpenseDeletionAsync(_auditRepository, target, _currentUser);
            }

            await LoadRecentExpensesAsync();
            PrepareBlankViewState();
            ShowToast($"Purchase expense {target.VouNo} deleted successfully.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Delete failed: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelExpense()
    {
        if (SelectedRecentExpense is not null)
        {
            _ = LoadExpenseAsync(SelectedRecentExpense.VouNo);
        }
        else
        {
            PrepareBlankViewState();
        }

        ShowToast("Action cancelled.", ToastType.Info);
    }

    private async Task ListExpensesAsync()
    {
        try
        {
            IsBusy = true;
            await LoadRecentExpensesAsync(SelectedRecentExpense?.VouNo);
            ShowToast("Recent expenses refreshed.", ToastType.Info);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PrintExpense()
    {
        ShowToast($"Printing purchase expense {VouNo}...", ToastType.Info);
    }

    private void AddLine()
    {
        var line = AddBlankLine();
        SelectedLine = line;
    }

    private void RemoveLine()
    {
        if (SelectedLine is null)
            return;

        DetachLine(SelectedLine);
        Lines.Remove(SelectedLine);
        SelectedLine = Lines.FirstOrDefault();
        EnsureBlankLine();
        RecalculateTotals();
    }

    private async Task LoadRecentExpensesAsync(string? selectedVouNo = null)
    {
        var recent = await _purchaseExpenseRepository.GetRecentAsync(25);
        RecentExpenses.Clear();
        foreach (var item in recent)
        {
            RecentExpenses.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(selectedVouNo))
        {
            var match = RecentExpenses.FirstOrDefault(item => string.Equals(item.VouNo, selectedVouNo, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                SetSelectedRecentExpense(match);
            }
        }
    }

    public async Task LoadExpenseAsync(string vouNo)
    {
        try
        {
            var expense = await _purchaseExpenseRepository.GetByVouNoAsync(vouNo);
            if (expense is null)
            {
                ShowToast($"Purchase expense not found: {vouNo}", ToastType.Warning);
                return;
            }

            ApplyExpense(expense);
            FormMode = PurchaseExpenseFormMode.View;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load expense {vouNo}: {ex.Message}", ToastType.Error);
        }
    }

    private void ApplyExpense(PurchaseExpense expense)
    {
        _suppressRecentSync = true;
        try
        {
            Nos = expense.Nos;
            VouNo = expense.VouNo;
            BillDate = expense.BillDate;
            AccountName = expense.AccountName;
            PurchaseHead = expense.Head;
            GstBillFrom = expense.PartyName;
            Gstin = expense.Gstin;
            SuppBillNo = expense.SuppBillNo;
            SuppBillDate = expense.SuppBillDate;
            Reverse = expense.Reverse;
            ReverseTaxRate = expense.ReverseTaxRate;
            ExclusiveAccount = expense.ExclusiveAccount;
            Exclusive = expense.Exclusive;
            Igst = expense.Igst;
            Cgst = expense.Cgst;
            Sgst = expense.Sgst;
            RoundedOff = expense.RoundedOff;
            Note = expense.Narration;

            ClearLines();
            foreach (var line in expense.Lines)
            {
                Lines.Add(line);
            }

            _originalExpenseSnapshot = new PurchaseExpense
            {
                Nos = expense.Nos,
                VouNo = expense.VouNo,
                BillDate = expense.BillDate,
                AccountName = expense.AccountName,
                Head = expense.Head,
                PartyName = expense.PartyName,
                Gstin = expense.Gstin,
                SuppBillNo = expense.SuppBillNo,
                SuppBillDate = expense.SuppBillDate,
                Reverse = expense.Reverse,
                ReverseTaxRate = expense.ReverseTaxRate,
                ExclusiveAccount = expense.ExclusiveAccount,
                Exclusive = expense.Exclusive,
                Igst = expense.Igst,
                Cgst = expense.Cgst,
                Sgst = expense.Sgst,
                RoundedOff = expense.RoundedOff,
                Narration = expense.Narration,
                GrossTotal = expense.GrossTotal,
                TaxableAmount = expense.TaxableAmount,
                TaxAmount = expense.TaxAmount,
                NetAmount = expense.NetAmount,
                Lines = expense.Lines.Select(l => l.Clone()).ToList()
            };

            RecalculateTotals();

            var recent = RecentExpenses.FirstOrDefault(item => string.Equals(item.VouNo, expense.VouNo, StringComparison.OrdinalIgnoreCase));
            if (recent is not null)
            {
                SelectedRecentExpense = recent;
            }
        }
        finally
        {
            _suppressRecentSync = false;
        }
    }

    private void PrepareBlankViewState()
    {
        ClearForm();
        SetSelectedRecentExpense(null);
        FormMode = PurchaseExpenseFormMode.View;
    }

    private void ClearForm()
    {
        Nos = 0;
        VouNo = string.Empty;
        BillDate = DateTime.Today;
        AccountName = string.Empty;
        PurchaseHead = string.Empty;
        GstBillFrom = string.Empty;
        Gstin = string.Empty;
        SuppBillNo = string.Empty;
        SuppBillDate = DateTime.Today;
        Reverse = false;
        ReverseTaxRate = 0m;
        ExclusiveAccount = string.Empty;
        Exclusive = 0m;
        Igst = 0m;
        Cgst = 0m;
        Sgst = 0m;
        RoundedOff = 0m;
        Note = string.Empty;
        ClearLines();
        RecalculateTotals();
    }

    private PurchaseExpense BuildExpense(IReadOnlyList<PurchaseExpenseLine> committedLines) => new()
    {
        Nos = Nos,
        VouType = "PurchaseE",
        VouNo = VouNo.Trim(),
        BillDate = BillDate,
        AccountName = AccountName.Trim(),
        Head = PurchaseHead.Trim(),
        PartyName = GstBillFrom.Trim(),
        Gstin = Gstin.Trim(),
        SuppBillNo = SuppBillNo.Trim(),
        SuppBillDate = SuppBillDate,
        Reverse = Reverse,
        ReverseTaxRate = ReverseTaxRate,
        TotalQuantity = TotalQuantity,
        GrossTotal = GrossTotal,
        TaxableAmount = TaxableAmount,
        TaxAmount = TaxAmount,
        ExclusiveAccount = ExclusiveAccount.Trim(),
        Exclusive = Exclusive,
        Igst = Igst,
        Cgst = Cgst,
        Sgst = Sgst,
        RoundedOff = RoundedOff,
        NetAmount = NetTotal,
        Narration = Note.Trim(),
        User = string.IsNullOrWhiteSpace(_currentUser) ? Environment.UserName : _currentUser,
        Status = "Active",
        Lines = committedLines.Select(l => l.Clone()).ToList()
    };

    private void Lines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var line in e.OldItems.OfType<PurchaseExpenseLine>())
                DetachLine(line);
        }

        if (e.NewItems is not null)
        {
            foreach (var line in e.NewItems.OfType<PurchaseExpenseLine>())
                AttachLine(line);
        }

        RecalculateTotals();
    }

    private void AttachLine(PurchaseExpenseLine line) => line.PropertyChanged += Line_PropertyChanged;

    private void DetachLine(PurchaseExpenseLine line) => line.PropertyChanged -= Line_PropertyChanged;

    private void Line_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressRecalc || sender is not PurchaseExpenseLine line)
            return;

        if (string.Equals(e.PropertyName, nameof(PurchaseExpenseLine.ItemName), StringComparison.OrdinalIgnoreCase))
        {
            ResolveLineItem(line);
        }

        if (string.Equals(e.PropertyName, nameof(PurchaseExpenseLine.Qty), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(PurchaseExpenseLine.Rate), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(PurchaseExpenseLine.TaxRate), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(PurchaseExpenseLine.ItemName), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(PurchaseExpenseLine.Hsn), StringComparison.OrdinalIgnoreCase))
        {
            RecalculateLine(line);
            if (IsFormEnabled)
            {
                EnsureBlankLine();
            }
            RecalculateTotals();
        }
    }

    private void ResolveLineItem(PurchaseExpenseLine line)
    {
        if (string.IsNullOrWhiteSpace(line.ItemName))
            return;

        var matchedItem = AvailableItems.FirstOrDefault(i =>
            string.Equals(i.ItemName, line.ItemName.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.ItemCode, line.ItemName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (matchedItem is null)
            return;

        _suppressRecalc = true;
        try
        {
            line.SlNo = matchedItem.ItemCode;
            if (string.IsNullOrWhiteSpace(line.Hsn) && !string.IsNullOrWhiteSpace(matchedItem.HSNCode))
            {
                line.Hsn = matchedItem.HSNCode;
            }
            if (line.TaxRate <= 0)
            {
                var tax = matchedItem.GSTRate > 0 ? matchedItem.GSTRate : matchedItem.Tax;
                if (tax > 0)
                {
                    line.TaxRate = tax;
                }
            }
        }
        finally
        {
            _suppressRecalc = false;
        }

        RecalculateLine(line);
        RecalculateTotals();
    }

    private void RecalculateLine(PurchaseExpenseLine line)
    {
        _suppressRecalc = true;
        try
        {
            line.RateAmount = Math.Round(line.Qty * line.Rate, 2);
            line.GrossAmount = line.RateAmount;
            line.TaxAmount = Math.Round(line.GrossAmount * line.TaxRate / 100m, 2);
        }
        finally
        {
            _suppressRecalc = false;
        }
    }

    private void RecalculateTotals()
    {
        var committedLines = Lines.Where(l => !IsBlankLine(l)).ToList();
        TotalQuantity = committedLines.Sum(l => l.Qty);
        GrossTotal = committedLines.Sum(l => l.GrossAmount);
        TaxableAmount = GrossTotal;
        TaxAmount = Math.Round(committedLines.Sum(l => l.TaxAmount), 2, MidpointRounding.AwayFromZero);

        if (Igst == 0m)
        {
            var roundedTax = Math.Round(TaxAmount, 2, MidpointRounding.AwayFromZero);
            Cgst = Math.Round(roundedTax / 2m, 2, MidpointRounding.AwayFromZero);
            Sgst = Math.Round(roundedTax - Cgst, 2, MidpointRounding.AwayFromZero);
        }

        SubTotal = TaxableAmount + Igst + Cgst + Sgst + Exclusive;
        NetTotal = SubTotal + RoundedOff;
    }

    private PurchaseExpenseLine AddBlankLine()
    {
        var line = new PurchaseExpenseLine();
        Lines.Add(line);
        return line;
    }

    private void EnsureBlankLine()
    {
        if (Lines.Count == 0 || !IsBlankLine(Lines.Last()))
        {
            AddBlankLine();
        }
    }

    private void ClearLines()
    {
        foreach (var line in Lines)
            DetachLine(line);

        Lines.Clear();
    }

    private List<PurchaseExpenseLine> GetCommittedLines()
    {
        foreach (var line in Lines)
            RecalculateLine(line);

        return Lines
            .Where(l => !IsBlankLine(l))
            .Select(l => l.Clone())
            .ToList();
    }

    private static bool IsBlankLine(PurchaseExpenseLine line)
    {
        return string.IsNullOrWhiteSpace(line.ItemName)
            && string.IsNullOrWhiteSpace(line.Hsn)
            && line.Qty == 0m
            && line.Rate == 0m
            && line.TaxRate == 0m;
    }

    private void SetSelectedRecentExpense(PurchaseExpense? expense)
    {
        _suppressRecentSync = true;
        try
        {
            SelectedRecentExpense = expense;
        }
        finally
        {
            _suppressRecentSync = false;
        }
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
