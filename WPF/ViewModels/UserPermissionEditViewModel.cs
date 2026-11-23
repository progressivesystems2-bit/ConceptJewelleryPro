using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class UserPermissionEditViewModel : ViewModelBase
{
    private readonly IUserPermissionRepository _userPermissionRepository;
    private readonly string _currentUser;
    private readonly string? _originalUsername;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _toolbar = "DP1";
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private string _errorMessage = string.Empty;

    // --- Entries Permissions ---
    private bool _saleAdd, _saleMod, _saleDel;
    private bool _ledAdd, _ledMod, _ledDel;
    private bool _itemAdd, _itemMod, _itemDel;
    private bool _barAdd, _barMod, _barDel;
    private bool _bcAdd, _bcMod, _bcDel;
    private bool _retAdd, _retMod, _retDel;
    private bool _purAdd, _purMod, _purDel;
    private bool _recAdd, _recMod, _recDel;
    private bool _cusAdd, _cusMod, _cusDel;
    private bool _payAdd, _payMod, _payDel;
    private bool _jouAdd, _jouMod, _jouDel;
    private bool _mIssAdd, _mIssMod, _mIssDel;
    private bool _mRecAdd, _mRecMod, _mRecDel;
    private bool _aIssAdd, _aIssMod, _aIssDel;
    private bool _aRecAdd, _aRecMod, _aRecDel;
    private bool _hIssAdd, _hIssMod, _hIssDel;
    private bool _hRecAdd, _hRecMod, _hRecDel;
    private bool _userAdd, _userMod, _userDel;
    private bool _repAdd, _repMod, _repDel;
    private bool _scAdd, _scMod, _scDel;
    private bool _staffAdd, _staffMod, _staffDel;
    private bool _ordAdd, _ordMod, _ordDel;
    private bool _bhavAdd, _bhavMod, _bhavDel;
    private bool _noteTransfer, _noteTransferMod, _noteTransferDel;

    // --- Reports Permissions ---
    private bool _salesRegister, _itemWiseSales, _vatRegister, _hidePartyDetails;
    private bool _purchaseRegister, _barcodeStock, _rateChart, _labelPrinting;
    private bool _ledgerIndividual, _ledgerGroup, _metalWiseLedger, _karigarLedger;
    private bool _trialBalance, _openingTrial, _trading, _profitLoss, _balanceSheet;
    private bool _cashBook, _cashBookOpening;
    private bool _auditView, _auditDel, _attendance;

    public event EventHandler<bool?>? RequestClose;

    public UserPermissionEditViewModel(
        IUserPermissionRepository userPermissionRepository,
        UserPermission? existingUser = null,
        string currentUser = "admin")
    {
        _userPermissionRepository = userPermissionRepository;
        _currentUser = currentUser;

        if (existingUser is not null)
        {
            _originalUsername = existingUser.User;
            _username = existingUser.User;
            _password = existingUser.Password;
            _toolbar = string.IsNullOrWhiteSpace(existingUser.Toolbar) ? "DP1" : existingUser.Toolbar;
            Title = $"Modify User Permissions - {existingUser.User}";
            IsNew = false;

            // Load Entries
            _saleAdd = existingUser.SaleAdd; _saleMod = existingUser.SaleMod; _saleDel = existingUser.SaleDel;
            _ledAdd = existingUser.LedAdd; _ledMod = existingUser.LedMod; _ledDel = existingUser.LedDel;
            _itemAdd = existingUser.ItemAdd; _itemMod = existingUser.ItemMod; _itemDel = existingUser.ItemDel;
            _barAdd = existingUser.BarAdd; _barMod = existingUser.BarMod; _barDel = existingUser.BarDel;
            _bcAdd = existingUser.BcAdd; _bcMod = existingUser.BcMod; _bcDel = existingUser.BcDel;
            _retAdd = existingUser.RetAdd; _retMod = existingUser.RetMod; _retDel = existingUser.RetDel;
            _purAdd = existingUser.PurAdd; _purMod = existingUser.PurMod; _purDel = existingUser.PurDel;
            _recAdd = existingUser.RecAdd; _recMod = existingUser.RecMod; _recDel = existingUser.RecDel;
            _cusAdd = existingUser.CusAdd; _cusMod = existingUser.CusMod; _cusDel = existingUser.CusDel;
            _payAdd = existingUser.PayAdd; _payMod = existingUser.PayMod; _payDel = existingUser.PayDel;
            _jouAdd = existingUser.JouAdd; _jouMod = existingUser.JouMod; _jouDel = existingUser.JouDel;
            _mIssAdd = existingUser.MIssAdd; _mIssMod = existingUser.MIssMod; _mIssDel = existingUser.MIssDel;
            _mRecAdd = existingUser.MRecAdd; _mRecMod = existingUser.MRecMod; _mRecDel = existingUser.MRecDel;
            _aIssAdd = existingUser.AIssAdd; _aIssMod = existingUser.AIssMod; _aIssDel = existingUser.AIssDel;
            _aRecAdd = existingUser.ARecAdd; _aRecMod = existingUser.ARecMod; _aRecDel = existingUser.ARecDel;
            _hIssAdd = existingUser.HIssAdd; _hIssMod = existingUser.HIssMod; _hIssDel = existingUser.HIssDel;
            _hRecAdd = existingUser.HRecAdd; _hRecMod = existingUser.HRecMod; _hRecDel = existingUser.HRecDel;
            _userAdd = existingUser.UserAdd; _userMod = existingUser.UserMod; _userDel = existingUser.UserDel;
            _repAdd = existingUser.RepAdd; _repMod = existingUser.RepMod; _repDel = existingUser.RepDel;
            _scAdd = existingUser.ScAdd; _scMod = existingUser.ScMod; _scDel = existingUser.ScDel;
            _staffAdd = existingUser.StaffAdd; _staffMod = existingUser.StaffMod; _staffDel = existingUser.StaffDel;
            _ordAdd = existingUser.OrdAdd; _ordMod = existingUser.OrdMod; _ordDel = existingUser.OrdDel;
            _bhavAdd = existingUser.BhavAdd; _bhavMod = existingUser.BhavMod; _bhavDel = existingUser.BhavDel;
            _noteTransfer = existingUser.NoteTransfer; _noteTransferMod = existingUser.NoteTransferMod; _noteTransferDel = existingUser.NoteTransferDel;

            // Load Reports
            _salesRegister = existingUser.SalesRegister; _itemWiseSales = existingUser.ItemWiseSales;
            _vatRegister = existingUser.VatRegister; _hidePartyDetails = existingUser.HidePartyDetails;
            _purchaseRegister = existingUser.PurchaseRegister; _barcodeStock = existingUser.BarcodeStock;
            _rateChart = existingUser.RateChart; _labelPrinting = existingUser.LabelPrinting;
            _ledgerIndividual = existingUser.LedgerIndividual; _ledgerGroup = existingUser.LedgerGroup;
            _metalWiseLedger = existingUser.MetalWiseLedger; _karigarLedger = existingUser.KarigarLedger;
            _trialBalance = existingUser.TrialBalance; _openingTrial = existingUser.OpeningTrial;
            _trading = existingUser.Trading; _profitLoss = existingUser.ProfitLoss;
            _balanceSheet = existingUser.BalanceSheet; _cashBook = existingUser.CashBook;
            _cashBookOpening = existingUser.CashBookOpening; _auditView = existingUser.AuditView;
            _auditDel = existingUser.AuditDel; _attendance = existingUser.Attendance;
        }
        else
        {
            Title = "Add New User";
            IsNew = true;
            _toolbar = "DP1";
        }

        SaveCommand = new RelayCommand(async () => await SaveAsync());
        CancelCommand = new RelayCommand(Cancel);

        SelectAllEntriesCommand = new RelayCommand(() => SetAllEntries(true));
        UnselectAllEntriesCommand = new RelayCommand(() => SetAllEntries(false));

        SelectAllReportsCommand = new RelayCommand(() => SetAllReports(true));
        UnselectAllReportsCommand = new RelayCommand(() => SetAllReports(false));

        GrantAllCommand = new RelayCommand(() => { SetAllEntries(true); SetAllReports(true); });
        RevokeAllCommand = new RelayCommand(() => { SetAllEntries(false); SetAllReports(false); });
    }

    public string Title { get; }
    public bool IsNew { get; }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllEntriesCommand { get; }
    public ICommand UnselectAllEntriesCommand { get; }
    public ICommand SelectAllReportsCommand { get; }
    public ICommand UnselectAllReportsCommand { get; }
    public ICommand GrantAllCommand { get; }
    public ICommand RevokeAllCommand { get; }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string Toolbar
    {
        get => _toolbar;
        set => SetProperty(ref _toolbar, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? ToastMessage
    {
        get => _toastMessage;
        private set => SetProperty(ref _toastMessage, value);
    }

    public ToastType ToastType
    {
        get => _toastType;
        private set => SetProperty(ref _toastType, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value) && !string.IsNullOrWhiteSpace(value))
            {
                ToastType = ToastType.Error;
                ToastMessage = string.Empty;
                ToastMessage = value;
            }
        }
    }

    // --- Entries Properties ---
    public bool SaleAdd { get => _saleAdd; set => SetProperty(ref _saleAdd, value); }
    public bool SaleMod { get => _saleMod; set => SetProperty(ref _saleMod, value); }
    public bool SaleDel { get => _saleDel; set => SetProperty(ref _saleDel, value); }

    public bool LedAdd { get => _ledAdd; set => SetProperty(ref _ledAdd, value); }
    public bool LedMod { get => _ledMod; set => SetProperty(ref _ledMod, value); }
    public bool LedDel { get => _ledDel; set => SetProperty(ref _ledDel, value); }

    public bool ItemAdd { get => _itemAdd; set => SetProperty(ref _itemAdd, value); }
    public bool ItemMod { get => _itemMod; set => SetProperty(ref _itemMod, value); }
    public bool ItemDel { get => _itemDel; set => SetProperty(ref _itemDel, value); }

    public bool BarAdd { get => _barAdd; set => SetProperty(ref _barAdd, value); }
    public bool BarMod { get => _barMod; set => SetProperty(ref _barMod, value); }
    public bool BarDel { get => _barDel; set => SetProperty(ref _barDel, value); }

    public bool BcAdd { get => _bcAdd; set => SetProperty(ref _bcAdd, value); }
    public bool BcMod { get => _bcMod; set => SetProperty(ref _bcMod, value); }
    public bool BcDel { get => _bcDel; set => SetProperty(ref _bcDel, value); }

    public bool RetAdd { get => _retAdd; set => SetProperty(ref _retAdd, value); }
    public bool RetMod { get => _retMod; set => SetProperty(ref _retMod, value); }
    public bool RetDel { get => _retDel; set => SetProperty(ref _retDel, value); }

    public bool PurAdd { get => _purAdd; set => SetProperty(ref _purAdd, value); }
    public bool PurMod { get => _purMod; set => SetProperty(ref _purMod, value); }
    public bool PurDel { get => _purDel; set => SetProperty(ref _purDel, value); }

    public bool RecAdd { get => _recAdd; set => SetProperty(ref _recAdd, value); }
    public bool RecMod { get => _recMod; set => SetProperty(ref _recMod, value); }
    public bool RecDel { get => _recDel; set => SetProperty(ref _recDel, value); }

    public bool CusAdd { get => _cusAdd; set => SetProperty(ref _cusAdd, value); }
    public bool CusMod { get => _cusMod; set => SetProperty(ref _cusMod, value); }
    public bool CusDel { get => _cusDel; set => SetProperty(ref _cusDel, value); }

    public bool PayAdd { get => _payAdd; set => SetProperty(ref _payAdd, value); }
    public bool PayMod { get => _payMod; set => SetProperty(ref _payMod, value); }
    public bool PayDel { get => _payDel; set => SetProperty(ref _payDel, value); }

    public bool JouAdd { get => _jouAdd; set => SetProperty(ref _jouAdd, value); }
    public bool JouMod { get => _jouMod; set => SetProperty(ref _jouMod, value); }
    public bool JouDel { get => _jouDel; set => SetProperty(ref _jouDel, value); }

    public bool MIssAdd { get => _mIssAdd; set => SetProperty(ref _mIssAdd, value); }
    public bool MIssMod { get => _mIssMod; set => SetProperty(ref _mIssMod, value); }
    public bool MIssDel { get => _mIssDel; set => SetProperty(ref _mIssDel, value); }

    public bool MRecAdd { get => _mRecAdd; set => SetProperty(ref _mRecAdd, value); }
    public bool MRecMod { get => _mRecMod; set => SetProperty(ref _mRecMod, value); }
    public bool MRecDel { get => _mRecDel; set => SetProperty(ref _mRecDel, value); }

    public bool AIssAdd { get => _aIssAdd; set => SetProperty(ref _aIssAdd, value); }
    public bool AIssMod { get => _aIssMod; set => SetProperty(ref _aIssMod, value); }
    public bool AIssDel { get => _aIssDel; set => SetProperty(ref _aIssDel, value); }

    public bool ARecAdd { get => _aRecAdd; set => SetProperty(ref _aRecMod, value); }
    public bool ARecMod { get => _aRecMod; set => SetProperty(ref _aRecMod, value); }
    public bool ARecDel { get => _aRecDel; set => SetProperty(ref _aRecDel, value); }

    public bool HIssAdd { get => _hIssAdd; set => SetProperty(ref _hIssAdd, value); }
    public bool HIssMod { get => _hIssMod; set => SetProperty(ref _hIssMod, value); }
    public bool HIssDel { get => _hIssDel; set => SetProperty(ref _hIssDel, value); }

    public bool HRecAdd { get => _hRecAdd; set => SetProperty(ref _hRecAdd, value); }
    public bool HRecMod { get => _hRecMod; set => SetProperty(ref _hRecMod, value); }
    public bool HRecDel { get => _hRecDel; set => SetProperty(ref _hRecDel, value); }

    public bool UserAdd { get => _userAdd; set => SetProperty(ref _userAdd, value); }
    public bool UserMod { get => _userMod; set => SetProperty(ref _userMod, value); }
    public bool UserDel { get => _userDel; set => SetProperty(ref _userDel, value); }

    public bool RepAdd { get => _repAdd; set => SetProperty(ref _repAdd, value); }
    public bool RepMod { get => _repMod; set => SetProperty(ref _repMod, value); }
    public bool RepDel { get => _repDel; set => SetProperty(ref _repDel, value); }

    public bool ScAdd { get => _scAdd; set => SetProperty(ref _scAdd, value); }
    public bool ScMod { get => _scMod; set => SetProperty(ref _scMod, value); }
    public bool ScDel { get => _scDel; set => SetProperty(ref _scDel, value); }

    public bool StaffAdd { get => _staffAdd; set => SetProperty(ref _staffAdd, value); }
    public bool StaffMod { get => _staffMod; set => SetProperty(ref _staffMod, value); }
    public bool StaffDel { get => _staffDel; set => SetProperty(ref _staffDel, value); }

    public bool OrdAdd { get => _ordAdd; set => SetProperty(ref _ordAdd, value); }
    public bool OrdMod { get => _ordMod; set => SetProperty(ref _ordMod, value); }
    public bool OrdDel { get => _ordDel; set => SetProperty(ref _ordDel, value); }

    public bool BhavAdd { get => _bhavAdd; set => SetProperty(ref _bhavAdd, value); }
    public bool BhavMod { get => _bhavMod; set => SetProperty(ref _bhavMod, value); }
    public bool BhavDel { get => _bhavDel; set => SetProperty(ref _bhavDel, value); }

    public bool NoteTransfer { get => _noteTransfer; set => SetProperty(ref _noteTransfer, value); }
    public bool NoteTransferMod { get => _noteTransferMod; set => SetProperty(ref _noteTransferMod, value); }
    public bool NoteTransferDel { get => _noteTransferDel; set => SetProperty(ref _noteTransferDel, value); }

    // --- Reports Properties ---
    public bool SalesRegister { get => _salesRegister; set => SetProperty(ref _salesRegister, value); }
    public bool ItemWiseSales { get => _itemWiseSales; set => SetProperty(ref _itemWiseSales, value); }
    public bool VatRegister { get => _vatRegister; set => SetProperty(ref _vatRegister, value); }
    public bool HidePartyDetails { get => _hidePartyDetails; set => SetProperty(ref _hidePartyDetails, value); }

    public bool PurchaseRegister { get => _purchaseRegister; set => SetProperty(ref _purchaseRegister, value); }
    public bool BarcodeStock { get => _barcodeStock; set => SetProperty(ref _barcodeStock, value); }
    public bool RateChart { get => _rateChart; set => SetProperty(ref _rateChart, value); }
    public bool LabelPrinting { get => _labelPrinting; set => SetProperty(ref _labelPrinting, value); }

    public bool LedgerIndividual { get => _ledgerIndividual; set => SetProperty(ref _ledgerIndividual, value); }
    public bool LedgerGroup { get => _ledgerGroup; set => SetProperty(ref _ledgerGroup, value); }
    public bool MetalWiseLedger { get => _metalWiseLedger; set => SetProperty(ref _metalWiseLedger, value); }
    public bool KarigarLedger { get => _karigarLedger; set => SetProperty(ref _karigarLedger, value); }
    public bool TrialBalance { get => _trialBalance; set => SetProperty(ref _trialBalance, value); }
    public bool OpeningTrial { get => _openingTrial; set => SetProperty(ref _openingTrial, value); }
    public bool Trading { get => _trading; set => SetProperty(ref _trading, value); }
    public bool ProfitLoss { get => _profitLoss; set => SetProperty(ref _profitLoss, value); }
    public bool BalanceSheet { get => _balanceSheet; set => SetProperty(ref _balanceSheet, value); }
    public bool CashBook { get => _cashBook; set => SetProperty(ref _cashBook, value); }
    public bool CashBookOpening { get => _cashBookOpening; set => SetProperty(ref _cashBookOpening, value); }

    public bool AuditView { get => _auditView; set => SetProperty(ref _auditView, value); }
    public bool AuditDel { get => _auditDel; set => SetProperty(ref _auditDel, value); }
    public bool Attendance { get => _attendance; set => SetProperty(ref _attendance, value); }

    public void SetAllEntries(bool enabled)
    {
        SaleAdd = enabled; SaleMod = enabled; SaleDel = enabled;
        LedAdd = enabled; LedMod = enabled; LedDel = enabled;
        ItemAdd = enabled; ItemMod = enabled; ItemDel = enabled;
        BarAdd = enabled; BarMod = enabled; BarDel = enabled;
        BcAdd = enabled; BcMod = enabled; BcDel = enabled;
        RetAdd = enabled; RetMod = enabled; RetDel = enabled;
        PurAdd = enabled; PurMod = enabled; PurDel = enabled;
        RecAdd = enabled; RecMod = enabled; RecDel = enabled;
        CusAdd = enabled; CusMod = enabled; CusDel = enabled;
        PayAdd = enabled; PayMod = enabled; PayDel = enabled;
        JouAdd = enabled; JouMod = enabled; JouDel = enabled;
        MIssAdd = enabled; MIssMod = enabled; MIssDel = enabled;
        MRecAdd = enabled; MRecMod = enabled; MRecDel = enabled;
        AIssAdd = enabled; AIssMod = enabled; AIssDel = enabled;
        ARecAdd = enabled; ARecMod = enabled; ARecDel = enabled;
        HIssAdd = enabled; HIssMod = enabled; HIssDel = enabled;
        HRecAdd = enabled; HRecMod = enabled; HRecDel = enabled;
        UserAdd = enabled; UserMod = enabled; UserDel = enabled;
        RepAdd = enabled; RepMod = enabled; RepDel = enabled;
        ScAdd = enabled; ScMod = enabled; ScDel = enabled;
        StaffAdd = enabled; StaffMod = enabled; StaffDel = enabled;
        OrdAdd = enabled; OrdMod = enabled; OrdDel = enabled;
        BhavAdd = enabled; BhavMod = enabled; BhavDel = enabled;
        NoteTransfer = enabled; NoteTransferMod = enabled; NoteTransferDel = enabled;
    }

    public void SetAllReports(bool enabled)
    {
        SalesRegister = enabled;
        ItemWiseSales = enabled;
        VatRegister = enabled;
        HidePartyDetails = enabled;
        PurchaseRegister = enabled;
        BarcodeStock = enabled;
        RateChart = enabled;
        LabelPrinting = enabled;
        LedgerIndividual = enabled;
        LedgerGroup = enabled;
        MetalWiseLedger = enabled;
        KarigarLedger = enabled;
        TrialBalance = enabled;
        OpeningTrial = enabled;
        Trading = enabled;
        ProfitLoss = enabled;
        BalanceSheet = enabled;
        CashBook = enabled;
        CashBookOpening = enabled;
        AuditView = enabled;
        AuditDel = enabled;
        Attendance = enabled;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Username is required.";
            return;
        }

        var result = MessageBox.Show(
            "Do you want to Save user permissions?",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var entity = new UserPermission
            {
                User = Username.Trim(),
                Password = Password ?? string.Empty,
                Toolbar = string.IsNullOrWhiteSpace(Toolbar) ? "DP1" : Toolbar.Trim(),

                SaleAdd = SaleAdd, SaleMod = SaleMod, SaleDel = SaleDel,
                LedAdd = LedAdd, LedMod = LedMod, LedDel = LedDel,
                ItemAdd = ItemAdd, ItemMod = ItemMod, ItemDel = ItemDel,
                BarAdd = BarAdd, BarMod = BarMod, BarDel = BarDel,
                BcAdd = BcAdd, BcMod = BcMod, BcDel = BcDel,
                RetAdd = RetAdd, RetMod = RetMod, RetDel = RetDel,
                PurAdd = PurAdd, PurMod = PurMod, PurDel = PurDel,
                RecAdd = RecAdd, RecMod = RecMod, RecDel = RecDel,
                CusAdd = CusAdd, CusMod = CusMod, CusDel = CusDel,
                PayAdd = PayAdd, PayMod = PayMod, PayDel = PayDel,
                JouAdd = JouAdd, JouMod = JouMod, JouDel = JouDel,
                MIssAdd = MIssAdd, MIssMod = MIssMod, MIssDel = MIssDel,
                MRecAdd = MRecAdd, MRecMod = MRecMod, MRecDel = MRecDel,
                AIssAdd = AIssAdd, AIssMod = AIssMod, AIssDel = AIssDel,
                ARecAdd = ARecAdd, ARecMod = ARecMod, ARecDel = ARecDel,
                HIssAdd = HIssAdd, HIssMod = HIssMod, HIssDel = HIssDel,
                HRecAdd = HRecAdd, HRecMod = HRecMod, HRecDel = HRecDel,
                UserAdd = UserAdd, UserMod = UserMod, UserDel = UserDel,
                RepAdd = RepAdd, RepMod = RepMod, RepDel = RepDel,
                ScAdd = ScAdd, ScMod = ScMod, ScDel = ScDel,
                StaffAdd = StaffAdd, StaffMod = StaffMod, StaffDel = StaffDel,
                OrdAdd = OrdAdd, OrdMod = OrdMod, OrdDel = OrdDel,
                BhavAdd = BhavAdd, BhavMod = BhavMod, BhavDel = BhavDel,
                NoteTransfer = NoteTransfer, NoteTransferMod = NoteTransferMod, NoteTransferDel = NoteTransferDel,

                SalesRegister = SalesRegister,
                ItemWiseSales = ItemWiseSales,
                VatRegister = VatRegister,
                HidePartyDetails = HidePartyDetails,
                PurchaseRegister = PurchaseRegister,
                BarcodeStock = BarcodeStock,
                RateChart = RateChart,
                LabelPrinting = LabelPrinting,
                LedgerIndividual = LedgerIndividual,
                LedgerGroup = LedgerGroup,
                MetalWiseLedger = MetalWiseLedger,
                KarigarLedger = KarigarLedger,
                TrialBalance = TrialBalance,
                OpeningTrial = OpeningTrial,
                Trading = Trading,
                ProfitLoss = ProfitLoss,
                BalanceSheet = BalanceSheet,
                CashBook = CashBook,
                CashBookOpening = CashBookOpening,
                AuditView = AuditView,
                AuditDel = AuditDel,
                Attendance = Attendance
            };

            if (IsNew || string.IsNullOrWhiteSpace(_originalUsername))
            {
                var existing = await _userPermissionRepository.GetByUsernameAsync(entity.User);
                if (existing is not null)
                {
                    ErrorMessage = $"User '{entity.User}' already exists.";
                    return;
                }

                await _userPermissionRepository.AddAsync(entity);
            }
            else
            {
                await _userPermissionRepository.UpdateAsync(_originalUsername, entity);
            }

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error saving user permissions: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }
}
