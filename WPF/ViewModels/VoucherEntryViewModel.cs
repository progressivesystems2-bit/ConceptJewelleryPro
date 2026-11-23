using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class VoucherEntryViewModel : ViewModelBase, IConfirmCloseDocument
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly string _vouType;

    private KarigharFormMode _formMode = KarigharFormMode.View;
    private VoucherEntry? _selectedVoucher;
    private VoucherEntry? _originalVoucherSnapshot;
    private bool _isBusy;
    private bool _isAutoNarrationManual;
    private string _searchText = string.Empty;

    // Form fields
    private string _vouNo = string.Empty;
    private DateTime _vouDate = DateTime.Today;
    private int _nos;
    private string _drAccount = string.Empty;
    private string _crAccount = string.Empty;
    private decimal _amount;
    private string _paymentMode = "Cash";
    private string _chequeNo = string.Empty;
    private DateTime? _chqDate;
    private string _bankName = string.Empty;
    private string _cardType = string.Empty;
    private decimal _cardComm;
    private decimal _commAmt;
    private decimal _netCredited;
    private string _narration = string.Empty;
    private string _status = "Saved";

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public VoucherEntryViewModel(
        IVoucherRepository voucherRepository,
        IAccountRepository accountRepository,
        string vouType,
        string currentUser = "admin",
        IAuditRepository? auditRepository = null)
    {
        _voucherRepository = voucherRepository;
        _accountRepository = accountRepository;
        _auditRepository = auditRepository;
        _vouType = string.IsNullOrWhiteSpace(vouType) ? VoucherType.Payments : vouType;
        _currentUser = currentUser;

        NewVoucherCommand = new AsyncRelayCommand(NewVoucherAsync, () => CanNew);
        SaveVoucherCommand = new AsyncRelayCommand(SaveVoucherAsync, () => CanSave);
        ModifyVoucherCommand = new RelayCommand(ModifyVoucher, () => CanModify);
        DeleteVoucherCommand = new AsyncRelayCommand(DeleteVoucherAsync, () => CanDelete);
        CancelVoucherCommand = new RelayCommand(CancelVoucher, () => CanCancel);
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
        GenerateNarrationCommand = new RelayCommand(GenerateDefaultNarration);
        PrintVoucherCommand = new RelayCommand(PrintVoucher, () => CanPrint);

        _ = InitializeAsync();
    }

    public event EventHandler? RequestFocusAdd;
    public event EventHandler? RequestFocusFirstInput;

    public string VouType => _vouType;

    public string Title => _vouType switch
    {
        VoucherType.Payments => "Payments",
        VoucherType.Receipts or "RECEIPTS" => "Receipts",
        VoucherType.Journal => "Journal",
        _ => "Voucher Entry"
    };

    public string CurrentUsername => _currentUser;

    public string AuditEntryType => _vouType switch
    {
        VoucherType.Payments => "Payment Entry",
        VoucherType.Receipts or "RECEIPTS" => "Receipt Entry",
        VoucherType.Journal => "Journal Entry",
        _ => $"{Title} Entry"
    };

    public string HeaderIcon => _vouType switch
    {
        VoucherType.Payments => AppIcons.Payments,
        VoucherType.Receipts or "RECEIPTS" => AppIcons.Receipts,
        VoucherType.Journal => AppIcons.Journal,
        _ => AppIcons.Vouchers
    };

    public string DrAccountLabel => _vouType switch
    {
        VoucherType.Payments => "Debit Account (Party / Expense)",
        VoucherType.Receipts or "RECEIPTS" => "Debit Account (Cash / Bank)",
        _ => "Debit Account (Dr)"
    };

    public string CrAccountLabel => _vouType switch
    {
        VoucherType.Payments => "Credit Account (Cash / Bank)",
        VoucherType.Receipts or "RECEIPTS" => "Credit Account (Customer / Party)",
        _ => "Credit Account (Cr)"
    };

    public bool IsJournal => _vouType == VoucherType.Journal;
    public bool ShowBankChequePanel => !IsJournal && (PaymentMode is "Cheque" or "Online");
    public bool ShowCardPanel => !IsJournal && PaymentMode == "Card";

    public ObservableCollection<string> Accounts { get; } = [];
    public ObservableCollection<string> CashBankAccounts { get; } = [];
    public ObservableCollection<string> DrAccounts { get; } = [];
    public ObservableCollection<string> CrAccounts { get; } = [];
    public ObservableCollection<string> BankAccounts { get; } = [];
    public ObservableCollection<string> CashAccounts { get; } = [];
    public ObservableCollection<string> CardTypes { get; } = [];
    public ObservableCollection<string> PaymentModes { get; } = ["Cash", "Cheque", "Card", "Online"];

    public ObservableCollection<VoucherEntry> RecentVouchers { get; } = [];
    public ObservableCollection<VoucherEntry> FilteredVouchers { get; } = [];

    public AsyncRelayCommand NewVoucherCommand { get; }
    public AsyncRelayCommand SaveVoucherCommand { get; }
    public RelayCommand ModifyVoucherCommand { get; }
    public AsyncRelayCommand DeleteVoucherCommand { get; }
    public RelayCommand CancelVoucherCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand GenerateNarrationCommand { get; }
    public RelayCommand PrintVoucherCommand { get; }

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

    public VoucherEntry? SelectedVoucher
    {
        get => _selectedVoucher;
        set
        {
            if (SetProperty(ref _selectedVoucher, value) && value is not null && FormMode == KarigharFormMode.View)
            {
                LoadFromVoucher(value);
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
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
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

    public string DrAccount
    {
        get => _drAccount;
        set
        {
            if (SetProperty(ref _drAccount, value))
            {
                if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
                {
                    GenerateDefaultNarration();
                }
            }
        }
    }

    public string CrAccount
    {
        get => _crAccount;
        set
        {
            if (SetProperty(ref _crAccount, value))
            {
                if (!_isAutoNarrationManual && FormMode != KarigharFormMode.View)
                {
                    GenerateDefaultNarration();
                }
            }
        }
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
        set => SetProperty(ref _chequeNo, value);
    }

    public DateTime? ChqDate
    {
        get => _chqDate;
        set => SetProperty(ref _chqDate, value);
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

    public string? CloseConfirmationMessage =>
        $"You have unsaved changes in the {Title} voucher ({VouNo}).\n\nAre you sure you want to close this tab?";

    public bool HasUnsavedChanges()
    {
        return FormMode != KarigharFormMode.View && (!string.IsNullOrWhiteSpace(DrAccount) || !string.IsNullOrWhiteSpace(CrAccount) || Amount > 0);
    }

    private async Task InitializeAsync()
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

            // Populate CashBankAccounts: where HeadCategory / HeadGroup is 'CASH IN HAND' or 'Bank A/c' / 'Bank A/C'
            var cashBankList = accs
                .Where(a => IsCashOrBankHeadGroup(a))
                .Select(a => a.AccountName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            // Ensure bank & cash accounts discovered from repository are also present
            foreach (var b in BankAccounts.Concat(CashAccounts))
            {
                if (!cashBankList.Contains(b, StringComparer.OrdinalIgnoreCase))
                {
                    cashBankList.Add(b);
                }
            }

            CashBankAccounts.Clear();
            foreach (var cb in cashBankList.OrderBy(x => x))
            {
                CashBankAccounts.Add(cb);
            }

            // Assign DrAccounts and CrAccounts based on Voucher Type:
            // 1. Payments: Debit = all accounts (Party/Expenses), Credit = CASH IN HAND / Bank A/C
            // 2. Receipts: Debit = CASH IN HAND / Bank A/C, Credit = all accounts (Customer/Party)
            // 3. Journal: Debit & Credit = all accounts
            DrAccounts.Clear();
            CrAccounts.Clear();

            if (_vouType == VoucherType.Payments)
            {
                foreach (var a in Accounts) DrAccounts.Add(a);
                foreach (var cb in CashBankAccounts) CrAccounts.Add(cb);
            }
            else if (_vouType == VoucherType.Receipts || _vouType == "RECEIPTS")
            {
                foreach (var cb in CashBankAccounts) DrAccounts.Add(cb);
                foreach (var a in Accounts) CrAccounts.Add(a);
            }
            else // Journal
            {
                foreach (var a in Accounts) DrAccounts.Add(a);
                foreach (var a in Accounts) CrAccounts.Add(a);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load master accounts: {ex.Message}", ToastType.Error);
        }
    }

    private static bool IsCashOrBankHeadGroup(Account a)
    {
        var head = a.HeadCategory?.Trim() ?? string.Empty;
        var sub = a.SubCategory?.Trim() ?? string.Empty;
        var name = a.AccountName?.Trim() ?? string.Empty;

        return head.Equals("CASH IN HAND", StringComparison.OrdinalIgnoreCase) ||
               head.Equals("Cash in Hand", StringComparison.OrdinalIgnoreCase) ||
               head.Equals("Cash", StringComparison.OrdinalIgnoreCase) ||
               head.Equals("Bank A/c", StringComparison.OrdinalIgnoreCase) ||
               head.Equals("Bank A/C", StringComparison.OrdinalIgnoreCase) ||
               head.Equals("Bank Accounts", StringComparison.OrdinalIgnoreCase) ||
               head.Equals("BANK", StringComparison.OrdinalIgnoreCase) ||
               sub.Equals("CASH", StringComparison.OrdinalIgnoreCase) ||
               sub.Equals("BANK", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("CASH", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("BANK", StringComparison.OrdinalIgnoreCase);
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var recent = await _voucherRepository.GetRecentAsync(_vouType, 100);
            RecentVouchers.Clear();
            foreach (var v in recent)
            {
                RecentVouchers.Add(v);
            }
            ApplyFilter();

            if (RecentVouchers.Count > 0 && SelectedVoucher is null)
            {
                SelectedVoucher = RecentVouchers.First();
            }
            else if (RecentVouchers.Count == 0)
            {
                ClearForm();
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load vouchers: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectVoucherByVouNoAsync(string vouNo)
    {
        if (string.IsNullOrWhiteSpace(vouNo)) return;
        if (RecentVouchers.Count == 0)
        {
            await LoadDataAsync();
        }
        var found = RecentVouchers.FirstOrDefault(v => string.Equals(v.VouNo, vouNo, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            SelectedVoucher = found;
        }
        else
        {
            var v = await _voucherRepository.GetByVouNoAsync(vouNo);
            if (v != null)
            {
                RecentVouchers.Insert(0, v);
                ApplyFilter();
                SelectedVoucher = v;
            }
        }
    }

    private void ApplyFilter()
    {
        FilteredVouchers.Clear();
        var q = _searchText?.Trim() ?? string.Empty;
        var query = string.IsNullOrEmpty(q)
            ? RecentVouchers
            : RecentVouchers.Where(v =>
                v.VouNo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                v.DrAccount.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                v.CrAccount.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                v.Narration.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                v.DrAmt.ToString().Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var item in query)
        {
            FilteredVouchers.Add(item);
        }
    }

    private async Task NewVoucherAsync()
    {
        IsBusy = true;
        try
        {
            ClearForm();
            FormMode = KarigharFormMode.Add;
            _isAutoNarrationManual = false;

            Nos = await _voucherRepository.GetNextNosAsync(_vouType);
            VouNo = await _voucherRepository.GetNextVouNoAsync(_vouType);
            VouDate = DateTime.Today;

            // Apply defaults based on voucher type
            if (_vouType == VoucherType.Payments)
            {
                CrAccount = CashAccounts.FirstOrDefault() ?? "CASH";
                PaymentMode = "Cash";
            }
            else if (_vouType == VoucherType.Receipts)
            {
                DrAccount = CashAccounts.FirstOrDefault() ?? "CASH";
                PaymentMode = "Cash";
            }

            GenerateDefaultNarration();
            ShowToast($"Ready to enter new {Title} voucher: {VouNo}", ToastType.Info);
            RequestFocusFirstInput?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowToast($"Error initializing new voucher: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyVoucher()
    {
        if (SelectedVoucher is null || string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("Please select a voucher to modify.", ToastType.Warning);
            return;
        }

        FormMode = KarigharFormMode.Modify;
        _isAutoNarrationManual = true;
        ShowToast($"Editing voucher {VouNo}", ToastType.Info);
        RequestFocusFirstInput?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveVoucherAsync()
    {
        if (string.IsNullOrWhiteSpace(DrAccount))
        {
            ShowToast($"Please enter/select {DrAccountLabel}.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(CrAccount))
        {
            ShowToast($"Please enter/select {CrAccountLabel}.", ToastType.Warning);
            return;
        }

        if (Amount <= 0)
        {
            ShowToast("Please enter an amount greater than 0.", ToastType.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            "Do you want to Save?",
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
            var voucher = new VoucherEntry
            {
                VouNo = VouNo,
                VouDate = VouDate,
                Nos = Nos,
                VouType = _vouType,
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
            await _voucherRepository.SaveAsync(voucher, isUpdate);

            if (isUpdate && _auditRepository != null)
            {
                await AuditService.LogVoucherModificationAsync(_auditRepository, _originalVoucherSnapshot, voucher, AuditEntryType, _currentUser);
            }

            ShowToast($"Voucher {voucher.VouNo} saved successfully!", ToastType.Success);
            FormMode = KarigharFormMode.View;

            await LoadDataAsync();
            SelectedVoucher = RecentVouchers.FirstOrDefault(v => v.VouNo == voucher.VouNo);
            RequestFocusAdd?.Invoke(this, EventArgs.Empty);
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

    private void PrintVoucher()
    {
        if (string.IsNullOrWhiteSpace(VouNo) || Amount <= 0)
        {
            ShowToast("No voucher selected to print.", ToastType.Warning);
            return;
        }

        try
        {
            if (!IsJournal)
            {
                var prepared = Services.FastReportDocumentService.PrepareVoucherReport(this);
                var previewWindow = new Views.FastReportPreviewWindow(prepared);
                previewWindow.SetOwnerSafely();
                previewWindow.ShowDialog();
            }
            else
            {
                var doc = Services.VoucherFlowDocumentBuilder.CreateVoucherDocument(this);
                var previewWindow = new Views.InvoicePrintPreviewWindow(doc)
                {
                    Title = $"{Title} Voucher Print Preview - {VouNo}"
                };
                previewWindow.SetOwnerSafely();
                previewWindow.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Print preview error: {ex.Message}", ToastType.Error);
        }
    }

    private async Task DeleteVoucherAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("No voucher selected to delete.", ToastType.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Are you sure you want to delete voucher '{VouNo}' ({Amount:N2})?",
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
            var target = SelectedVoucher ?? _originalVoucherSnapshot;
            await _voucherRepository.DeleteAsync(VouNo);

            if (_auditRepository != null && target != null)
            {
                await AuditService.LogVoucherDeletionAsync(_auditRepository, target, AuditEntryType, _currentUser);
            }

            ShowToast($"Voucher {VouNo} deleted successfully.", ToastType.Success);
            await LoadDataAsync();
            RequestFocusAdd?.Invoke(this, EventArgs.Empty);
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
        FormMode = KarigharFormMode.View;
        if (SelectedVoucher is not null)
        {
            LoadFromVoucher(SelectedVoucher);
        }
        else
        {
            ClearForm();
        }
        ShowToast("Operation cancelled.", ToastType.Info);
        RequestFocusAdd?.Invoke(this, EventArgs.Empty);
    }

    private void LoadFromVoucher(VoucherEntry v)
    {
        VouNo = v.VouNo;
        VouDate = v.VouDate;
        Nos = v.Nos;
        DrAccount = v.DrAccount;
        CrAccount = v.CrAccount;
        Amount = v.DrAmt > 0 ? v.DrAmt : v.CrAmt;
        PaymentMode = v.PaymentMode;
        ChequeNo = v.ChequeNo;
        ChqDate = v.ChqDt;
        BankName = v.BankName;
        CardType = v.CardType;
        CardComm = v.CardComm;
        CommAmt = v.CommAmt;
        NetCredited = v.NetCredited;
        _narration = v.Narration;
        OnPropertyChanged(nameof(Narration));
        Status = v.Status;
        _isAutoNarrationManual = true;

        _originalVoucherSnapshot = new VoucherEntry
        {
            VouNo = v.VouNo,
            VouDate = v.VouDate,
            Nos = v.Nos,
            VouType = v.VouType,
            DrAccount = v.DrAccount,
            DrAmt = v.DrAmt,
            CrAccount = v.CrAccount,
            CrAmt = v.CrAmt,
            PaymentMode = v.PaymentMode,
            ChequeNo = v.ChequeNo,
            ChqDt = v.ChqDt,
            BankName = v.BankName,
            CardType = v.CardType,
            CardComm = v.CardComm,
            CommAmt = v.CommAmt,
            NetCredited = v.NetCredited,
            Narration = v.Narration,
            Status = v.Status
        };
    }

    private void ClearForm()
    {
        VouNo = string.Empty;
        VouDate = DateTime.Today;
        Nos = 0;
        DrAccount = string.Empty;
        CrAccount = string.Empty;
        Amount = 0m;
        PaymentMode = "Cash";
        ChequeNo = string.Empty;
        ChqDate = null;
        BankName = string.Empty;
        CardType = string.Empty;
        CardComm = 0m;
        CommAmt = 0m;
        NetCredited = 0m;
        _narration = string.Empty;
        OnPropertyChanged(nameof(Narration));
        Status = "Saved";
    }

    private void UpdateAccountDefaultsForPaymentMode()
    {
        if (FormMode == KarigharFormMode.View)
            return;

        if (_vouType == VoucherType.Payments)
        {
            if (PaymentMode is "Cheque" or "Online" or "Card")
            {
                if (string.IsNullOrWhiteSpace(CrAccount) || CashAccounts.Contains(CrAccount))
                {
                    CrAccount = BankAccounts.FirstOrDefault() ?? "HDFC BANK -NEW 50200040965163";
                }
            }
            else if (PaymentMode == "Cash")
            {
                if (string.IsNullOrWhiteSpace(CrAccount) || BankAccounts.Contains(CrAccount))
                {
                    CrAccount = CashAccounts.FirstOrDefault() ?? "CASH";
                }
            }
        }
        else if (_vouType == VoucherType.Receipts)
        {
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
        if (_vouType == VoucherType.Payments)
        {
            if (!string.IsNullOrWhiteSpace(DrAccount))
            {
                var modeDesc = PaymentMode switch
                {
                    "Cash" => "CASH",
                    "Cheque" => string.IsNullOrWhiteSpace(ChequeNo) ? "CHEQUE" : $"CHEQUE NO : {ChequeNo}",
                    "Card" => "CARD",
                    "Online" => "ONLINE / NEFT",
                    _ => PaymentMode.ToUpperInvariant()
                };
                _narration = $"BEING {modeDesc} PAID TO {DrAccount}";
            }
            else
            {
                _narration = "BEING PAYMENT PAID";
            }
        }
        else if (_vouType == VoucherType.Receipts)
        {
            if (!string.IsNullOrWhiteSpace(CrAccount))
            {
                var modeDesc = PaymentMode switch
                {
                    "Cash" => "Cash",
                    "Cheque" => string.IsNullOrWhiteSpace(ChequeNo) ? "Cheque" : $"Cheque No : {ChequeNo}",
                    "Card" => "Card",
                    "Online" => "Online Transfer",
                    _ => PaymentMode
                };
                _narration = $"Being Amount Received By {modeDesc} From {CrAccount}";
            }
            else
            {
                _narration = "Being Amount Received";
            }
        }
        else if (_vouType == VoucherType.Journal)
        {
            if (!string.IsNullOrWhiteSpace(DrAccount) && !string.IsNullOrWhiteSpace(CrAccount))
            {
                _narration = $"Being adjustment entry between {DrAccount} and {CrAccount}";
            }
            else
            {
                _narration = "Being journal adjustment entry";
            }
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
        NewVoucherCommand.RaiseCanExecuteChanged();
        SaveVoucherCommand.RaiseCanExecuteChanged();
        DeleteVoucherCommand.RaiseCanExecuteChanged();
        CommandManager.InvalidateRequerySuggested();
    }
}
