using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class SchemeReceiptViewModel : ViewModelBase, IConfirmCloseDocument
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly ISchemeCreationRepository _schemeRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;

    private KarigharFormMode _formMode = KarigharFormMode.View;
    private VoucherEntry? _selectedReceipt;
    private VoucherEntry? _originalReceiptSnapshot;
    private bool _isBusy;
    private bool _isAutoNarrationManual;
    private string _searchText = string.Empty;
    private int _selectedTabIndex;

    // Form fields
    private string _vouNo = string.Empty;
    private DateTime _vouDate = DateTime.Today;
    private int _nos;
    private string _scode = string.Empty;
    private SchemeCreationItem? _selectedSchemeItem;
    private string _crAccount = string.Empty;
    private string _drAccount = string.Empty;
    private string _crBalanceText = "0.00 Cr";
    private string _drBalanceText = "0.00 Dr";
    private decimal _amount;
    private string _paymentMode = "Cash";
    private string _chequeNo = string.Empty;
    private DateTime? _chqDate;
    private string _bankName = string.Empty;
    private string _cardType = string.Empty;
    private decimal _cardComm;
    private decimal _commAmt;
    private decimal _netCredited;
    private int _installmentNo = 1;
    private string _narration = string.Empty;
    private string _status = "Saved";

    // Scheme Summary
    private int _paidInstallmentsCount;
    private decimal _totalPaidAmount;
    private string _schemeSummaryText = string.Empty;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public SchemeReceiptViewModel(
        IVoucherRepository voucherRepository,
        ISchemeCreationRepository schemeRepository,
        IAccountRepository accountRepository,
        ICustomerRepository customerRepository,
        string currentUser = "admin",
        IAuditRepository? auditRepository = null)
    {
        _voucherRepository = voucherRepository;
        _schemeRepository = schemeRepository;
        _accountRepository = accountRepository;
        _customerRepository = customerRepository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;

        NewCommand = new AsyncRelayCommand(NewReceiptAsync, () => CanNew);
        SaveCommand = new AsyncRelayCommand(SaveReceiptAsync, () => CanSave);
        ModifyCommand = new RelayCommand(ModifyReceipt, () => CanModify);
        DeleteCommand = new AsyncRelayCommand(DeleteReceiptAsync, () => CanDelete);
        CancelCommand = new RelayCommand(CancelReceipt, () => CanCancel);
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
        GenerateNarrationCommand = new RelayCommand(GenerateDefaultNarration);
        PrintCommand = new RelayCommand(PrintReceipt, () => CanPrint);
        SmsCommand = new RelayCommand(SendSms, () => CanSms);

        _ = InitializeAsync();
    }

    public event EventHandler? RequestFocusAdd;
    public event EventHandler? RequestFocusFirstInput;

    public string Title => "Scheme Receipts";
    public string HeaderIcon => AppIcons.SchemeReceipts;
    public string CurrentUsername => _currentUser;
    public string CurrentDateText => DateTime.Today.ToString("dd/MM/yyyy");

    public ObservableCollection<SchemeCreationItem> AllSchemes { get; } = [];
    public ObservableCollection<string> AllSchemeCodes { get; } = [];
    public ObservableCollection<string> Accounts { get; } = [];
    public ObservableCollection<string> CashBankAccounts { get; } = [];
    public ObservableCollection<string> BankAccounts { get; } = [];
    public ObservableCollection<string> CashAccounts { get; } = [];
    public ObservableCollection<string> CardTypes { get; } = [];
    public ObservableCollection<string> PaymentModes { get; } = ["Cash", "Cheque", "Online", "Card"];
    public ObservableCollection<int> AvailableInstallmentNumbers { get; } = [];

    public ObservableCollection<VoucherEntry> PastSchemeReceipts { get; } = [];
    public ObservableCollection<VoucherEntry> RecentReceipts { get; } = [];
    public ObservableCollection<VoucherEntry> FilteredReceipts { get; } = [];

    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand ModifyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand GenerateNarrationCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand SmsCommand { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public KarigharFormMode FormMode
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
                RefreshCommands();
            }
        }
    }

    public bool IsFormEnabled => FormMode != KarigharFormMode.View;
    public bool CanNew => FormMode == KarigharFormMode.View && !IsBusy;
    public bool CanSave => FormMode != KarigharFormMode.View && !IsBusy;
    public bool CanModify => FormMode == KarigharFormMode.View && !string.IsNullOrWhiteSpace(VouNo) && !IsBusy;
    public bool CanDelete => FormMode == KarigharFormMode.View && !string.IsNullOrWhiteSpace(VouNo) && !IsBusy;
    public bool CanCancel => FormMode != KarigharFormMode.View && !IsBusy;
    public bool CanPrint => !string.IsNullOrWhiteSpace(VouNo) && Amount > 0 && !IsBusy;
    public bool CanSms => !string.IsNullOrWhiteSpace(VouNo) && !string.IsNullOrWhiteSpace(CrAccount) && !IsBusy;

    public bool ShowBankChequePanel => PaymentMode is "Cheque" or "Online";
    public bool ShowCardPanel => PaymentMode == "Card";

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
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanPrint));
                OnPropertyChanged(nameof(CanSms));
                RefreshCommands();
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
                ApplyFilter();
            }
        }
    }

    public VoucherEntry? SelectedReceipt
    {
        get => _selectedReceipt;
        set
        {
            if (SetProperty(ref _selectedReceipt, value) && value is not null && FormMode == KarigharFormMode.View)
            {
                LoadFromReceipt(value);
            }
        }
    }

    #region Form Fields

    public string VouNo
    {
        get => _vouNo;
        set
        {
            if (SetProperty(ref _vouNo, value))
            {
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(CanPrint));
                OnPropertyChanged(nameof(CanSms));
                RefreshCommands();
            }
        }
    }

    public DateTime VouDate
    {
        get => _vouDate;
        set => SetProperty(ref _vouDate, value);
    }

    public int Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public string Scode
    {
        get => _scode;
        set
        {
            if (SetProperty(ref _scode, value))
            {
                OnScodeChanged(value);
            }
        }
    }

    public SchemeCreationItem? SelectedSchemeItem
    {
        get => _selectedSchemeItem;
        set => SetProperty(ref _selectedSchemeItem, value);
    }

    public string CrAccount
    {
        get => _crAccount;
        set
        {
            if (SetProperty(ref _crAccount, value))
            {
                _ = UpdateCrBalanceAsync(value);
                if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
                {
                    GenerateDefaultNarration();
                }
            }
        }
    }

    public string DrAccount
    {
        get => _drAccount;
        set
        {
            if (SetProperty(ref _drAccount, value))
            {
                _ = UpdateDrBalanceAsync(value);
                if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
                {
                    GenerateDefaultNarration();
                }
            }
        }
    }

    public string CrBalanceText
    {
        get => _crBalanceText;
        set => SetProperty(ref _crBalanceText, value);
    }

    public string DrBalanceText
    {
        get => _drBalanceText;
        set => SetProperty(ref _drBalanceText, value);
    }

    public decimal Amount
    {
        get => _amount;
        set
        {
            if (SetProperty(ref _amount, value))
            {
                RecalculateCardCommission();
                if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
                {
                    GenerateDefaultNarration();
                }
            }
        }
    }

    public string PaymentMode
    {
        get => _paymentMode;
        set
        {
            if (SetProperty(ref _paymentMode, value))
            {
                OnPropertyChanged(nameof(ShowBankChequePanel));
                OnPropertyChanged(nameof(ShowCardPanel));
                UpdateAccountDefaultsForPaymentMode();
                if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
                {
                    GenerateDefaultNarration();
                }
            }
        }
    }

    public string ChequeNo
    {
        get => _chequeNo;
        set
        {
            if (SetProperty(ref _chequeNo, value))
            {
                if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
                {
                    GenerateDefaultNarration();
                }
            }
        }
    }

    public DateTime? ChqDate
    {
        get => _chqDate;
        set
        {
            if (SetProperty(ref _chqDate, value))
            {
                if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
                {
                    GenerateDefaultNarration();
                }
            }
        }
    }

    public string BankName
    {
        get => _bankName;
        set => SetProperty(ref _bankName, value);
    }

    public string CardType
    {
        get => _cardType;
        set => SetProperty(ref _cardType, value);
    }

    public decimal CardComm
    {
        get => _cardComm;
        set
        {
            if (SetProperty(ref _cardComm, value))
            {
                RecalculateCardCommission();
            }
        }
    }

    public decimal CommAmt
    {
        get => _commAmt;
        set => SetProperty(ref _commAmt, value);
    }

    public decimal NetCredited
    {
        get => _netCredited;
        set => SetProperty(ref _netCredited, value);
    }

    public int InstallmentNo
    {
        get => _installmentNo;
        set
        {
            if (SetProperty(ref _installmentNo, value))
            {
                if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
                {
                    GenerateDefaultNarration();
                }
            }
        }
    }

    public string Narration
    {
        get => _narration;
        set
        {
            if (SetProperty(ref _narration, value))
            {
                _isAutoNarrationManual = true;
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public int PaidInstallmentsCount
    {
        get => _paidInstallmentsCount;
        set => SetProperty(ref _paidInstallmentsCount, value);
    }

    public decimal TotalPaidAmount
    {
        get => _totalPaidAmount;
        set => SetProperty(ref _totalPaidAmount, value);
    }

    public string SchemeSummaryText
    {
        get => _schemeSummaryText;
        set => SetProperty(ref _schemeSummaryText, value);
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

    #endregion

    public string? CloseConfirmationMessage =>
        $"You have unsaved changes in Scheme Receipt voucher ({VouNo}).\n\nAre you sure you want to close this tab?";

    public bool HasUnsavedChanges()
    {
        return FormMode != KarigharFormMode.View && (!string.IsNullOrWhiteSpace(Scode) || !string.IsNullOrWhiteSpace(CrAccount) || Amount > 0);
    }

    public async Task InitializeAsync()
    {
        await LoadMasterDataAsync();
        await LoadDataAsync();
    }

    private async Task LoadMasterDataAsync()
    {
        try
        {
            var accs = await _accountRepository.GetAllAsync();
            Accounts.Clear();
            foreach (var a in accs.OrderBy(a => a.AccountName))
            {
                Accounts.Add(a.AccountName);
            }

            var banks = await _voucherRepository.GetBankAccountsAsync();
            BankAccounts.Clear();
            foreach (var b in banks)
            {
                BankAccounts.Add(b);
            }

            var cash = await _voucherRepository.GetCashAccountsAsync();
            CashAccounts.Clear();
            foreach (var c in cash)
            {
                CashAccounts.Add(c);
            }

            var cards = await _voucherRepository.GetCardTypesAsync();
            CardTypes.Clear();
            foreach (var card in cards)
            {
                CardTypes.Add(card);
            }

            CashBankAccounts.Clear();
            foreach (var cb in CashAccounts.Concat(BankAccounts).Distinct())
            {
                CashBankAccounts.Add(cb);
            }

            var schemes = await _schemeRepository.GetAllAsync(500);
            AllSchemes.Clear();
            AllSchemeCodes.Clear();
            foreach (var s in schemes.OrderByDescending(s => s.Nos))
            {
                AllSchemes.Add(s);
                AllSchemeCodes.Add(s.Scode);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load master data: {ex.Message}", ToastType.Error);
        }
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var recent = await _voucherRepository.GetRecentAsync(VoucherType.SchemeEntry, 100);
            RecentReceipts.Clear();
            foreach (var r in recent)
            {
                RecentReceipts.Add(r);
            }
            ApplyFilter();

            if (RecentReceipts.Count > 0 && SelectedReceipt is null)
            {
                SelectedReceipt = RecentReceipts.First();
            }
            else if (RecentReceipts.Count == 0)
            {
                ClearForm();
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load scheme receipts: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredReceipts.Clear();
        var q = _searchText?.Trim() ?? string.Empty;
        var query = string.IsNullOrEmpty(q)
            ? RecentReceipts
            : RecentReceipts.Where(r =>
                r.VouNo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Scode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.CrAccount.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.DrAccount.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.InstallmentNo.ToString().Contains(q) ||
                r.DrAmt.ToString().Contains(q) ||
                r.Narration.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var item in query)
        {
            FilteredReceipts.Add(item);
        }
    }

    private async void OnScodeChanged(string? scode)
    {
        if (string.IsNullOrWhiteSpace(scode))
        {
            SelectedSchemeItem = null;
            PastSchemeReceipts.Clear();
            AvailableInstallmentNumbers.Clear();
            PaidInstallmentsCount = 0;
            TotalPaidAmount = 0m;
            SchemeSummaryText = string.Empty;
            return;
        }

        // Find or load scheme creation record
        var scheme = AllSchemes.FirstOrDefault(s => string.Equals(s.Scode, scode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (scheme is null)
        {
            scheme = await _schemeRepository.GetByScodeAsync(scode.Trim());
            if (scheme is not null)
            {
                AllSchemes.Add(scheme);
            }
        }

        SelectedSchemeItem = scheme;

        if (scheme is not null)
        {
            // Auto-populate customer and installment amount in Add/Modify mode
            if (FormMode != KarigharFormMode.View)
            {
                CrAccount = scheme.CustomerName;
                Amount = scheme.InstallmentAmt;
            }

            // Load past receipts for this scheme
            var pastReceipts = await _voucherRepository.GetByScodeAsync(scheme.Scode);
            PastSchemeReceipts.Clear();
            foreach (var r in pastReceipts)
            {
                PastSchemeReceipts.Add(r);
            }

            PaidInstallmentsCount = PastSchemeReceipts.Count;
            TotalPaidAmount = PastSchemeReceipts.Sum(r => r.DrAmt > 0 ? r.DrAmt : r.CrAmt);

            var totalMonths = (int)scheme.Month;
            if (totalMonths <= 0) totalMonths = 11;

            AvailableInstallmentNumbers.Clear();
            for (var i = 1; i <= Math.Max(totalMonths, PaidInstallmentsCount + 1); i++)
            {
                AvailableInstallmentNumbers.Add(i);
            }

            if (FormMode != KarigharFormMode.View)
            {
                // Auto-suggest next installment number
                var nextInst = PaidInstallmentsCount + 1;
                InstallmentNo = Math.Clamp(nextInst, 1, totalMonths);
            }

            SchemeSummaryText = $"Paid {PaidInstallmentsCount} of {totalMonths} installments (₹{TotalPaidAmount:N2} / ₹{scheme.TotalValue:N2})";
        }
        else
        {
            PastSchemeReceipts.Clear();
            PaidInstallmentsCount = 0;
            TotalPaidAmount = 0m;
            SchemeSummaryText = "Scheme details not found";
        }

        if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
        {
            GenerateDefaultNarration();
        }
    }

    private async Task UpdateCrBalanceAsync(string customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            CrBalanceText = "0.00 Cr";
            return;
        }

        try
        {
            var bal = await _voucherRepository.GetAccountBalanceAsync(customerName);
            // In Indian jewellery accounting: credit balance is positive for customers
            if (bal <= 0)
            {
                CrBalanceText = $"{Math.Abs(bal):0.00} Cr";
            }
            else
            {
                CrBalanceText = $"{bal:0.00} Dr";
            }
        }
        catch
        {
            CrBalanceText = "0.00 Cr";
        }
    }

    private async Task UpdateDrBalanceAsync(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            DrBalanceText = "0.00 Dr";
            return;
        }

        try
        {
            var bal = await _voucherRepository.GetAccountBalanceAsync(accountName);
            if (bal >= 0)
            {
                DrBalanceText = $"{bal:0.00} Dr";
            }
            else
            {
                DrBalanceText = $"{Math.Abs(bal):0.00} Cr";
            }
        }
        catch
        {
            DrBalanceText = "0.00 Dr";
        }
    }

    public async Task SelectReceiptByVouNoAsync(string vouNo)
    {
        if (string.IsNullOrWhiteSpace(vouNo)) return;
        if (RecentReceipts.Count == 0)
        {
            await LoadDataAsync();
        }
        var found = RecentReceipts.FirstOrDefault(r => string.Equals(r.VouNo, vouNo, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            SelectedReceipt = found;
        }
        else
        {
            var r = await _voucherRepository.GetByVouNoAsync(vouNo);
            if (r != null)
            {
                RecentReceipts.Insert(0, r);
                ApplyFilter();
                SelectedReceipt = r;
            }
        }
    }

    private async Task NewReceiptAsync()
    {
        IsBusy = true;
        try
        {
            ClearForm();
            FormMode = KarigharFormMode.Add;
            _isAutoNarrationManual = false;

            Nos = await _voucherRepository.GetNextNosAsync(VoucherType.SchemeEntry);
            VouNo = await _voucherRepository.GetNextVouNoAsync(VoucherType.SchemeEntry);
            VouDate = DateTime.Today;

            DrAccount = CashAccounts.FirstOrDefault() ?? "CASH";
            PaymentMode = "Cash";

            // If active schemes exist, pre-select first active scheme
            if (AllSchemes.Count > 0)
            {
                var activeScheme = AllSchemes.FirstOrDefault(s => !s.Closed) ?? AllSchemes.First();
                Scode = activeScheme.Scode;
            }

            GenerateDefaultNarration();
            ShowToast($"Ready to enter new Scheme Receipt: {VouNo}", ToastType.Info);
            RequestFocusFirstInput?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowToast($"Error initializing new scheme receipt: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyReceipt()
    {
        if (SelectedReceipt is null || string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("Please select a scheme receipt to modify.", ToastType.Warning);
            return;
        }

        FormMode = KarigharFormMode.Modify;
        _isAutoNarrationManual = true;
        ShowToast($"Editing Scheme Receipt {VouNo}", ToastType.Info);
        RequestFocusFirstInput?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveReceiptAsync()
    {
        if (string.IsNullOrWhiteSpace(Scode))
        {
            ShowToast("Please enter or select a Scheme Code (SCode).", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(CrAccount))
        {
            ShowToast("Please enter or select Received From (Customer Name).", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(DrAccount))
        {
            ShowToast("Please enter or select Received By (Cash / Bank Account).", ToastType.Warning);
            return;
        }

        if (Amount <= 0)
        {
            ShowToast("Please enter an amount greater than 0.", ToastType.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Do you want to Save Scheme Receipt {VouNo} for {CrAccount} (₹{Amount:N2})?",
            "Confirm Save",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var receipt = new VoucherEntry
            {
                VouNo = VouNo,
                VouDate = VouDate,
                Nos = Nos,
                VouType = VoucherType.SchemeEntry,
                Scode = Scode.Trim(),
                InstallmentNo = InstallmentNo,
                DrAccount = DrAccount.Trim(),
                DrAmt = Amount,
                CrAccount = CrAccount.Trim(),
                CrAmt = Amount,
                PaymentMode = PaymentMode,
                ChequeNo = ChequeNo?.Trim() ?? string.Empty,
                ChqDt = ChqDate,
                BankName = BankName?.Trim() ?? string.Empty,
                CardType = CardType?.Trim() ?? string.Empty,
                CardComm = CardComm,
                CommAmt = CommAmt,
                NetCredited = NetCredited,
                Narration = Narration?.Trim() ?? string.Empty,
                Narr2 = Narration?.Trim() ?? string.Empty,
                MasterDr = DrAccount.Trim(),
                MasterCr = CrAccount.Trim(),
                Status = "Saved",
                User = _currentUser,
                Date = DateTime.Today,
                Time = DateTime.Now.TimeOfDay,
                Original = true,
                Master = true
            };

            var isUpdate = FormMode == KarigharFormMode.Modify;
            await _voucherRepository.SaveAsync(receipt, isUpdate);

            if (isUpdate && _auditRepository != null)
            {
                await AuditService.LogSchemeReceiptModificationAsync(_auditRepository, _originalReceiptSnapshot, receipt, _currentUser);
            }

            ShowToast($"Scheme Receipt {receipt.VouNo} saved successfully!", ToastType.Success);
            FormMode = KarigharFormMode.View;

            await LoadDataAsync();
            OnScodeChanged(Scode);
            SelectedReceipt = RecentReceipts.FirstOrDefault(r => r.VouNo == receipt.VouNo);
            RequestFocusAdd?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save scheme receipt: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteReceiptAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("No scheme receipt selected to delete.", ToastType.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Are you sure you want to delete Scheme Receipt '{VouNo}' for {CrAccount} (₹{Amount:N2})?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var target = SelectedReceipt ?? _originalReceiptSnapshot;
            await _voucherRepository.DeleteAsync(VouNo);

            if (_auditRepository != null && target != null)
            {
                await AuditService.LogSchemeReceiptDeletionAsync(_auditRepository, target, _currentUser);
            }

            ShowToast($"Scheme Receipt {VouNo} deleted successfully.", ToastType.Success);
            await LoadDataAsync();
            OnScodeChanged(Scode);
            RequestFocusAdd?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to delete scheme receipt: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelReceipt()
    {
        FormMode = KarigharFormMode.View;
        if (SelectedReceipt is not null)
        {
            LoadFromReceipt(SelectedReceipt);
        }
        else
        {
            ClearForm();
        }
        ShowToast("Operation cancelled.", ToastType.Info);
        RequestFocusAdd?.Invoke(this, EventArgs.Empty);
    }

    private void PrintReceipt()
    {
        if (string.IsNullOrWhiteSpace(VouNo) || Amount <= 0)
        {
            ShowToast("No scheme receipt selected to print.", ToastType.Warning);
            return;
        }

        try
        {
            var prepared = Services.FastReportDocumentService.PrepareSchemeReceiptReport(this);
            var previewWindow = new Views.FastReportPreviewWindow(prepared);
            previewWindow.SetOwnerSafely();
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowToast($"Print preview error: {ex.Message}", ToastType.Error);
        }
    }

    private void SendSms()
    {
        if (string.IsNullOrWhiteSpace(CrAccount) || Amount <= 0)
        {
            ShowToast("Select a valid scheme receipt to send SMS.", ToastType.Warning);
            return;
        }

        var msg = $"Dear {CrAccount}, received ₹{Amount:N2} towards Scheme {Scode} (Inst. #{InstallmentNo}) on {VouDate:dd/MM/yyyy}. Thank you!";
        var result = MessageBox.Show(
            $"Send following SMS notification to {CrAccount}?\n\n\"{msg}\"",
            "Send Scheme SMS Notification",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            ShowToast($"SMS notification sent to {CrAccount}.", ToastType.Success);
        }
    }

    private void LoadFromReceipt(VoucherEntry r)
    {
        VouNo = r.VouNo;
        VouDate = r.VouDate;
        Nos = r.Nos;
        _scode = r.Scode;
        OnPropertyChanged(nameof(Scode));
        OnScodeChanged(r.Scode);

        CrAccount = r.CrAccount;
        DrAccount = r.DrAccount;
        Amount = r.DrAmt > 0 ? r.DrAmt : r.CrAmt;
        PaymentMode = r.PaymentMode;
        ChequeNo = r.ChequeNo;
        ChqDate = r.ChqDt;
        BankName = r.BankName;
        CardType = r.CardType;
        CardComm = r.CardComm;
        CommAmt = r.CommAmt;
        NetCredited = r.NetCredited;
        InstallmentNo = r.InstallmentNo > 0 ? r.InstallmentNo : 1;
        _narration = r.Narration;
        OnPropertyChanged(nameof(Narration));
        Status = r.Status;
        _isAutoNarrationManual = true;

        _originalReceiptSnapshot = new VoucherEntry
        {
            VouNo = r.VouNo,
            VouDate = r.VouDate,
            Nos = r.Nos,
            VouType = r.VouType,
            Scode = r.Scode,
            InstallmentNo = r.InstallmentNo,
            DrAccount = r.DrAccount,
            DrAmt = r.DrAmt,
            CrAccount = r.CrAccount,
            CrAmt = r.CrAmt,
            PaymentMode = r.PaymentMode,
            ChequeNo = r.ChequeNo,
            ChqDt = r.ChqDt,
            BankName = r.BankName,
            CardType = r.CardType,
            CardComm = r.CardComm,
            CommAmt = r.CommAmt,
            NetCredited = r.NetCredited,
            Narration = r.Narration,
            Status = r.Status
        };
    }

    private void ClearForm()
    {
        VouNo = string.Empty;
        VouDate = DateTime.Today;
        Nos = 0;
        _scode = string.Empty;
        OnPropertyChanged(nameof(Scode));
        SelectedSchemeItem = null;
        CrAccount = string.Empty;
        DrAccount = CashAccounts.FirstOrDefault() ?? "CASH";
        Amount = 0m;
        PaymentMode = "Cash";
        ChequeNo = string.Empty;
        ChqDate = null;
        BankName = string.Empty;
        CardType = string.Empty;
        CardComm = 0m;
        CommAmt = 0m;
        NetCredited = 0m;
        InstallmentNo = 1;
        _narration = string.Empty;
        OnPropertyChanged(nameof(Narration));
        Status = "Saved";
        PastSchemeReceipts.Clear();
        AvailableInstallmentNumbers.Clear();
        PaidInstallmentsCount = 0;
        TotalPaidAmount = 0m;
        SchemeSummaryText = string.Empty;
    }

    private void UpdateAccountDefaultsForPaymentMode()
    {
        if (FormMode == KarigharFormMode.View)
            return;

        if (PaymentMode is "Cheque" or "Online" or "Card")
        {
            if (string.IsNullOrWhiteSpace(DrAccount) || CashAccounts.Contains(DrAccount))
            {
                DrAccount = BankAccounts.FirstOrDefault() ?? "HDFC BANK -NEW 50200040965163";
            }
        }
        else if (PaymentMode == "Cash")
        {
            if (string.IsNullOrWhiteSpace(DrAccount) || BankAccounts.Contains(DrAccount))
            {
                DrAccount = CashAccounts.FirstOrDefault() ?? "CASH";
            }
        }
    }

    private void RecalculateCardCommission()
    {
        if (CardComm > 0 && Amount > 0)
        {
            CommAmt = Math.Round(Amount * (CardComm / 100m), 2);
            NetCredited = Amount - CommAmt;
        }
        else
        {
            CommAmt = 0m;
            NetCredited = Amount;
        }
    }

    public void GenerateDefaultNarration()
    {
        _isAutoNarrationManual = false;
        if (!string.IsNullOrWhiteSpace(CrAccount))
        {
            var modeDesc = PaymentMode switch
            {
                "Cash" => "Cash",
                "Cheque" => string.IsNullOrWhiteSpace(ChequeNo) ? "Cheque" : $"Cheque No : {ChequeNo} Chq Dated : {(ChqDate.HasValue ? ChqDate.Value.ToString("dd/MM/yyyy") : "")}",
                "Card" => "Card",
                "Online" => "Online Transfer",
                _ => PaymentMode
            };

            var scodeDesc = string.IsNullOrWhiteSpace(Scode) ? string.Empty : $" [{Scode}]";
            var instDesc = InstallmentNo > 0 ? $" Installment No : {InstallmentNo}" : string.Empty;

            _narration = $"Being Amount Received By {modeDesc} From {CrAccount}{scodeDesc}{instDesc}".Trim();
        }
        else
        {
            _narration = "Being Amount Received towards scheme";
        }

        OnPropertyChanged(nameof(Narration));
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }

    private void RefreshCommands()
    {
        NewCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        CommandManager.InvalidateRequerySuggested();
    }
}
