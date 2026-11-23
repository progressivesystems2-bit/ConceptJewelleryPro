using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Application.Licensing;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IAccountRepository? _accountRepository;
    private readonly ICustomerRepository? _customerRepository;
    private readonly IItemRepository? _itemRepository;
    private readonly IPurityRepository? _purityRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly Lazy<AccountCreationViewModel> _accountCreationViewModel;
    private readonly Lazy<CustomerCreationViewModel> _customerCreationViewModel;
    private readonly Lazy<ItemCreationViewModel> _itemCreationViewModel;
    private readonly Func<InvoiceViewModel> _invoiceViewModelFactory;
    private readonly Lazy<PurchaseViewModel>? _purchaseViewModel;
    private readonly Lazy<PurchaseExpenseEntryViewModel>? _purchaseExpenseEntryViewModel;
    private readonly Lazy<NonBarcodeStockViewModel>? _nonBarcodeStockViewModel;
    private readonly Lazy<BarcodeCreationViewModel>? _barcodeCreationViewModel;
    private readonly Lazy<BarcodeCreationBulkViewModel>? _barcodeCreationBulkViewModel;
    private readonly Lazy<KarigharIssueViewModel>? _karigharIssueViewModel;
    private readonly Lazy<KarigharReceiveViewModel>? _karigharReceiveViewModel;
    private readonly Lazy<HireIssueViewModel>? _hireIssueViewModel;
    private readonly Lazy<HireReceiveViewModel>? _hireReceiveViewModel;
    private readonly Lazy<PurityMasterViewModel>? _purityMasterViewModel;
    private readonly Lazy<StaffCreationViewModel>? _staffCreationViewModel;
    private readonly Lazy<SchemeCreationViewModel>? _schemeCreationViewModel;
    private readonly Lazy<UserPermissionViewModel>? _userPermissionViewModel;
    private readonly Lazy<GeneralSettingsViewModel>? _generalSettingsViewModel;
    private readonly Lazy<PrinterSettingsViewModel>? _printerSettingsViewModel;
    private readonly Lazy<BackupViewModel>? _backupViewModel;
    private readonly Lazy<VoucherEntryViewModel>? _paymentsViewModel;
    private readonly Lazy<VoucherEntryViewModel>? _receiptsViewModel;
    private readonly Lazy<VoucherEntryViewModel>? _journalViewModel;
    private readonly Lazy<SchemeReceiptViewModel>? _schemeReceiptViewModel;
    private readonly Lazy<SchemeDueListViewModel>? _schemeDueListViewModel;
    private readonly Lazy<BarcodeStockReportViewModel>? _barcodeStockReportViewModel;
    private readonly Lazy<NonBarcodeStockReportViewModel>? _nonBarcodeStockReportViewModel;
    private readonly Lazy<NonBarcodeItemStockReportViewModel>? _nonBarcodeItemStockReportViewModel;
    private readonly Lazy<LedgerReportViewModel>? _ledgerReportViewModel;
    private readonly Lazy<LedgerGroupViewModel>? _ledgerGroupViewModel;
    private readonly Lazy<TrialBalanceViewModel>? _trialBalanceViewModel;
    private readonly Lazy<TradingAccountViewModel>? _tradingAccountViewModel;
    private readonly Lazy<BalanceSheetViewModel>? _balanceSheetViewModel;
    private readonly Lazy<AuditReportViewModel>? _auditReportViewModel;
    private readonly Lazy<SalesVouTypeMasterViewModel>? _salesVouTypeMasterViewModel;
    private readonly Lazy<PurchaseRegisterViewModel>? _purchaseRegisterViewModel;
    private readonly Lazy<CatalogueViewModel>? _catalogueViewModel;
    private readonly Lazy<GSTReportsViewModel>? _gstReportsViewModel;
    private readonly Lazy<OrderRequirementViewModel>? _orderRequirementViewModel;
    private readonly Lazy<DashboardViewModel>? _dashboardViewModel;
    private readonly Lazy<BoxMasterViewModel>? _boxMasterViewModel;
    private readonly Lazy<BankReconciliationViewModel>? _bankReconciliationViewModel;
    private readonly Lazy<CardAdjustmentReceiptViewModel>? _cardAdjustmentReceiptViewModel;
    private readonly Lazy<StockTransferViewModel>? _stockTransferViewModel;
    private readonly Lazy<CouponEngineViewModel>? _couponEngineViewModel;
    private readonly Lazy<SoftwareUpdateViewModel>? _softwareUpdateViewModel;
    private readonly Lazy<LicenseInfoViewModel>? _licenseInfoViewModel;
    private readonly ILicenseValidationService? _licenseValidationService;
    private readonly List<MenuEntry> _allMenuItems = new();
    private CancellationTokenSource? _globalSearchCts;
    private IReadOnlyList<Account>? _searchAccounts;
    private IReadOnlyList<Customer>? _searchCustomers;
    private IReadOnlyList<Item>? _searchItems;
    private ModuleDocument? _selectedDocument;
    private string _searchText = string.Empty;
    private bool _isGlobalSearchOpen;
    private GlobalSearchResultViewModel? _selectedSearchResult;

    public MainViewModel(
        User currentUser,
        Lazy<AccountCreationViewModel> accountCreationViewModel,
        Lazy<CustomerCreationViewModel> customerCreationViewModel,
        Lazy<ItemCreationViewModel> itemCreationViewModel,
        Func<InvoiceViewModel>? invoiceViewModelFactory = null,
        Lazy<PurchaseViewModel>? purchaseViewModel = null,
        Lazy<PurchaseExpenseEntryViewModel>? purchaseExpenseEntryViewModel = null,
        Lazy<NonBarcodeStockViewModel>? nonBarcodeStockViewModel = null,
        Lazy<BarcodeCreationViewModel>? barcodeCreationViewModel = null,
        Lazy<BarcodeCreationBulkViewModel>? barcodeCreationBulkViewModel = null,
        Lazy<KarigharIssueViewModel>? karigharIssueViewModel = null,
        Lazy<KarigharReceiveViewModel>? karigharReceiveViewModel = null,
        Lazy<HireIssueViewModel>? hireIssueViewModel = null,
        Lazy<HireReceiveViewModel>? hireReceiveViewModel = null,
        Lazy<PurityMasterViewModel>? purityMasterViewModel = null,
        Lazy<StaffCreationViewModel>? staffCreationViewModel = null,
        Lazy<SchemeCreationViewModel>? schemeCreationViewModel = null,
        Lazy<UserPermissionViewModel>? userPermissionViewModel = null,
        UserPermission? currentUserPermissions = null,
        Lazy<GeneralSettingsViewModel>? generalSettingsViewModel = null,
        Lazy<PrinterSettingsViewModel>? printerSettingsViewModel = null,
        Lazy<BackupViewModel>? backupViewModel = null,
        Lazy<VoucherEntryViewModel>? paymentsViewModel = null,
        Lazy<VoucherEntryViewModel>? receiptsViewModel = null,
        Lazy<VoucherEntryViewModel>? journalViewModel = null,
        Lazy<SchemeReceiptViewModel>? schemeReceiptViewModel = null,
        Lazy<SchemeDueListViewModel>? schemeDueListViewModel = null,
        Lazy<BarcodeStockReportViewModel>? barcodeStockReportViewModel = null,
        Lazy<NonBarcodeStockReportViewModel>? nonBarcodeStockReportViewModel = null,
        Lazy<NonBarcodeItemStockReportViewModel>? nonBarcodeItemStockReportViewModel = null,
        Lazy<LedgerReportViewModel>? ledgerReportViewModel = null,
        Lazy<LedgerGroupViewModel>? ledgerGroupViewModel = null,
        Lazy<TrialBalanceViewModel>? trialBalanceViewModel = null,
        Lazy<TradingAccountViewModel>? tradingAccountViewModel = null,
        Lazy<BalanceSheetViewModel>? balanceSheetViewModel = null,
        Lazy<AuditReportViewModel>? auditReportViewModel = null,
        Lazy<SalesVouTypeMasterViewModel>? salesVouTypeMasterViewModel = null,
        Lazy<PurchaseRegisterViewModel>? purchaseRegisterViewModel = null,
        Lazy<CatalogueViewModel>? catalogueViewModel = null,
        Lazy<GSTReportsViewModel>? gstReportsViewModel = null,
        Lazy<OrderRequirementViewModel>? orderRequirementViewModel = null,
        Lazy<DashboardViewModel>? dashboardViewModel = null,
        Lazy<BoxMasterViewModel>? boxMasterViewModel = null,
        Lazy<BankReconciliationViewModel>? bankReconciliationViewModel = null,
        Lazy<CardAdjustmentReceiptViewModel>? cardAdjustmentReceiptViewModel = null,
        Lazy<StockTransferViewModel>? stockTransferViewModel = null,
        Lazy<CouponEngineViewModel>? couponEngineViewModel = null,
        Lazy<SoftwareUpdateViewModel>? softwareUpdateViewModel = null,
        Lazy<LicenseInfoViewModel>? licenseInfoViewModel = null,
        ILicenseValidationService? licenseValidationService = null,
        IAccountRepository? accountRepository = null,
        ICustomerRepository? customerRepository = null,
        IItemRepository? itemRepository = null,
        IPurityRepository? purityRepository = null,
        IAuditRepository? auditRepository = null)
    {
        CurrentUser = currentUser;
        CurrentUserPermissions = currentUserPermissions;
        _accountRepository = accountRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _auditRepository = auditRepository;
        _accountCreationViewModel = accountCreationViewModel;
        _customerCreationViewModel = customerCreationViewModel;
        _itemCreationViewModel = itemCreationViewModel;

        _invoiceViewModelFactory = invoiceViewModelFactory ?? (() => throw new InvalidOperationException("InvoiceViewModel factory not provided."));
        _purchaseViewModel = purchaseViewModel;
        _purchaseExpenseEntryViewModel = purchaseExpenseEntryViewModel;
        _nonBarcodeStockViewModel = nonBarcodeStockViewModel;
        _barcodeCreationViewModel = barcodeCreationViewModel;
        _barcodeCreationBulkViewModel = barcodeCreationBulkViewModel;
        _karigharIssueViewModel = karigharIssueViewModel;
        _karigharReceiveViewModel = karigharReceiveViewModel;
        _hireIssueViewModel = hireIssueViewModel;
        _hireReceiveViewModel = hireReceiveViewModel;
        _purityMasterViewModel = purityMasterViewModel;
        _staffCreationViewModel = staffCreationViewModel;
        _schemeCreationViewModel = schemeCreationViewModel;
        _userPermissionViewModel = userPermissionViewModel;
        _generalSettingsViewModel = generalSettingsViewModel;
        _printerSettingsViewModel = printerSettingsViewModel;
        _backupViewModel = backupViewModel;
        _paymentsViewModel = paymentsViewModel;
        _receiptsViewModel = receiptsViewModel;
        _journalViewModel = journalViewModel;
        _schemeReceiptViewModel = schemeReceiptViewModel;
        _schemeDueListViewModel = schemeDueListViewModel;
        _barcodeStockReportViewModel = barcodeStockReportViewModel;
        _nonBarcodeStockReportViewModel = nonBarcodeStockReportViewModel;
        _nonBarcodeItemStockReportViewModel = nonBarcodeItemStockReportViewModel;
        _ledgerReportViewModel = ledgerReportViewModel;
        _ledgerGroupViewModel = ledgerGroupViewModel;
        _trialBalanceViewModel = trialBalanceViewModel;
        _tradingAccountViewModel = tradingAccountViewModel;
        _balanceSheetViewModel = balanceSheetViewModel;
        _auditReportViewModel = auditReportViewModel;
        _salesVouTypeMasterViewModel = salesVouTypeMasterViewModel;
        _purchaseRegisterViewModel = purchaseRegisterViewModel;
        _catalogueViewModel = catalogueViewModel;
        _gstReportsViewModel = gstReportsViewModel;
        _orderRequirementViewModel = orderRequirementViewModel;
        _dashboardViewModel = dashboardViewModel;
        _boxMasterViewModel = boxMasterViewModel;
        _bankReconciliationViewModel = bankReconciliationViewModel;
        _cardAdjustmentReceiptViewModel = cardAdjustmentReceiptViewModel;
        _stockTransferViewModel = stockTransferViewModel;
        _couponEngineViewModel = couponEngineViewModel;
        _softwareUpdateViewModel = softwareUpdateViewModel ?? new Lazy<SoftwareUpdateViewModel>(() => new SoftwareUpdateViewModel());
        _licenseInfoViewModel = licenseInfoViewModel;
        _licenseValidationService = licenseValidationService;

        SelectModuleCommand = new RelayCommand(parameter => SelectModule(parameter as string));
        OpenGlobalSearchResultCommand = new RelayCommand(parameter => OpenGlobalSearchResult(parameter as GlobalSearchResultViewModel ?? SelectedSearchResult));
        ToggleMenuCommand = new RelayCommand(parameter => ToggleMenu(parameter as TopMenuCategory));
        CloseAllMenusCommand = new RelayCommand(CloseAllMenus);
        CloseDocumentCommand = new RelayCommand(parameter => CloseDocument(parameter as ModuleDocument));
        CloseCurrentDocumentCommand = new RelayCommand(() => CloseDocument(SelectedDocument), () => SelectedDocument is not null);
        LogoutCommand = new RelayCommand(() => LogoutRequested?.Invoke(this, EventArgs.Empty));
        OpenRateUpdateCommand = new RelayCommand(OpenRateUpdate);
        RefreshCommand = new RelayCommand(async () => await ExecuteRefreshAsync());
        ThemeSelector = new ThemeSelectorViewModel();

        Documents = new ObservableCollection<ModuleDocument>();
        TopMenus = BuildMenu();

        if (_dashboardViewModel is not null)
        {
            SelectModule("Dashboard");
        }
    }

    public event EventHandler? LogoutRequested;

    public User CurrentUser { get; }

    public UserPermission? CurrentUserPermissions { get; }

    public ICommand OpenRateUpdateCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand RefreshLookupsCommand => RefreshCommand;

    public string WelcomeMessage => $"Welcome, {CurrentUser.DisplayName}";

    public string RoleName => AppRoles.ToName(CurrentUser.Role);

    public string StatusText => $"Signed in as {CurrentUser.Username}";

    public string AppVersion => AppSettings.AppName + "  v" + AppSettings.GetInstalledVersion();

    public IReadOnlyList<TopMenuCategory> TopMenus { get; }

    public RelayCommand ToggleMenuCommand { get; }

    public RelayCommand CloseAllMenusCommand { get; }

    public ThemeSelectorViewModel ThemeSelector { get; }

    public ObservableCollection<ModuleDocument> Documents { get; }

    public ObservableCollection<GlobalSearchResultViewModel> GlobalSearchResults { get; } = new();

    public RelayCommand SelectModuleCommand { get; }

    public RelayCommand OpenGlobalSearchResultCommand { get; }

    public RelayCommand CloseDocumentCommand { get; }

    public RelayCommand CloseCurrentDocumentCommand { get; }

    public RelayCommand LogoutCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = RefreshGlobalSearchAsync(value);
            }
        }
    }

    public bool IsGlobalSearchOpen
    {
        get => _isGlobalSearchOpen;
        set
        {
            if (SetProperty(ref _isGlobalSearchOpen, value) && !value)
            {
                CloseGlobalSearchPopup();
            }
        }
    }

    public GlobalSearchResultViewModel? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set => SetProperty(ref _selectedSearchResult, value);
    }

    public ModuleDocument? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            var oldDoc = _selectedDocument;
            if (SetProperty(ref _selectedDocument, value))
            {
                if (oldDoc?.Content is IDocumentActivationHandler oldHandler)
                {
                    _ = oldHandler.OnDeactivatedAsync();
                }
                if (value?.Content is IDocumentActivationHandler newHandler)
                {
                    _ = newHandler.OnActivatedAsync();
                }
            }
        }
    }

    public async Task ExecuteRefreshAsync()
    {
        _searchAccounts = null;
        _searchCustomers = null;
        _searchItems = null;

        var activeDoc = SelectedDocument?.Content;
        if (activeDoc is IRefreshable activeRefreshable)
        {
            await activeRefreshable.RefreshAsync();
        }

        foreach (var doc in Documents)
        {
            if (doc.Content is IRefreshable refreshable && refreshable != activeDoc)
            {
                _ = refreshable.RefreshAsync();
            }
        }
    }

    public void SelectModule(string? moduleTitle, object? parameter = null)
    {
        CloseAllMenus();

        if (string.IsNullOrWhiteSpace(moduleTitle))
        {
            return;
        }

        if (moduleTitle is "Account Creation" or "Ledger Creation" or "LedgerCreation")
        {
            moduleTitle = "Account Creation";
        }
        else if (moduleTitle is "Rate Update" or "Rate Master" or "Daily Rate Update" or "Update Rates")
        {
            OpenRateUpdate();
            return;
        }
        else if (moduleTitle is "Purchases Expenses" or "Purchase Expenses" or "Purchase Expense")
        {
            moduleTitle = "Purchases Expenses";
        }
        else if (moduleTitle is "Purchase" or "Purchase Entry" or "Purchases")
        {
            moduleTitle = "Purchase";
        }
        else if (moduleTitle is "Purity Masters" or "Purity Master")
        {
            moduleTitle = "Purity Master";
        }
        else if (moduleTitle is "Barcode Reports" or "Barcode Stock Reports" or "Barcode Stock" or "Barcode Report" or "BARCODE STOCK REPORTS")
        {
            moduleTitle = "Barcode Stock Reports";
        }

        if (!IsModuleLicensed(moduleTitle))
        {
            var requiredCode = JewelleryModuleCatalog.ResolveModuleForScreen(moduleTitle);
            var requiredName = JewelleryModuleCatalog.GetDisplayName(requiredCode);
            MessageBox.Show(
                $"The \"{moduleTitle}\" feature belongs to the \"{requiredName}\" module, which is not enabled in your current license.\n\n" +
                "Please contact your software provider to enable this module in Concept License Manager, then refresh your license under Settings -> License Information.",
                "Module Locked - Upgrade Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!IsPermitted(moduleTitle))
        {
            return;
        }

        if (moduleTitle == "Invoice")
        {
            SelectInvoiceModule(parameter);
            return;
        }

        var existing = Documents.FirstOrDefault(doc => doc.Title == moduleTitle);
        if (existing is not null)
        {
            SelectedDocument = existing;
            ApplyNavigationParameter(moduleTitle, parameter);
            return;
        }

        ModuleDocument document;
        if ((moduleTitle is "Dashboard" or "Showroom Dashboard" or "Overview") && _dashboardViewModel is not null)
        {
            var vm = _dashboardViewModel.Value;
            vm.RequestNavigateModule -= OnDashboardNavigateModule;
            vm.RequestNavigateModule += OnDashboardNavigateModule;
            vm.RequestOpenOrder -= OnDashboardOpenOrder;
            vm.RequestOpenOrder += OnDashboardOpenOrder;
            vm.RequestOpenSchemeReceipt -= OnDashboardOpenSchemeReceipt;
            vm.RequestOpenSchemeReceipt += OnDashboardOpenSchemeReceipt;
            vm.RequestOpenRateUpdate -= OnDashboardOpenRateUpdate;
            vm.RequestOpenRateUpdate += OnDashboardOpenRateUpdate;
            document = new ModuleDocument("Dashboard", AppIcons.Dashboard, vm);
        }
        else if (moduleTitle == "Account Creation")
        {
            document = new ModuleDocument("Account Creation", AppIcons.AccountCreation, _accountCreationViewModel.Value);
        }
        else if (moduleTitle == "Customer Creation")
        {
            document = new ModuleDocument("Customer Creation", AppIcons.CustomerCreation, _customerCreationViewModel.Value);
        }
        else if (moduleTitle == "Item Creation")
        {
            document = new ModuleDocument("Item Creation", AppIcons.ItemCreation, _itemCreationViewModel.Value);
        }
        else if (moduleTitle == "Purity Master" && _purityMasterViewModel is not null)
        {
            document = new ModuleDocument("Purity Master", AppIcons.PurityMaster, _purityMasterViewModel.Value);
        }
        else if ((moduleTitle is "Box Master" or "BoxMaster" or "Boxes" or "Box") && _boxMasterViewModel is not null)
        {
            document = new ModuleDocument("Box Master", "\uE8F1", _boxMasterViewModel.Value);
        }
        else if ((moduleTitle is "Voucher Types" or "Voucher Type Configuration" or "Sales Voucher Types" or "SalesVouType" or "SalesVouTypes" or "SalesVouType Configuration" or "Invoice Types" or "Invoice Type Configuration") && _salesVouTypeMasterViewModel is not null)
        {
            document = new ModuleDocument("Voucher Types", AppIcons.SalesVouType, _salesVouTypeMasterViewModel.Value);
        }
        else if ((moduleTitle == "Staff Creation" || moduleTitle == "Staff" || moduleTitle == "StaffCreation") && _staffCreationViewModel is not null)
        {
            document = new ModuleDocument("Staff Creation", AppIcons.StaffCreation, _staffCreationViewModel.Value);
        }
        else if ((moduleTitle is "Stock Transfer" or "StockTransfer" or "Stock Transfers") && _stockTransferViewModel is not null)
        {
            document = new ModuleDocument("Stock Transfer", AppIcons.StockTransfer, _stockTransferViewModel.Value);
        }
        else if (moduleTitle == "Purchase" && _purchaseViewModel is not null)
        {
            document = new ModuleDocument("Purchase", AppIcons.Purchase, _purchaseViewModel.Value);
        }
        else if ((moduleTitle is "Purchase Register" or "PurchaseRegister" or "Purchase List" or "Purchases Register" or "PURCHASE REGISTER") && _purchaseRegisterViewModel is not null)
        {
            var vm = _purchaseRegisterViewModel.Value;
            vm.RequestOpenPurchase -= OnOpenPurchaseRequested;
            vm.RequestOpenPurchase += OnOpenPurchaseRequested;
            document = new ModuleDocument("Purchase Register", AppIcons.Purchase, vm);
        }
        else if (moduleTitle == "Purchases Expenses" && _purchaseExpenseEntryViewModel is not null)
        {
            document = new ModuleDocument("Purchases Expenses", AppIcons.PurchasesExpenses, _purchaseExpenseEntryViewModel.Value);
        }
        else if ((moduleTitle == "Non-Barcode Stock" || moduleTitle == "Non Barcode Stock") && _nonBarcodeStockViewModel is not null)
        {
            document = new ModuleDocument("Non-Barcode Stock", AppIcons.NonBarcodeStock, _nonBarcodeStockViewModel.Value);
        }
        else if ((moduleTitle == "Barcode Creation" || moduleTitle == "BarcodeCreation") && _barcodeCreationViewModel is not null)
        {
            document = new ModuleDocument("Barcode Creation", AppIcons.BarcodeCreation, _barcodeCreationViewModel.Value);
            var barcode = parameter?.ToString();
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                _ = _barcodeCreationViewModel.Value.SearchAndLoadBarcodeAsync(barcode);
            }
        }
        else if ((moduleTitle is "Catalogue" or "Catalogue View" or "CatalogueView") && _catalogueViewModel is not null)
        {
            var vm = _catalogueViewModel.Value;
            vm.OpenBarcodeRequested -= OnCatalogueOpenBarcodeRequested;
            vm.OpenBarcodeRequested += OnCatalogueOpenBarcodeRequested;
            document = new ModuleDocument("Catalogue", AppIcons.Catalogue, vm);
        }
        else if ((moduleTitle == "Barcode Creation (Bulk)" || moduleTitle == "BarcodeCreationBulk" || moduleTitle == "Bulk Barcode Creation") && _barcodeCreationBulkViewModel is not null)
        {
            document = new ModuleDocument("Barcode Creation (Bulk)", AppIcons.BarcodeCreationBulk, _barcodeCreationBulkViewModel.Value);
        }
        else if ((moduleTitle == "Karighar Issue" || moduleTitle == "Metal Issued" || moduleTitle == "MetalIssued") && _karigharIssueViewModel is not null)
        {
            document = new ModuleDocument("Karighar Issue", AppIcons.KarigharIssue, _karigharIssueViewModel.Value);
        }
        else if ((moduleTitle == "Karighar Received" || moduleTitle == "Karighar Receive" || moduleTitle == "Metal Received" || moduleTitle == "MetalReceived") && _karigharReceiveViewModel is not null)
        {
            document = new ModuleDocument("Karighar Received", AppIcons.KarigharReceived, _karigharReceiveViewModel.Value);
        }
        else if ((moduleTitle == "Hire Issue" || moduleTitle == "HireIssued" || moduleTitle == "Hire Issue Entry" || moduleTitle == "HireIssue") && _hireIssueViewModel is not null)
        {
            document = new ModuleDocument("Hire Issue", AppIcons.KarigharIssue, _hireIssueViewModel.Value);
        }
        else if ((moduleTitle == "Hire Receive" || moduleTitle == "HireReceived" || moduleTitle == "Hire Recieve" || moduleTitle == "HireRecieve" || moduleTitle == "Hire Receive Entry" || moduleTitle == "HireReceive") && _hireReceiveViewModel is not null)
        {
            document = new ModuleDocument("Hire Receive", AppIcons.KarigharReceived, _hireReceiveViewModel.Value);
        }
        else if ((moduleTitle is "Scheme Creation" or "SchemeCreation" or "Scheme") && _schemeCreationViewModel is not null)
        {
            document = new ModuleDocument("Scheme Creation", AppIcons.SchemeCreation, _schemeCreationViewModel.Value);
        }
        else if ((moduleTitle is "Coupon Engine" or "CouponEngine" or "Coupons") && _couponEngineViewModel is not null)
        {
            document = new ModuleDocument("Coupon Engine", AppIcons.SchemeCreation, _couponEngineViewModel.Value);
        }
        else if (moduleTitle == "User Permission" && _userPermissionViewModel is not null)
        {
            document = new ModuleDocument("User Permission", AppIcons.UserPermission, _userPermissionViewModel.Value);
        }
        else if (moduleTitle == "General Settings" && _generalSettingsViewModel is not null)
        {
            document = new ModuleDocument("General Settings", AppIcons.GeneralSettings, _generalSettingsViewModel.Value);
        }
        else if (moduleTitle == "Printer Settings" && _printerSettingsViewModel is not null)
        {
            document = new ModuleDocument("Printer Settings", AppIcons.PrinterSettings, _printerSettingsViewModel.Value);
        }
        else if ((moduleTitle is "Backup Settings" or "Backup" or "BackUp" or "Database Backup" or "BackUpView" or "BackupView") && _backupViewModel is not null)
        {
            document = new ModuleDocument("Backup Settings", AppIcons.BackupSettings, _backupViewModel.Value);
        }
        else if ((moduleTitle is "Software Updates" or "Software Update" or "Update" or "Updates" or "Check for Updates") && _softwareUpdateViewModel is not null)
        {
            document = new ModuleDocument("Software Updates", AppIcons.SoftwareUpdates, _softwareUpdateViewModel.Value);
        }
        else if ((moduleTitle is "License Information" or "License" or "Licensing" or "License Info") && _licenseInfoViewModel is not null)
        {
            document = new ModuleDocument("License Information", "\uE8D7", _licenseInfoViewModel.Value);
        }
        else if ((moduleTitle is "Payments" or "Payment" or "PAYMENTS") && _paymentsViewModel is not null)
        {
            document = new ModuleDocument("Payments", AppIcons.Payments, _paymentsViewModel.Value);
        }
        else if ((moduleTitle is "Receipts" or "Receipt" or "RECIEPTS") && _receiptsViewModel is not null)
        {
            document = new ModuleDocument("Receipts", AppIcons.Receipts, _receiptsViewModel.Value);
        }
        else if ((moduleTitle is "Journal" or "Journal Entry" or "JOURNAL" or "JV") && _journalViewModel is not null)
        {
            document = new ModuleDocument("Journal", AppIcons.Journal, _journalViewModel.Value);
        }
        else if ((moduleTitle is "Scheme Receipts" or "Scheme Receipt" or "SchemeReceipts" or "SchemeReceipt" or "SCHEME ENTRY" or "Scheme Entry") && _schemeReceiptViewModel is not null)
        {
            document = new ModuleDocument("Scheme Receipts", AppIcons.SchemeReceipts, _schemeReceiptViewModel.Value);
        }
        else if ((moduleTitle is "Bank Reconciliation" or "BankReconciliation" or "BRS" or "Bank Statement Reconciliation") && _bankReconciliationViewModel is not null)
        {
            document = new ModuleDocument("Bank Reconciliation", AppIcons.BankReconciliation, _bankReconciliationViewModel.Value);
        }
        else if ((moduleTitle is "Card Reconciliation" or "CardReconciliation" or "Card Adjustment" or "CardAdjustment" or "Card Adjustment Receipt" or "CardAdjustmentReceipt") && _cardAdjustmentReceiptViewModel is not null)
        {
            document = new ModuleDocument("Card Reconciliation", AppIcons.CardReconciliation, _cardAdjustmentReceiptViewModel.Value);
        }
        else if ((moduleTitle is "Scheme Due List" or "Scheme Due" or "Scheme Dues" or "SchemeDueList") && _schemeDueListViewModel is not null)
        {
            var vm = _schemeDueListViewModel.Value;
            vm.CollectPaymentRequested -= OnSchemeCollectPaymentRequested;
            vm.CollectPaymentRequested += OnSchemeCollectPaymentRequested;
            document = new ModuleDocument("Scheme Due List", AppIcons.SchemeReceipts, vm);
        }
        else if ((moduleTitle is "Barcode Reports" or "Barcode Stock Reports" or "Barcode Stock" or "Barcode Report" or "BARCODE STOCK REPORTS") && _barcodeStockReportViewModel is not null)
        {
            document = new ModuleDocument("Barcode Stock Reports", AppIcons.BarcodeReports, _barcodeStockReportViewModel.Value);
        }
        else if ((moduleTitle is "NonBarcode Stock reports" or "NonBarcode Stock Reports" or "Non-Barcode Stock Reports" or "Non-Barcode Stock Report" or "Non Barcode Stock Report" or "Non Barcode Stock Reports" or "Item-Wise Non-Barcode Stock" or "ItemWise NonBarcode Stock") && _nonBarcodeItemStockReportViewModel is not null)
        {
            document = new ModuleDocument("NonBarcode Stock reports", AppIcons.BarcodeReports, _nonBarcodeItemStockReportViewModel.Value);
        }
        else if ((moduleTitle is "ItemType Stock Report" or "ItemType Stock Reports" or "ItemType Stock" or "Item Type Stock" or "Item Type Stock Report" or "Primary Stock Report") && _nonBarcodeStockReportViewModel is not null)
        {
            document = new ModuleDocument("ItemType Stock Report", AppIcons.Reports, _nonBarcodeStockReportViewModel.Value);
        }
        else if ((moduleTitle is "Ledger Reports" or "Ledger Report" or "Ledger" or "LEDGER REPORTS" or "Account Ledger") && _ledgerReportViewModel is not null)
        {
            var vm = _ledgerReportViewModel.Value;
            vm.OpenVoucherRequested -= OnOpenVoucherRequested;
            vm.OpenVoucherRequested += OnOpenVoucherRequested;
            document = new ModuleDocument("Ledger Reports", AppIcons.LedgerReports, vm);
        }
        else if ((moduleTitle is "Ledger Group" or "Group Ledger" or "LedgerGroup" or "HeadGroup Ledger") && _ledgerGroupViewModel is not null)
        {
            var vm = _ledgerGroupViewModel.Value;
            vm.OpenAccountLedgerRequested -= OnOpenAccountLedgerRequested;
            vm.OpenAccountLedgerRequested += OnOpenAccountLedgerRequested;
            document = new ModuleDocument("Ledger Group", AppIcons.LedgerReports, vm);
        }
        else if ((moduleTitle is "Trial Balance" or "TrialBalance" or "TRIAL BALANCE") && _trialBalanceViewModel is not null)
        {
            document = new ModuleDocument("Trial Balance", AppIcons.Reports, _trialBalanceViewModel.Value);
        }
        else if ((moduleTitle is "Trading Account" or "TradingAccount" or "TRADING ACCOUNT" or "Trading") && _tradingAccountViewModel is not null)
        {
            var vm = _tradingAccountViewModel.Value;
            vm.OpenAccountLedgerRequested -= OnOpenAccountLedgerRequested;
            vm.OpenAccountLedgerRequested += OnOpenAccountLedgerRequested;
            document = new ModuleDocument("Trading Account", AppIcons.Reports, vm);
        }
        else if ((moduleTitle is "Balance Sheet" or "BalanceSheet" or "BALANCE SHEET") && _balanceSheetViewModel is not null)
        {
            var vm = _balanceSheetViewModel.Value;
            vm.OpenAccountLedgerRequested -= OnOpenAccountLedgerRequested;
            vm.OpenAccountLedgerRequested += OnOpenAccountLedgerRequested;
            document = new ModuleDocument("Balance Sheet", AppIcons.Reports, vm);
        }
        else if ((moduleTitle is "Audit Report" or "Audit Reports" or "Audit Log" or "Audit Logs" or "AuditReport" or "Audit") && _auditReportViewModel is not null)
        {
            document = new ModuleDocument("Audit Report", AppIcons.Reports, _auditReportViewModel.Value);
        }
        else if ((moduleTitle is "GST Reports & Reconciliation" or "GST Reports" or "GST Report" or "GST Reconciliation" or "GSTR-1" or "GSTR-3B" or "GSTR-2" or "GST" or "GSTReports") && _gstReportsViewModel is not null)
        {
            document = new ModuleDocument("GST Reports & Reconciliation", AppIcons.Reports, _gstReportsViewModel.Value);
        }
        else if ((moduleTitle is "Order/Customer Requirements" or "Order / Customer Requirements" or "Order" or "Orders" or "Customer Requirements" or "OrderRequirement" or "OrderRequirements" or "Customer Orders" or "Customers Order") && _orderRequirementViewModel is not null)
        {
            document = new ModuleDocument("Order/Customer Requirements", AppIcons.Entries, _orderRequirementViewModel.Value);
        }
        else
        {
            var entry = TopMenus
                .SelectMany(menu => menu.Sections)
                .SelectMany(section => section.Items)
                .FirstOrDefault(item => item.Title == moduleTitle);

            document = new ModuleDocument(
                moduleTitle,
                entry?.Icon ?? AppIcons.Dashboard,
                new ModulePlaceholderViewModel(
                    moduleTitle,
                    entry?.Icon ?? AppIcons.Dashboard,
                    $"The \"{moduleTitle}\" module is under construction."));
        }

        Documents.Add(document);
        SelectedDocument = document;
        ApplyNavigationParameter(moduleTitle, parameter);
    }

    private void ApplyNavigationParameter(string moduleTitle, object? parameter)
    {
        if (moduleTitle == "Account Creation")
        {
            var vm = _accountCreationViewModel.Value;
            var paramStr = parameter?.ToString();
            if (!string.IsNullOrWhiteSpace(paramStr))
            {
                vm.SearchText = paramStr;
                var found = vm.Accounts.FirstOrDefault(a =>
                    string.Equals(a.PartyCode, paramStr, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.AccountName, paramStr, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    vm.SelectedAccount = found;
                }
            }
            else
            {
                vm.ResetSelection();
            }
            return;
        }

        if (moduleTitle == "Ledger Reports")
        {
            var vm = _ledgerReportViewModel?.Value;
            if (vm != null)
            {
                var accountName = parameter?.ToString();
                if (!string.IsNullOrWhiteSpace(accountName))
                {
                    vm.SelectedAccountName = accountName;
                }
                else
                {
                    vm.ResetSelection();
                }
            }
            return;
        }

        if (parameter is null) return;
        else if (moduleTitle == "Customer Creation")
        {
            var customerCode = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(customerCode))
            {
                var vm = _customerCreationViewModel.Value;
                _ = vm.SelectCustomerByCodeAsync(customerCode);
            }
        }
        else if (moduleTitle == "Item Creation")
        {
            var itemCode = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(itemCode) && _itemCreationViewModel != null)
            {
                var vm = _itemCreationViewModel.Value;
                _ = vm.SelectItemByCodeAsync(itemCode);
            }
        }
        else if (moduleTitle == "Purchase")
        {
            var vouNo = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(vouNo) && _purchaseViewModel != null)
            {
                _ = _purchaseViewModel.Value.LoadPurchaseAsync(vouNo);
            }
        }
        else if (moduleTitle == "Purchases Expenses")
        {
            var vouNo = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(vouNo) && _purchaseExpenseEntryViewModel != null)
            {
                _ = _purchaseExpenseEntryViewModel.Value.LoadExpenseAsync(vouNo);
            }
        }
        else if (moduleTitle == "Payments")
        {
            var vouNo = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(vouNo) && _paymentsViewModel != null)
            {
                _ = _paymentsViewModel.Value.SelectVoucherByVouNoAsync(vouNo);
            }
        }
        else if (moduleTitle == "Receipts")
        {
            var vouNo = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(vouNo) && _receiptsViewModel != null)
            {
                _ = _receiptsViewModel.Value.SelectVoucherByVouNoAsync(vouNo);
            }
        }
        else if (moduleTitle == "Journal")
        {
            var vouNo = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(vouNo) && _journalViewModel != null)
            {
                _ = _journalViewModel.Value.SelectVoucherByVouNoAsync(vouNo);
            }
        }
        else if (moduleTitle == "Scheme Receipts")
        {
            var vouNo = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(vouNo) && _schemeReceiptViewModel != null)
            {
                _ = _schemeReceiptViewModel.Value.SelectReceiptByVouNoAsync(vouNo);
            }
        }
        else if (moduleTitle == "Karighar Issue")
        {
            var vouNo = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(vouNo) && _karigharIssueViewModel != null)
            {
                _ = _karigharIssueViewModel.Value.SelectIssueByVouNoAsync(vouNo);
            }
        }
        else if (moduleTitle == "Karighar Received")
        {
            var vouNo = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(vouNo) && _karigharReceiveViewModel != null)
            {
                _ = _karigharReceiveViewModel.Value.SelectReceiveByVouNoAsync(vouNo);
            }
        }
        else if ((moduleTitle == "Barcode Creation" || moduleTitle == "BarcodeCreation") && _barcodeCreationViewModel != null)
        {
            var barcode = parameter?.ToString();
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                _ = _barcodeCreationViewModel.Value.SearchAndLoadBarcodeAsync(barcode);
            }
        }
        else if ((moduleTitle is "Barcode Reports" or "Barcode Stock Reports" or "Barcode Stock" or "Barcode Report" or "BARCODE STOCK REPORTS") && _barcodeStockReportViewModel != null)
        {
            var barcode = parameter?.ToString();
            var vm = _barcodeStockReportViewModel.Value;
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                vm.SearchText = barcode.Trim();
            }
            _ = vm.RefreshAllAsync();
        }
    }

    private void SelectInvoiceModule(object? parameter)
    {
        CloseAllMenus();

        if (!IsPermitted("Invoice"))
        {
            return;
        }

        var paramVouNo = parameter?.ToString()?.Trim();

        if (!string.IsNullOrWhiteSpace(paramVouNo))
        {
            var existingVouDoc = Documents.FirstOrDefault(doc =>
                doc.Content is InvoiceViewModel ivm &&
                string.Equals(ivm.VouNo, paramVouNo, StringComparison.OrdinalIgnoreCase));

            if (existingVouDoc != null)
            {
                SelectedDocument = existingVouDoc;
                return;
            }

            var newVm = _invoiceViewModelFactory();
            var sessionDoc = new ModuleDocument($"Invoice #{paramVouNo}", AppIcons.Invoice, newVm);

            newVm.SessionTitleChanged += (_, _) =>
            {
                sessionDoc.Title = newVm.SessionTitle;
            };

            Documents.Add(sessionDoc);
            SelectedDocument = sessionDoc;
            _ = newVm.LoadBillAsync(paramVouNo);
            return;
        }

        var freshVm = _invoiceViewModelFactory();
        var invoiceSessionCount = Documents.Count(d => d.Content is InvoiceViewModel) + 1;
        var defaultTitle = invoiceSessionCount > 1 ? $"Invoice (Session {invoiceSessionCount})" : "Invoice";
        var newDoc = new ModuleDocument(defaultTitle, AppIcons.Invoice, freshVm);

        freshVm.SessionTitleChanged += (_, _) =>
        {
            if (freshVm.IsExistingInvoice || !string.IsNullOrWhiteSpace(freshVm.CustomerName) || !string.IsNullOrWhiteSpace(freshVm.VouNo))
            {
                newDoc.Title = freshVm.SessionTitle;
            }
        };

        Documents.Add(newDoc);
        SelectedDocument = newDoc;
    }

    private void OnCatalogueOpenBarcodeRequested(object? sender, string barcode)
    {
        SelectModule("Barcode Creation", barcode);
    }

    private void OpenGlobalSearchResult(GlobalSearchResultViewModel? result)
    {
        if (result is null) return;

        IsGlobalSearchOpen = false;
        SearchText = string.Empty;
        GlobalSearchResults.Clear();
        SelectedSearchResult = null;

        SelectModule(result.NavigationKey, result.NavigationParameter);
    }

    public void SelectNextSearchResult()
    {
        if (GlobalSearchResults.Count == 0)
            return;

        if (SelectedSearchResult is null)
        {
            SelectedSearchResult = GlobalSearchResults[0];
            return;
        }

        var index = GlobalSearchResults.IndexOf(SelectedSearchResult);
        if (index < GlobalSearchResults.Count - 1)
        {
            SelectedSearchResult = GlobalSearchResults[index + 1];
        }
    }

    public void SelectPreviousSearchResult()
    {
        if (GlobalSearchResults.Count == 0)
            return;

        if (SelectedSearchResult is null)
        {
            SelectedSearchResult = GlobalSearchResults[0];
            return;
        }

        var index = GlobalSearchResults.IndexOf(SelectedSearchResult);
        if (index > 0)
        {
            SelectedSearchResult = GlobalSearchResults[index - 1];
        }
    }

    private void CloseGlobalSearchPopup()
    {
        _globalSearchCts?.Cancel();
        GlobalSearchResults.Clear();
        SelectedSearchResult = null;
    }

    private async Task RefreshGlobalSearchAsync(string value)
    {
        _globalSearchCts?.Cancel();
        _globalSearchCts?.Dispose();
        _globalSearchCts = new CancellationTokenSource();
        var token = _globalSearchCts.Token;

        var searchText = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            GlobalSearchResults.Clear();
            SelectedSearchResult = null;
            IsGlobalSearchOpen = false;
            return;
        }

        IsGlobalSearchOpen = true;

        try
        {
            await Task.Delay(180, token);
            await EnsureGlobalSearchCachesAsync(token);
            var results = BuildGlobalSearchResults(searchText);

            if (token.IsCancellationRequested)
                return;

            GlobalSearchResults.Clear();
            foreach (var result in results)
            {
                GlobalSearchResults.Add(result);
            }

            SelectedSearchResult = GlobalSearchResults.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            GlobalSearchResults.Clear();
            SelectedSearchResult = null;
        }
    }

    private async Task EnsureGlobalSearchCachesAsync(CancellationToken cancellationToken)
    {
        if (_searchAccounts is not null && _searchCustomers is not null && _searchItems is not null)
            return;

        var accountsTask = _searchAccounts is null && _accountRepository != null
            ? _accountRepository.GetAllAsync(cancellationToken)
            : Task.FromResult(_searchAccounts ?? (IReadOnlyList<Account>)Array.Empty<Account>());

        var customersTask = _searchCustomers is null && _customerRepository != null
            ? _customerRepository.GetAllAsync(cancellationToken)
            : Task.FromResult(_searchCustomers ?? (IReadOnlyList<Customer>)Array.Empty<Customer>());

        var itemsTask = _searchItems is null && _itemRepository != null
            ? _itemRepository.GetAllAsync(cancellationToken)
            : Task.FromResult(_searchItems ?? (IReadOnlyList<Item>)Array.Empty<Item>());

        _searchAccounts ??= await accountsTask;
        _searchCustomers ??= await customersTask;
        _searchItems ??= await itemsTask;
    }

    private List<GlobalSearchResultViewModel> BuildGlobalSearchResults(string searchText)
    {
        var results = new List<GlobalSearchResultViewModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddMenuResults(results, seen, searchText);
        AddAccountResults(results, seen, searchText);
        AddCustomerResults(results, seen, searchText);
        AddItemResults(results, seen, searchText);

        return results
            .Take(15)
            .ToList();
    }

    private void AddMenuResults(List<GlobalSearchResultViewModel> results, HashSet<string> seen, string searchText)
    {
        foreach (var menu in OrderByMatchQuality(
                     _allMenuItems,
                     searchText,
                     menu => new[] { menu.Title, menu.Subtitle }))
        {
            AddSearchResult(results, seen, new GlobalSearchResultViewModel
            {
                Title = menu.Title,
                Detail = menu.Subtitle,
                NavigationKey = menu.Title,
                NavigationParameter = null,
                Icon = menu.Icon
            });
        }
    }

    private void AddAccountResults(List<GlobalSearchResultViewModel> results, HashSet<string> seen, string searchText)
    {
        if (_searchAccounts is null)
            return;

        foreach (var account in OrderByMatchQuality(
                     _searchAccounts,
                     searchText,
                     account => new[] { account.AccountName, account.RefName, account.PartyCode, account.Mobile, account.PhoneNo, account.City }))
        {
            AddSearchResult(results, seen, new GlobalSearchResultViewModel
            {
                Title = "Account Creation",
                Detail = string.IsNullOrWhiteSpace(account.PartyCode) ? account.AccountName : $"{account.AccountName} ({account.PartyCode})",
                NavigationKey = "Account Creation",
                NavigationParameter = string.IsNullOrWhiteSpace(account.PartyCode) ? account.AccountName : account.PartyCode,
                Icon = AppIcons.AccountCreation
            });

            AddSearchResult(results, seen, new GlobalSearchResultViewModel
            {
                Title = "Ledger Report",
                Detail = account.AccountName,
                NavigationKey = "Ledger Reports",
                NavigationParameter = account.AccountName,
                Icon = AppIcons.LedgerReports
            });
        }
    }

    private void AddCustomerResults(List<GlobalSearchResultViewModel> results, HashSet<string> seen, string searchText)
    {
        if (_searchCustomers is null)
            return;

        foreach (var customer in OrderByMatchQuality(
                     _searchCustomers,
                     searchText,
                     customer => new[] { customer.CompanyName, customer.Code, customer.Mobile, customer.Phone, customer.City }))
        {
            AddSearchResult(results, seen, new GlobalSearchResultViewModel
            {
                Title = "Customer Creation",
                Detail = string.IsNullOrWhiteSpace(customer.Code) ? customer.CompanyName : $"{customer.CompanyName} ({customer.Code})",
                NavigationKey = "Customer Creation",
                NavigationParameter = string.IsNullOrWhiteSpace(customer.Code) ? customer.CompanyName : customer.Code,
                Icon = AppIcons.CustomerCreation
            });
        }
    }

    private void AddItemResults(List<GlobalSearchResultViewModel> results, HashSet<string> seen, string searchText)
    {
        if (_searchItems is null)
            return;

        foreach (var item in OrderByMatchQuality(
                     _searchItems,
                     searchText,
                     item => new[] { item.ItemName, item.ItemCode, item.Group, item.SubGroup }))
        {
            AddSearchResult(results, seen, new GlobalSearchResultViewModel
            {
                Title = "Item Creation",
                Detail = string.IsNullOrWhiteSpace(item.ItemCode) ? item.ItemName : $"{item.ItemName} ({item.ItemCode})",
                NavigationKey = "Item Creation",
                NavigationParameter = string.IsNullOrWhiteSpace(item.ItemCode) ? item.ItemName : item.ItemCode,
                Icon = AppIcons.ItemCreation
            });
        }
    }

    private static IEnumerable<T> OrderByMatchQuality<T>(IEnumerable<T> source, string searchText, Func<T, IEnumerable<string?>> valueSelector)
    {
        return source
            .Select(item => new
            {
                Item = item,
                Score = GetBestScore(valueSelector(item), searchText)
            })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .ThenBy(x => GetPrimaryValue(valueSelector(x.Item)), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Item);
    }

    private static int GetBestScore(IEnumerable<string?> values, string searchText)
    {
        var best = int.MaxValue;
        foreach (var value in values)
        {
            var score = GetScore(value, searchText);
            if (score < best)
                best = score;
        }

        return best;
    }

    private static int GetScore(string? value, string searchText)
    {
        if (string.IsNullOrWhiteSpace(value))
            return int.MaxValue;

        var candidate = value.Trim();
        if (candidate.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            return 0;

        if (candidate.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
            return 1;

        if (candidate.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
            return 2;

        return int.MaxValue;
    }

    private static string GetPrimaryValue(IEnumerable<string?> values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static void AddSearchResult(
        ICollection<GlobalSearchResultViewModel> results,
        ISet<string> seen,
        GlobalSearchResultViewModel result)
    {
        var key = $"{result.NavigationKey}|{result.Detail}|{result.NavigationParameter}";
        if (!seen.Add(key))
            return;

        results.Add(result);
    }

    private void CloseDocument(ModuleDocument? document)
    {
        if (document is null)
        {
            return;
        }

        var index = Documents.IndexOf(document);
        if (index < 0)
        {
            return;
        }

        if (document.Content is IConfirmCloseDocument confirmable && confirmable.HasUnsavedChanges())
        {
            var prompt = confirmable.CloseConfirmationMessage ??
                         $"You have unsaved changes in \"{document.Title}\".\nAre you sure you want to close this tab and discard changes?";
            var result = System.Windows.MessageBox.Show(
                prompt,
                "Discard Changes?",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }

        Documents.RemoveAt(index);
        if (SelectedDocument == document)
        {
            SelectedDocument = Documents.Count == 0
                ? null
                : Documents[Math.Min(index, Documents.Count - 1)];
        }
    }

    public bool IsPermitted(string moduleTitle)
    {
        if (CurrentUser.Role == UserRole.Administrator ||
            string.Equals(CurrentUser.Username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasModulePermission(moduleTitle);
    }

    private bool HasModulePermission(string moduleTitle)
    {
        if (CurrentUserPermissions is null)
        {
            return true;
        }

        return moduleTitle switch
        {
            // Master Creations (Add Permissions)
            "Account Creation" => CurrentUserPermissions.LedAdd,
            "Customer Creation" => CurrentUserPermissions.CusAdd,
            "Item Creation" => CurrentUserPermissions.ItemAdd,
            "Staff Creation" => CurrentUserPermissions.UserAdd || CurrentUserPermissions.ItemAdd,
            "Purity Masters" => CurrentUserPermissions.ItemAdd,
            "Purity Master" => CurrentUserPermissions.ItemAdd,
            "Rate Update" => CurrentUserPermissions.ItemAdd || CurrentUserPermissions.PurAdd,
            "Rate Master" => CurrentUserPermissions.ItemAdd || CurrentUserPermissions.PurAdd,
            "Voucher Types" => CurrentUserPermissions.UserAdd || CurrentUserPermissions.SaleAdd || CurrentUserPermissions.ItemAdd,
            "Sales Voucher Types" => CurrentUserPermissions.UserAdd || CurrentUserPermissions.SaleAdd || CurrentUserPermissions.ItemAdd,
            "Voucher Type Configuration" => CurrentUserPermissions.UserAdd || CurrentUserPermissions.SaleAdd || CurrentUserPermissions.ItemAdd,

            // Entries (Add Permissions)
            "Invoice" => CurrentUserPermissions.SaleAdd,
            "Stock Transfer" => CurrentUserPermissions.NoteTransfer,
            "Purchase" => CurrentUserPermissions.PurAdd,
            "Purchase Register" => CurrentUserPermissions.PurAdd || CurrentUserPermissions.BarcodeStock,
            "Purchases Expenses" => CurrentUserPermissions.PurAdd || CurrentUserPermissions.PayAdd,
            "Barcode Creation" => CurrentUserPermissions.BarAdd || CurrentUserPermissions.PurAdd || CurrentUserPermissions.ItemAdd,
            "Barcode Creation (Bulk)" => CurrentUserPermissions.BarAdd || CurrentUserPermissions.PurAdd || CurrentUserPermissions.ItemAdd,
            "Non-Barcode Stock" => CurrentUserPermissions.PurAdd || CurrentUserPermissions.BarAdd || CurrentUserPermissions.ItemAdd,
            "Non Barcode Stock" => CurrentUserPermissions.PurAdd || CurrentUserPermissions.BarAdd || CurrentUserPermissions.ItemAdd,
            "Catalogue" => CurrentUserPermissions.BarcodeStock || CurrentUserPermissions.BarAdd || CurrentUserPermissions.ItemAdd || CurrentUserPermissions.SaleAdd,
            "Karighar Issue" => CurrentUserPermissions.MIssAdd,
            "Karighar Received" => CurrentUserPermissions.MRecAdd,
            "Hire Issue" => CurrentUserPermissions.HIssAdd || CurrentUserPermissions.MIssAdd,
            "Hire Receive" => CurrentUserPermissions.HRecAdd || CurrentUserPermissions.MRecAdd,
            "Scheme Creation" => CurrentUserPermissions.ScAdd,

            // Vouchers (Add Permissions)
            "Payments" => CurrentUserPermissions.PayAdd,
            "Receipts" => CurrentUserPermissions.RecAdd,
            "Journal" => CurrentUserPermissions.JouAdd,
            "Scheme Receipts" => CurrentUserPermissions.ScAdd,
            "Bank Reconciliation" => CurrentUserPermissions.PayAdd || CurrentUserPermissions.RecAdd || CurrentUserPermissions.LedgerIndividual,

            // Reports
            "Scheme Due List" => CurrentUserPermissions.ScAdd || CurrentUserPermissions.LedgerIndividual || CurrentUserPermissions.BarcodeStock,
            "Barcode Reports" => CurrentUserPermissions.BarcodeStock || CurrentUserPermissions.LabelPrinting,
            "Barcode Stock Reports" => CurrentUserPermissions.BarcodeStock || CurrentUserPermissions.LabelPrinting,
            "Barcode Stock" => CurrentUserPermissions.BarcodeStock || CurrentUserPermissions.LabelPrinting,
            "NonBarcode Stock reports" => CurrentUserPermissions.BarcodeStock || CurrentUserPermissions.PurAdd || CurrentUserPermissions.SaleAdd,
            "NonBarcode Stock Reports" => CurrentUserPermissions.BarcodeStock || CurrentUserPermissions.PurAdd || CurrentUserPermissions.SaleAdd,
            "ItemType Stock Report" => CurrentUserPermissions.BarcodeStock || CurrentUserPermissions.PurAdd || CurrentUserPermissions.SaleAdd,
            "Non-Barcode Reports" => CurrentUserPermissions.BarcodeStock || CurrentUserPermissions.PurAdd || CurrentUserPermissions.SaleAdd,
            "Non-Barcode Stock Report" => CurrentUserPermissions.BarcodeStock || CurrentUserPermissions.PurAdd || CurrentUserPermissions.SaleAdd,
            "Ledger Reports" => CurrentUserPermissions.LedgerIndividual || CurrentUserPermissions.LedgerGroup || CurrentUserPermissions.MetalWiseLedger || CurrentUserPermissions.KarigarLedger || CurrentUserPermissions.TrialBalance,
            "Ledger Group" => CurrentUserPermissions.LedgerGroup || CurrentUserPermissions.LedgerIndividual || CurrentUserPermissions.TrialBalance,
            "Trial Balance" => CurrentUserPermissions.TrialBalance || CurrentUserPermissions.LedgerIndividual || CurrentUserPermissions.LedgerGroup,
            "Trading Account" => CurrentUserPermissions.Trading || CurrentUserPermissions.BalanceSheet || CurrentUserPermissions.TrialBalance || CurrentUserPermissions.LedgerIndividual || CurrentUserPermissions.LedgerGroup,
            "Balance Sheet" => CurrentUserPermissions.BalanceSheet || CurrentUserPermissions.TrialBalance || CurrentUserPermissions.LedgerIndividual || CurrentUserPermissions.LedgerGroup,
            "Audit Report" => CurrentUserPermissions.AuditView || CurrentUserPermissions.UserAdd,

            // Settings
            "User Permission" => CurrentUserPermissions.UserAdd,
            "General Settings" => CurrentUserPermissions.UserAdd || CurrentUserPermissions.AuditView,
            "Printer Settings" => CurrentUserPermissions.UserAdd || CurrentUserPermissions.LabelPrinting,
            "Backup Settings" => CurrentUserPermissions.UserAdd,
            "Backup" => CurrentUserPermissions.UserAdd,
            "Database Backup" => CurrentUserPermissions.UserAdd,
            "BackUp" => CurrentUserPermissions.UserAdd,
            "Software Updates" => CurrentUserPermissions.UserAdd,

            _ => true
        };
    }

    private void OnOpenPurchaseRequested(object? sender, PurchaseRegisterItem item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.VouNo)) return;
        SelectModule("Purchase", item.VouNo);
    }

    private void OnSchemeCollectPaymentRequested(object? sender, string scode)
    {
        SelectModule("Scheme Receipts");
        if (_schemeReceiptViewModel != null)
        {
            _schemeReceiptViewModel.Value.Scode = scode;
        }
    }

    private void OnOpenAccountLedgerRequested(object? sender, string accountName)
    {
        SelectModule("Ledger Reports");
        if (_ledgerReportViewModel != null)
        {
            _ledgerReportViewModel.Value.SelectedAccountName = accountName;
        }
    }

    private void OnOpenVoucherRequested(object? sender, LedgerEntry entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.VouNo) || string.Equals(entry.VouType, "Opening Balance", StringComparison.OrdinalIgnoreCase))
            return;

        var vouType = entry.VouType?.Trim() ?? string.Empty;
        var vouNo = entry.VouNo?.Trim() ?? string.Empty;

        string targetModule;

        if (vouType.StartsWith("PAY", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("PAY", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Payments";
        }
        else if (vouType.StartsWith("REC", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("REC", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Receipts";
        }
        else if (vouType.StartsWith("JOURNAL", StringComparison.OrdinalIgnoreCase) || vouType.StartsWith("JV", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("JV", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Journal";
        }
        else if (vouType.StartsWith("CONTRA", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("CON", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Payments";
        }
        else if (vouType.Contains("EXPENSE", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("PEX", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Purchases Expenses";
        }
        else if (vouType.Contains("PURCHASE", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("PV", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Purchase";
        }
        else if (vouType.Contains("SCHEME", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("SCH", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("SC", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Scheme Receipts";
        }
        else if (vouType.Contains("KARIGHAR ISSUE", StringComparison.OrdinalIgnoreCase) || vouType.Contains("METAL ISSUED", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("KI", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Karighar Issue";
        }
        else if (vouType.Contains("KARIGHAR RECEIVE", StringComparison.OrdinalIgnoreCase) || vouType.Contains("METAL RECEIVED", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("KR", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("MR", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Karighar Received";
        }
        else if (vouType.Contains("HIRE ISSUE", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("HI", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Hire Issue";
        }
        else if (vouType.Contains("HIRE RECEIVE", StringComparison.OrdinalIgnoreCase) || vouNo.StartsWith("HR", StringComparison.OrdinalIgnoreCase))
        {
            targetModule = "Hire Receive";
        }
        else
        {
            // Default to Invoice (Retail Invoice, Tax Invoice, Estimate, SVN..., etc.)
            targetModule = "Invoice";
        }

        SelectModule(targetModule, vouNo);
    }

    public void OpenRateUpdate()
    {
        if (_purityRepository is null) return;
        var vm = new RateUpdateViewModel(_purityRepository, CurrentUser.Username, _auditRepository);
        var win = new Views.RateUpdateWindow(vm);
        win.SetOwnerSafely();
        if (win.ShowDialog() == true)
        {
            if (_purityMasterViewModel?.IsValueCreated == true)
            {
                _ = _purityMasterViewModel.Value.LoadDataAsync();
            }
            if (_dashboardViewModel?.IsValueCreated == true)
            {
                _ = _dashboardViewModel.Value.LoadDashboardDataAsync();
            }
        }
    }

    private void OnDashboardNavigateModule(object? sender, string module) => SelectModule(module);
    private void OnDashboardOpenOrder(object? sender, string orderNo) => SelectModule("Order Requirement", orderNo);
    private void OnDashboardOpenSchemeReceipt(object? sender, string scode) => SelectModule("Scheme Receipts", scode);
    private void OnDashboardOpenRateUpdate(object? sender, EventArgs e) => OpenRateUpdate();

    private void ToggleMenu(TopMenuCategory? menu)
    {
        if (menu is null) return;
        var wasOpen = menu.IsOpen;
        CloseAllMenus();
        menu.IsOpen = !wasOpen;
    }

    public void CloseAllMenus()
    {
        if (TopMenus is null) return;
        foreach (var menu in TopMenus)
        {
            menu.IsOpen = false;
        }
    }

    private IReadOnlyList<TopMenuCategory> BuildMenu()
    {
        _allMenuItems.Clear();
        var menus = new List<TopMenuCategory>();

        // 0. Overview / Dashboard
        var overviewSections = new List<MenuSection>();
        var dashboardSection = CreateSection("Showroom", AppIcons.Dashboard, FilterEntries(new[]
        {
            new MenuEntry("Dashboard", "Showroom executive overview, KPIs, and daily vitals", AppIcons.Dashboard)
        }));
        if (dashboardSection.Items.Count > 0) overviewSections.Add(dashboardSection);
        if (overviewSections.Count > 0)
        {
            menus.Add(new TopMenuCategory("Dashboard", AppIcons.Dashboard, overviewSections));
        }

        // 1. Masters
        var masterSections = new List<MenuSection>();
        var accountStaff = CreateSection("Accounts & Staff", AppIcons.AccountCreation, FilterEntries(new[]
        {
            new MenuEntry("Account Creation", "Create and manage ledger accounts", AppIcons.AccountCreation),
            new MenuEntry("Customer Creation", "Create and manage customer records", AppIcons.CustomerCreation),
            new MenuEntry("Staff Creation", "Create and manage staff members", AppIcons.StaffCreation)
        }));
        if (accountStaff.Items.Count > 0) masterSections.Add(accountStaff);

        var itemsPurity = CreateSection("Items & Purity", AppIcons.ItemCreation, FilterEntries(new[]
        {
            new MenuEntry("Item Creation", "Create and manage item master records", AppIcons.ItemCreation),
            new MenuEntry("Purity Masters", "Configure purity rates, item heads and types", AppIcons.PurityMaster),
            new MenuEntry("Rate Update", "Update daily metal rates per gram across purities (F2)", AppIcons.PurityMaster),
            new MenuEntry("Box Master", "Manage physical stock boxes, rack storage positions and serials", AppIcons.StockTransfer)
        }));
        if (itemsPurity.Items.Count > 0) masterSections.Add(itemsPurity);

        var savingSchemes = CreateSection("Saving Schemes & Promotions", AppIcons.SchemeCreation, FilterEntries(new[]
        {
            new MenuEntry("Scheme Creation", "Create and manage customer saving schemes", AppIcons.SchemeCreation),
            new MenuEntry("Coupon Engine", "Manage coupon campaigns, customer targeting & WhatsApp dispatch", AppIcons.SchemeCreation)
        }));
        if (savingSchemes.Items.Count > 0) masterSections.Add(savingSchemes);

        if (masterSections.Count > 0)
        {
            menus.Add(new TopMenuCategory("Masters", AppIcons.MasterCreations, masterSections));
        }

        // 2. Entries
        var entryColumns = new List<MenuColumn>();
        var salesTransfers = CreateSection("Sales & Transfers", AppIcons.Invoice, FilterEntries(new[]
        {
            new MenuEntry("Invoice", "Generate sales tax invoices & retail bills", AppIcons.Invoice),
            new MenuEntry("Stock Transfer", "Move stock between stores and locations", AppIcons.StockTransfer)
        }));

        var customerOrders = CreateSection("Customer Orders", AppIcons.Entries, FilterEntries(new[]
        {
            new MenuEntry("Order/Customer Requirements", "Customer jewellery orders, specifications & tracking", AppIcons.Entries)
        }));

        var salesAndOrders = new MenuColumn(salesTransfers, customerOrders);
        if (salesAndOrders.HasItems) entryColumns.Add(salesAndOrders);

        var stockBarcode = CreateSection("Barcode & Stock", AppIcons.BarcodeCreation, FilterEntries(new[]
        {
            new MenuEntry("Barcode Creation", "Single item opening stock barcode creation (Vou 9)", AppIcons.BarcodeCreation),
            new MenuEntry("Barcode Creation (Bulk)", "Voucher-wise bulk stock barcode creation", AppIcons.BarcodeCreationBulk),
            new MenuEntry("Non-Barcode Stock", "Add and manage non-barcoded stock items", AppIcons.NonBarcodeStock),
            new MenuEntry("Catalogue", "Browse jewellery catalogue with photos & weight filters", AppIcons.Catalogue)
        }));
        if (stockBarcode.Items.Count > 0) entryColumns.Add(new MenuColumn(stockBarcode));

        var purchases = CreateSection("Purchases", AppIcons.Purchase, FilterEntries(new[]
        {
            new MenuEntry("Purchase", "Record supplier purchases & inward goods", AppIcons.Purchase),
            new MenuEntry("Purchase Register", "View & filter purchase records, detailed reports and print", AppIcons.Reports),
            new MenuEntry("Purchases Expenses", "Record purchase related expenses & charges", AppIcons.PurchasesExpenses)
        }));
        if (purchases.Items.Count > 0) entryColumns.Add(new MenuColumn(purchases));

        var karigharHire = CreateSection("Karighar & Hire", AppIcons.KarigharIssue, FilterEntries(new[]
        {
            new MenuEntry("Karighar Issue", "Issue metal/goods to karighar (contractor)", AppIcons.KarigharIssue),
            new MenuEntry("Karighar Received", "Receive finished goods from karighar", AppIcons.KarigharReceived),
            new MenuEntry("Hire Issue", "Issue metal/goods on hire to artisans", AppIcons.KarigharIssue),
            new MenuEntry("Hire Receive", "Receive metal/goods returned from hire", AppIcons.KarigharReceived)
        }));
        if (karigharHire.Items.Count > 0) entryColumns.Add(new MenuColumn(karigharHire));

        if (entryColumns.Count > 0)
        {
            menus.Add(new TopMenuCategory("Entries", AppIcons.Entries, entryColumns));
        }

        // 3. Vouchers
        var voucherSections = new List<MenuSection>();
        var financialVouchers = CreateSection("Financial Vouchers", AppIcons.Payments, FilterEntries(new[]
        {
            new MenuEntry("Receipts", "Record cash and bank receipts from parties", AppIcons.Receipts),
            new MenuEntry("Payments", "Record cash and bank payments to parties", AppIcons.Payments),
            new MenuEntry("Journal", "Record journal adjustments & transfers", AppIcons.Journal)
        }));
        if (financialVouchers.Items.Count > 0) voucherSections.Add(financialVouchers);

        var schemeVouchers = CreateSection("Scheme Receipts", AppIcons.SchemeReceipts, FilterEntries(new[]
        {
            new MenuEntry("Scheme Receipts", "Record monthly scheme installment receipts", AppIcons.SchemeReceipts)
        }));
        if (schemeVouchers.Items.Count > 0) voucherSections.Add(schemeVouchers);

        var bankingSection = CreateSection("Banking & Reconciliation", AppIcons.BankReconciliation, FilterEntries(new[]
        {
            new MenuEntry("Bank Reconciliation", "Reconcile official bank statements with ERP ledger", AppIcons.BankReconciliation),
            new MenuEntry("Card Reconciliation", "Reconcile card swipes and settlement receipts", AppIcons.CardReconciliation)
        }));
        if (bankingSection.Items.Count > 0) voucherSections.Add(bankingSection);

        if (voucherSections.Count > 0)
        {
            menus.Add(new TopMenuCategory("Vouchers", AppIcons.Vouchers, voucherSections));
        }

        // 4. Reports
        var reportSections = new List<MenuSection>();
        var stockReports = CreateSection("Stock Reports", AppIcons.BarcodeReports, FilterEntries(new[]
        {
            new MenuEntry("Barcode Reports", "Generate barcode stock labels and reports", AppIcons.BarcodeReports),
            new MenuEntry("Catalogue", "Browse jewellery catalogue with photos & filters", AppIcons.Catalogue),
            new MenuEntry("NonBarcode Stock reports", "Item-wise non-barcoded stock summaries and transactions", AppIcons.BarcodeReports),
            new MenuEntry("ItemType Stock Report", "ItemType stock summaries and transaction ledger", AppIcons.Reports),
            new MenuEntry("Purchase Register", "Itemwise & Daywise purchase register reports", AppIcons.Reports)
        }));
        if (stockReports.Items.Count > 0) reportSections.Add(stockReports);

        var financialReports = CreateSection("Financial Reports", AppIcons.Reports, FilterEntries(new[]
        {
            new MenuEntry("Trial Balance", "View group-wise & detailed trial balance", AppIcons.Reports),
            new MenuEntry("Trading Account", "Trading account with dual-column metal weights, COGS & gross margin", AppIcons.Reports),
            new MenuEntry("Balance Sheet", "Statement of assets, liabilities, and capital with P&L transfer", AppIcons.Reports)
        }));
        if (financialReports.Items.Count > 0) reportSections.Add(financialReports);

        var accountReports = CreateSection("Account Reports", AppIcons.LedgerReports, FilterEntries(new[]
        {
            new MenuEntry("Ledger Reports", "Print and view individual account ledgers", AppIcons.LedgerReports),
            new MenuEntry("Ledger Group", "HeadCategory group-wise account summaries and statements", AppIcons.LedgerReports)
        }));
        if (accountReports.Items.Count > 0) reportSections.Add(accountReports);

        var schemeAudit = CreateSection("Scheme & Audit", AppIcons.SchemeReceipts, FilterEntries(new[]
        {
            new MenuEntry("Scheme Due List", "Track missed, upcoming, and pending scheme installments", AppIcons.SchemeReceipts),
            new MenuEntry("Audit Report", "Track modifications and deletions across modules", AppIcons.Reports)
        }));
        if (schemeAudit.Items.Count > 0) reportSections.Add(schemeAudit);

        var gstReports = CreateSection("GST Reports & Compliance", AppIcons.Reports, FilterEntries(new[]
        {
            new MenuEntry("GST Reports & Reconciliation", "Statutory returns (GSTR-1, GSTR-3B, GSTR-2) & GSTR-2B reconciliation", AppIcons.Reports)
        }));
        if (gstReports.Items.Count > 0) reportSections.Add(gstReports);

        if (reportSections.Count > 0)
        {
            menus.Add(new TopMenuCategory("Reports", AppIcons.Reports, reportSections));
        }

        // 5. Settings
        var settingSections = new List<MenuSection>();
        var systemSettings = CreateSection("System & Printing", AppIcons.GeneralSettings, FilterEntries(new[]
        {
            new MenuEntry("General Settings", "Configure application preferences and defaults", AppIcons.GeneralSettings),
            new MenuEntry("Voucher Types", "Configure invoice types, voucher prefixes, accounts and tax", AppIcons.SalesVouType),
            new MenuEntry("Printer Settings", "Configure invoice, barcode and receipt printers", AppIcons.PrinterSettings),
            new MenuEntry("Backup Settings", "Database backup, restore and auto-backup schedules", AppIcons.BackupSettings)
        }));
        if (systemSettings.Items.Count > 0) settingSections.Add(systemSettings);

        var securitySettings = CreateSection("Security & Software", AppIcons.UserPermission, FilterEntries(new[]
        {
            new MenuEntry("User Permission", "Manage user roles, rights and security permissions", AppIcons.UserPermission),
            new MenuEntry("License Information", "View active license status, expiry, and authorized computers", "\uE8D7"),
            new MenuEntry("Software Updates", "Check for updates and version information", AppIcons.SoftwareUpdates)
        }));
        if (securitySettings.Items.Count > 0) settingSections.Add(securitySettings);

        if (settingSections.Count > 0)
        {
            menus.Add(new TopMenuCategory("Settings", AppIcons.Settings, settingSections));
        }

        return menus;
    }

    private bool IsModuleLicensed(string? moduleTitle)
    {
        return _licenseValidationService is null || _licenseValidationService.IsScreenLicensed(moduleTitle);
    }

    private IReadOnlyList<MenuEntry> FilterEntries(IEnumerable<MenuEntry> entries)
    {
        var list = entries.Where(e => IsModuleLicensed(e.Title) && IsPermitted(e.Title)).ToList();
        _allMenuItems.AddRange(list);
        return list;
    }

    private static MenuSection CreateSection(string title, string icon, IReadOnlyList<MenuEntry> entries)
    {
        return new MenuSection(title, icon, entries);
    }
}
