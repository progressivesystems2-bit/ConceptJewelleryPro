using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ConceptJewelleryPro.Application.Authentication;
using ConceptJewelleryPro.Application.Services;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Accounts;
using ConceptJewelleryPro.Infrastructure.Authentication;
using ConceptJewelleryPro.Infrastructure.Printing;
using ConceptJewelleryPro.Infrastructure.Security;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Services;
using ConceptJewelleryPro.WPF.Themes;
using ConceptJewelleryPro.WPF.ViewModels;
using ConceptJewelleryPro.WPF.Views;

namespace ConceptJewelleryPro.WPF;

public partial class App : System.Windows.Application
{
    private PasswordHasher? _passwordHasher;
    private IAuthenticationService? _authenticationService;
    private DatabaseSettingsStore? _settingsStore;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
        {
            try
            {
                var requestedName = new System.Reflection.AssemblyName(args.Name).Name;
                if (string.IsNullOrWhiteSpace(requestedName))
                    return null;

                var candidateFolders = new[]
                {
                    AppContext.BaseDirectory,
                    Path.GetDirectoryName(typeof(App).Assembly.Location),
                    AppDomain.CurrentDomain.BaseDirectory
                };

                foreach (var folder in candidateFolders)
                {
                    if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                        continue;

                    var targetFile = Path.Combine(folder, requestedName + ".dll");
                    if (File.Exists(targetFile))
                    {
                        return System.Reflection.Assembly.LoadFrom(targetFile);
                    }
                }
            }
            catch { }
            return null;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var msg = $"Fatal startup/runtime error:\n\n{ex?.Message}\n\nDetails:\n{ex}";
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup_error.log"), msg);
            }
            catch { }
            MessageBox.Show(msg, "Concept Jewellery Pro - Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            var msg = $"Application error:\n\n{args.Exception.Message}\n\nDetails:\n{args.Exception}";
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup_error.log"), msg);
            }
            catch { }
            MessageBox.Show(msg, "Concept Jewellery Pro - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        EventManager.RegisterClassHandler(
            typeof(System.Windows.UIElement),
            System.Windows.UIElement.PreviewKeyDownEvent,
            new KeyEventHandler(OnPreviewKeyDown), true);

        // Global auto-select whole value on focus for all TextBoxes, WatermarkTextBoxes, etc.
        EventManager.RegisterClassHandler(
            typeof(TextBoxBase),
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnTextBoxBaseGotKeyboardFocus), true);

        EventManager.RegisterClassHandler(
            typeof(TextBoxBase),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnTextBoxBasePreviewMouseLeftButtonDown), true);

        EventManager.RegisterClassHandler(
            typeof(Telerik.Windows.Controls.RadNumericUpDown),
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnRadNumericUpDownGotKeyboardFocus), true);

        EventManager.RegisterClassHandler(
            typeof(Telerik.Windows.Controls.RadNumericUpDown),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnRadNumericUpDownLoaded), true);

        EventManager.RegisterClassHandler(
            typeof(Telerik.Windows.Controls.RadNumericUpDown),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnRadNumericUpDownPreviewMouseWheel), true);

        EventManager.RegisterClassHandler(
            typeof(Telerik.Windows.Controls.RadComboBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnRadComboBoxLoaded), true);

        _passwordHasher = new PasswordHasher();
        _settingsStore = new DatabaseSettingsStore(DatabaseSettingsStore.DefaultFilePath);
        var userRepository = new AccessUserRepository(() => ResolveProjectDatabasePath(_settingsStore), _passwordHasher);
        _authenticationService = new LoginService(userRepository, _passwordHasher);

        ThemeManager.Apply(ThemeManager.LoadSavedThemeId());

        ShowLoginWindow();
    }

    private void ShowLoginWindow()
    {
        LoginViewModel? loginViewModel = null;
        var dynamicUserRepo = new AccessUserRepository(() =>
        {
            if (loginViewModel is not null && loginViewModel.ConnectionSettings.IsAccessSelected && !string.IsNullOrWhiteSpace(loginViewModel.ConnectionSettings.AccessDatabasePath))
            {
                return loginViewModel.ConnectionSettings.AccessDatabasePath;
            }
            return ResolveProjectDatabasePath(_settingsStore);
        }, _passwordHasher!);

        var authService = new LoginService(dynamicUserRepo, _passwordHasher!);
        loginViewModel = new LoginViewModel(authService, _settingsStore!);
        var loginWindow = new LoginWindow
        {
            DataContext = loginViewModel
        };

        loginViewModel.LoginSucceeded += async (_, user) =>
        {
            var connectionSettings = loginViewModel.ConnectionSettings;
            var activeDbPath = connectionSettings.IsAccessSelected && !string.IsNullOrWhiteSpace(connectionSettings.AccessDatabasePath)
                ? connectionSettings.AccessDatabasePath
                : ResolveProjectDatabasePath(_settingsStore);
            DateRangeService.SetDatabasePath(activeDbPath);

            var accountRepo = CreateAccountRepository(connectionSettings);
            var customerRepo = CreateCustomerRepository(connectionSettings);
            var itemRepo = CreateItemRepository(connectionSettings);
            var billRepo = CreateBillRepository(connectionSettings);
            var purchaseRepo = CreatePurchaseRepository(connectionSettings);
            var purityRepo = CreatePurityRepository(connectionSettings);
            var userPermissionRepo = CreateUserPermissionRepository(connectionSettings);
            var purchaseExpenseRepo = CreatePurchaseExpenseRepository(connectionSettings);
            var nonBarcodeRepo = CreateNonBarcodeRepository(connectionSettings);
            var barcodeCreationRepo = CreateBarcodeCreationRepository(connectionSettings);
            var karigharRepo = CreateKarigharRepository(connectionSettings);
            var staffRepo = CreateStaffRepository(connectionSettings);
            var schemeRepo = CreateSchemeCreationRepository(connectionSettings);
            var settingsRepo = CreateSettingsRepository(connectionSettings);
            var voucherRepo = CreateVoucherRepository(connectionSettings);
            var barcodeReportRepo = CreateBarcodeReportRepository(connectionSettings, barcodeCreationRepo);
            var nonBarcodeReportRepo = CreateNonBarcodeReportRepository(connectionSettings);
            var ledgerReportRepo = CreateLedgerReportRepository(connectionSettings);
            var trialBalanceRepo = CreateTrialBalanceRepository(connectionSettings);
            var tradingAccountRepo = CreateTradingAccountRepository(connectionSettings);
            var balanceSheetRepo = CreateBalanceSheetRepository(connectionSettings);
            var schemeDueRepo = CreateSchemeDueRepository(connectionSettings);
            var hireRepo = CreateHireRepository(connectionSettings);
            var auditRepo = CreateAuditRepository(connectionSettings);
            var salesVouTypeRepo = CreateSalesVouTypeRepository(connectionSettings);
            var catalogueRepo = CreateCatalogueRepository(connectionSettings);
            var gstReportRepo = CreateGstReportRepository(connectionSettings);
            var orderRepo = CreateOrderRepository(connectionSettings);
            var boxMasterRepo = CreateBoxMasterRepository(connectionSettings);
            var stockTransferRepo = CreateStockTransferRepository(connectionSettings);
            var whatsAppTrackerRepo = CreateWhatsAppBillTrackerRepository(connectionSettings);
            var couponRepo = CreateCouponRepository(connectionSettings);
            var whatsAppService = new WhatsAppService(settingsRepo, whatsAppTrackerRepo);
            var couponEngineService = new CouponEngineService(couponRepo, whatsAppService);
            var cardAdjustmentReceiptRepo = CreateCardAdjustmentReceiptRepository(connectionSettings);
            var cardSetupRepo = CreateCardSetupRepository(connectionSettings);
            var printerSettingsStore = new LocalPrinterSettingsStore();
            var barcodePrintService = new BarcodePrintService(printerSettingsStore, settingsRepo);
            barcodePrintService.PreviewHandler = async previewData =>
            {
                return await Current.Dispatcher.InvokeAsync(() =>
                {
                    var previewWin = new BarcodeStickerPreviewWindow(previewData);
                    previewWin.SetOwnerSafely();
                    return previewWin.ShowDialog() == true;
                });
            };
            var databaseBackupService = new DatabaseBackupService();
            var aiImageEnhancementService = new AiImageEnhancementService();
            var dashboardRepo = CreateDashboardRepository(connectionSettings, databaseBackupService, _settingsStore!);

            UserPermission? currentUserPermissions = null;
            try
            {
                currentUserPermissions = await userPermissionRepo.GetByUsernameAsync(user.Username);
            }
            catch
            {
                // Fallback to role defaults
            }

            MainViewModel? mainViewModel = null;
            var bankStatementParserService = new BankStatementParserService();
            mainViewModel = new MainViewModel(
                user,
                new Lazy<AccountCreationViewModel>(() => new AccountCreationViewModel(accountRepo, user.Username, auditRepo)),
                new Lazy<CustomerCreationViewModel>(() => new CustomerCreationViewModel(customerRepo, user.Username, auditRepo)),
                new Lazy<ItemCreationViewModel>(() => new ItemCreationViewModel(itemRepo, user.Username)),
                () => new InvoiceViewModel(billRepo, customerRepo, accountRepo, itemRepo, purityRepo, settingsRepo, user.Username, auditRepo, whatsAppService: whatsAppService, whatsAppBillTrackerRepository: whatsAppTrackerRepo, couponEngineService: couponEngineService),
                new Lazy<PurchaseViewModel>(() => new PurchaseViewModel(purchaseRepo, accountRepo, itemRepo, purityRepo, settingsRepo, user.Username, auditRepo)),
                new Lazy<PurchaseExpenseEntryViewModel>(() => new PurchaseExpenseEntryViewModel(purchaseExpenseRepo, accountRepo, itemRepo, user.Username, auditRepo)),
                new Lazy<NonBarcodeStockViewModel>(() => new NonBarcodeStockViewModel(nonBarcodeRepo, accountRepo, itemRepo, user.Username)),
                new Lazy<BarcodeCreationViewModel>(() => new BarcodeCreationViewModel(barcodeCreationRepo, accountRepo, itemRepo, purityRepo, barcodePrintService, settingsRepo, user.Username, auditRepo, aiImageEnhancementService, boxMasterRepo)),
                new Lazy<BarcodeCreationBulkViewModel>(() => new BarcodeCreationBulkViewModel(barcodeCreationRepo, accountRepo, itemRepo, purityRepo, user.Username, auditRepo, boxMasterRepo)),
                new Lazy<KarigharIssueViewModel>(() => new KarigharIssueViewModel(karigharRepo, accountRepo, purityRepo, user.Username)),
                new Lazy<KarigharReceiveViewModel>(() => new KarigharReceiveViewModel(karigharRepo, accountRepo, user.Username)),
                new Lazy<HireIssueViewModel>(() => new HireIssueViewModel(hireRepo, user.Username)),
                new Lazy<HireReceiveViewModel>(() => new HireReceiveViewModel(hireRepo, user.Username)),
                new Lazy<PurityMasterViewModel>(() => new PurityMasterViewModel(purityRepo, user.Username, auditRepo)),
                new Lazy<StaffCreationViewModel>(() => new StaffCreationViewModel(staffRepo, user.Username, auditRepo)),
                new Lazy<SchemeCreationViewModel>(() => new SchemeCreationViewModel(schemeRepo, customerRepo, staffRepo, accountRepo, user.Username, auditRepo)),
                new Lazy<UserPermissionViewModel>(() => new UserPermissionViewModel(userPermissionRepo, user.Username)),
                currentUserPermissions,
                new Lazy<GeneralSettingsViewModel>(() => new GeneralSettingsViewModel(settingsRepo, user.Username, trackerRepository: whatsAppTrackerRepo, databaseSettingsStore: _settingsStore)),
                new Lazy<PrinterSettingsViewModel>(() => new PrinterSettingsViewModel(printerSettingsStore, user.Username, settingsRepo)),
                new Lazy<BackupViewModel>(() => new BackupViewModel(databaseBackupService, _settingsStore!, () => ResolveProjectDatabasePath(_settingsStore), user.Username)),
                new Lazy<VoucherEntryViewModel>(() => new VoucherEntryViewModel(voucherRepo, accountRepo, VoucherType.Payments, user.Username, auditRepo)),
                new Lazy<VoucherEntryViewModel>(() => new VoucherEntryViewModel(voucherRepo, accountRepo, VoucherType.Receipts, user.Username, auditRepo)),
                new Lazy<VoucherEntryViewModel>(() => new VoucherEntryViewModel(voucherRepo, accountRepo, VoucherType.Journal, user.Username, auditRepo)),
                new Lazy<SchemeReceiptViewModel>(() => new SchemeReceiptViewModel(voucherRepo, schemeRepo, accountRepo, customerRepo, user.Username, auditRepo)),
                new Lazy<SchemeDueListViewModel>(() => new SchemeDueListViewModel(schemeDueRepo, user.Username)),
                new Lazy<BarcodeStockReportViewModel>(() => new BarcodeStockReportViewModel(barcodeReportRepo, settingsRepo, barcodeCreationRepo, barcodePrintService, boxMasterRepo, stockTransferRepo, billRepo)),
                new Lazy<NonBarcodeStockReportViewModel>(() => new NonBarcodeStockReportViewModel(nonBarcodeReportRepo)),
                new Lazy<NonBarcodeItemStockReportViewModel>(() => new NonBarcodeItemStockReportViewModel(nonBarcodeReportRepo)),
                new Lazy<LedgerReportViewModel>(() => new LedgerReportViewModel(ledgerReportRepo)),
                new Lazy<LedgerGroupViewModel>(() => new LedgerGroupViewModel(ledgerReportRepo, user.Username)),
                new Lazy<TrialBalanceViewModel>(() => new TrialBalanceViewModel(trialBalanceRepo)),
                new Lazy<TradingAccountViewModel>(() => new TradingAccountViewModel(tradingAccountRepo, user.Username)),
                new Lazy<BalanceSheetViewModel>(() => new BalanceSheetViewModel(balanceSheetRepo, user.Username)),
                new Lazy<AuditReportViewModel>(() => new AuditReportViewModel(auditRepo, user.Username)),
                new Lazy<SalesVouTypeMasterViewModel>(() => new SalesVouTypeMasterViewModel(salesVouTypeRepo, accountRepo, itemRepo, purityRepo, user.Username)),
                new Lazy<PurchaseRegisterViewModel>(() => new PurchaseRegisterViewModel(purchaseRepo, user.Username)),
                new Lazy<CatalogueViewModel>(() => new CatalogueViewModel(catalogueRepo, itemRepo, settingsRepo, user.Username)),
                new Lazy<GSTReportsViewModel>(() => new GSTReportsViewModel(gstReportRepo, user.Username, settingsRepo)),
                new Lazy<OrderRequirementViewModel>(() => new OrderRequirementViewModel(orderRepo, customerRepo, itemRepo, purityRepo, settingsRepo, user.Username, auditRepo)),
                dashboardViewModel: new Lazy<DashboardViewModel>(() => new DashboardViewModel(
                    dashboardRepo,
                    user.Username,
                    AppRoles.ToName(user.Role),
                    databaseBackupService,
                    _settingsStore!,
                    () => ResolveProjectDatabasePath(_settingsStore),
                    orderRepo,
                    customerRepo,
                    itemRepo,
                    purityRepo,
                    settingsRepo,
                    auditRepo)),
                boxMasterViewModel: new Lazy<BoxMasterViewModel>(() => new BoxMasterViewModel(boxMasterRepo, user.Username)),
                bankReconciliationViewModel: new Lazy<BankReconciliationViewModel>(() => new BankReconciliationViewModel(
                    accountRepo,
                    ledgerReportRepo,
                    bankStatementParserService,
                    (module, param) => mainViewModel?.SelectModule(module, param))),
                cardAdjustmentReceiptViewModel: new Lazy<CardAdjustmentReceiptViewModel>(() => new CardAdjustmentReceiptViewModel(
                    cardAdjustmentReceiptRepo,
                    accountRepo,
                    user.Username,
                    () => new CardSetupViewModel(cardSetupRepo, accountRepo))),
                stockTransferViewModel: new Lazy<StockTransferViewModel>(() => new StockTransferViewModel(stockTransferRepo, user.Username)),
                couponEngineViewModel: new Lazy<CouponEngineViewModel>(() => new CouponEngineViewModel(couponRepo, couponEngineService, user.Username)),
                softwareUpdateViewModel: new Lazy<SoftwareUpdateViewModel>(() => new SoftwareUpdateViewModel()),
                accountRepository: accountRepo,
                customerRepository: customerRepo,
                itemRepository: itemRepo,
                purityRepository: purityRepo,
                auditRepository: auditRepo);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainViewModel.LogoutRequested += (_, _) =>
            {
                ShowLoginWindow();
                mainWindow.Close();
            };

            MainWindow = mainWindow;
            mainWindow.Show();
            loginWindow.Close();
        };

        MainWindow = loginWindow;
        loginWindow.Show();
        DumpDiagnostics(loginWindow);
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var focused = Keyboard.FocusedElement;
        if (focused is null)
        {
            return;
        }

        var focusedDo = focused as DependencyObject;

        // Let BarcodeSaveConfirmationDialog handle Enter and navigation keys itself
        if (focusedDo is not null && (focusedDo is Views.BarcodeSaveConfirmationDialog || FindParent<Views.BarcodeSaveConfirmationDialog>(focusedDo) is not null))
        {
            return;
        }

        // Let Barcode Stock Check and Stock Transfer scan boxes handle Enter themselves (barcode scanning)
        if (focusedDo is not null && IsInsideBarcodeScanBox(focusedDo))
        {
            return;
        }

        // ComboBox & RadComboBox arrow key navigation on filtered items
        if (focusedDo is not null && (e.Key is Key.Down or Key.Up))
        {
            if (Helpers.ComboBoxNavigationHelper.HandleKeyDown(focusedDo, e))
            {
                return;
            }
        }

        if (e.Key != Key.Enter || (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt)) != 0)
        {
            return;
        }

        // If focused inside RadNumericUpDown:
        // Advance focus past the numeric control to the next logical control, skipping spinner buttons
        if (focusedDo is not null)
        {
            var numericControl = FindParent<Telerik.Windows.Controls.RadNumericUpDown>(focusedDo)
                                 ?? (focusedDo as Telerik.Windows.Controls.RadNumericUpDown);
            if (numericControl != null)
            {
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement? current = (focused as UIElement) ?? (numericControl as UIElement);
                for (int i = 0; i < 5; i++)
                {
                    if (current == null) break;
                    current.MoveFocus(request);
                    var newFocused = Keyboard.FocusedElement as DependencyObject;
                    if (newFocused != null && !IsChildOf(newFocused, numericControl))
                    {
                        e.Handled = true;
                        return;
                    }
                    current = newFocused as UIElement;
                }
                e.Handled = true;
                return;
            }
        }

        // If focused inside RadDatePicker, RadDateTimePicker, or DatePicker:
        // Advance focus past the picker control to the next logical control, skipping internal calendar buttons
        if (focusedDo is not null)
        {
            var dateControl = FindParent<Telerik.Windows.Controls.RadDatePicker>(focusedDo)
                              ?? (focusedDo as Telerik.Windows.Controls.RadDatePicker)
                              ?? (FrameworkElement?)FindParent<Telerik.Windows.Controls.RadDateTimePicker>(focusedDo)
                              ?? (focusedDo as Telerik.Windows.Controls.RadDateTimePicker)
                              ?? (FrameworkElement?)FindParent<System.Windows.Controls.DatePicker>(focusedDo)
                              ?? (focusedDo as System.Windows.Controls.DatePicker);
            if (dateControl != null)
            {
                if (dateControl is Telerik.Windows.Controls.RadDatePicker rdp)
                {
                    rdp.IsDropDownOpen = false;
                }
                else if (dateControl is Telerik.Windows.Controls.RadDateTimePicker rdtp)
                {
                    rdtp.IsDropDownOpen = false;
                }
                else if (dateControl is System.Windows.Controls.DatePicker dp)
                {
                    dp.IsDropDownOpen = false;
                }

                if (string.Equals(dateControl.Name, "InvDateDatePicker", StringComparison.OrdinalIgnoreCase))
                {
                    var invoiceView = FindParent<Views.InvoiceView>(dateControl);
                    if (invoiceView != null)
                    {
                        invoiceView.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                        {
                            invoiceView.PhoneTextBox?.Focus();
                        }));
                        e.Handled = true;
                        return;
                    }
                }

                if (string.Equals(dateControl.Name, "DueDateDatePicker", StringComparison.OrdinalIgnoreCase))
                {
                    var invoiceView = FindParent<Views.InvoiceView>(dateControl);
                    if (invoiceView != null)
                    {
                        invoiceView.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(invoiceView.FocusFirstBillItemCell));
                        e.Handled = true;
                        return;
                    }
                }

                // Advance focus past the date picker control
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement? current = (focused as UIElement) ?? (dateControl as UIElement);
                for (int i = 0; i < 5; i++)
                {
                    if (current == null) break;
                    current.MoveFocus(request);
                    var newFocused = Keyboard.FocusedElement as DependencyObject;
                    if (newFocused != null && !IsChildOf(newFocused, dateControl))
                    {
                        e.Handled = true;
                        return;
                    }
                    current = newFocused as UIElement;
                }

                e.Handled = true;
                return;
            }
        }

        // If focused inside a multi-line TextBox with AcceptsReturn enabled, let it insert a newline
        if (focusedDo is TextBoxBase tb && tb.AcceptsReturn)
        {
            return;
        }

        // Let buttons handle Enter themselves (click)
        if (focusedDo is not null && IsButtonOrButtonContent(focusedDo))
        {
            return;
        }

        // Let menu items handle Enter themselves (acts like a click / activate)
        if (focusedDo is not null && IsMenuItemOrMenuContent(focusedDo))
        {
            return;
        }

        // If focused inside StickerFormatComboBox, route focus directly to Save button in BarcodeCreationView
        if (focusedDo is not null && IsInsideStickerFormat(focusedDo))
        {
            if (FindParent<Telerik.Windows.Controls.RadComboBox>(focusedDo) is { } combo)
            {
                combo.IsDropDownOpen = false;
            }

            var view = FindParent<Views.BarcodeCreationView>(focusedDo);
            if (view is not null)
            {
                view.FocusSaveButton();
                e.Handled = true;
                return;
            }
        }

        // If focused inside NarrationTextBox in VoucherEntryView, route focus directly to Save button
        if (focusedDo is not null && IsInsideVoucherNarration(focusedDo))
        {
            var view = FindParent<Views.VoucherEntryView>(focusedDo);
            if (view is not null)
            {
                view.FocusSaveButton();
                e.Handled = true;
                return;
            }
        }

        // Let DataGrids and RadGridViews handle Enter key themselves for grid/cell traversal
        // (Must precede ComboBoxNavigationHelper so in-grid ComboBoxes are not bounced out)
        if (focusedDo is not null && IsInsideGrid(focusedDo))
        {
            return;
        }

        // ComboBox & RadComboBox Enter key selection & focus advance
        if (focusedDo is not null && Helpers.ComboBoxNavigationHelper.HandleKeyDown(focusedDo, e))
        {
            return;
        }

        // Let open non-combobox dropdowns, popups, and dropdown items handle Enter themselves
        if (focusedDo is not null && IsDropDownOrPopupOpen(focusedDo))
        {
            return;
        }

        // Call MoveFocus directly on the focused element — NOT on a parent found by walking up.
        // Calling MoveFocus on a container whose focus is already inside it causes WPF to
        // re-enter that container rather than advance past it (backwards-navigation bug).
        if (focused is UIElement focusedUiElement)
        {
            var request = new TraversalRequest(FocusNavigationDirection.Next);
            if (focusedUiElement.MoveFocus(request))
            {
                e.Handled = true;
            }
        }
    }

    private static bool IsInsideGrid(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is Telerik.Windows.Controls.RadGridView or
                System.Windows.Controls.DataGrid or
                Telerik.Windows.Controls.GridView.GridViewRow or
                Telerik.Windows.Controls.GridView.GridViewCell)
            {
                return true;
            }

            current = GetParentDependencyObject(current);
        }

        return false;
    }

    private static bool IsMenuItemOrMenuContent(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is System.Windows.Controls.MenuItem or
                Telerik.Windows.Controls.RadMenuItem or
                System.Windows.Controls.Primitives.MenuBase or
                Telerik.Windows.Controls.RadMenu or
                System.Windows.Controls.ContextMenu or
                Telerik.Windows.Controls.RadContextMenu)
            {
                return true;
            }

            current = GetParentDependencyObject(current);
        }

        return false;
    }

    private static bool IsDropDownOrPopupOpen(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is Telerik.Windows.Controls.RadComboBox comboBox && comboBox.IsDropDownOpen)
            {
                return true;
            }

            if (current is System.Windows.Controls.ComboBox stdComboBox && stdComboBox.IsDropDownOpen)
            {
                return true;
            }

            if (current is Telerik.Windows.Controls.RadDateTimePicker dateTimePicker && dateTimePicker.IsDropDownOpen)
            {
                return true;
            }

            if (current is System.Windows.Controls.DatePicker datePicker && datePicker.IsDropDownOpen)
            {
                return true;
            }

            if (current is Telerik.Windows.Controls.RadComboBoxItem or System.Windows.Controls.ComboBoxItem)
            {
                return true;
            }

            if (current is System.Windows.Controls.Primitives.Popup popup && popup.IsOpen)
            {
                return true;
            }

            current = GetParentDependencyObject(current);
        }

        return false;
    }

    private static DependencyObject? GetParentDependencyObject(DependencyObject? current)
    {
        if (current is null) return null;

        DependencyObject? parent = null;
        if (current is System.Windows.Media.Visual)
        {
            parent = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        if (parent is null)
        {
            parent = System.Windows.LogicalTreeHelper.GetParent(current);
        }

        if (parent is null && current is FrameworkElement fe)
        {
            parent = fe.Parent ?? fe.TemplatedParent;
        }

        if (parent is null && current is System.Windows.Controls.Primitives.Popup popup)
        {
            parent = popup.PlacementTarget ?? popup.Parent;
        }

        return parent;
    }




    private static bool IsInsideStickerFormat(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement fe)
            {
                if (fe.Name == "StickerFormatComboBox" || Equals(fe.Tag, "EnterToSave"))
                {
                    return true;
                }
            }

            current = current is System.Windows.Media.Visual
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : System.Windows.LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private static bool IsInsideVoucherNarration(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement fe)
            {
                if (fe.Name == "NarrationTextBox" && FindParent<Views.VoucherEntryView>(fe) is not null)
                {
                    return true;
                }
            }

            current = current is System.Windows.Media.Visual
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : System.Windows.LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private static bool IsInsideBarcodeScanBox(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement fe)
            {
                if (fe.Name is "StockCheckScanTextBox" or "ScanTextBox")
                {
                    return true;
                }
            }

            if (current is Views.BarcodeStockReportView reportView &&
                reportView.DataContext is ViewModels.BarcodeStockReportViewModel { IsStockCheckActive: true })
            {
                return true;
            }

            current = current is System.Windows.Media.Visual
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : System.Windows.LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private static T? FindParent<T>(DependencyObject element) where T : DependencyObject
    {
        var current = element;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is System.Windows.Media.Visual
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : System.Windows.LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsChildOf(DependencyObject child, DependencyObject parent)
    {
        var current = child;
        while (current is not null)
        {
            if (current == parent) return true;
            current = current is System.Windows.Media.Visual
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : System.Windows.LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private static bool IsButtonOrButtonContent(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is ButtonBase)
            {
                return true;
            }

            current = current is System.Windows.Media.Visual
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : System.Windows.LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private static void OnTextBoxBaseGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is DependencyObject d && IsInsideGrid(d))
        {
            return;
        }

        if (sender is TextBoxBase textBox && textBox.IsKeyboardFocused)
        {
            textBox.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (textBox.IsKeyboardFocused)
                {
                    textBox.SelectAll();
                }
            }));
        }
    }

    private static void OnTextBoxBasePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject d && IsInsideGrid(d))
        {
            return;
        }

        if (sender is TextBoxBase textBox && !textBox.IsKeyboardFocusWithin)
        {
            textBox.Focus();
        }
    }

    private static void OnRadNumericUpDownGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is DependencyObject d && IsInsideGrid(d))
        {
            return;
        }

        if (sender is Telerik.Windows.Controls.RadNumericUpDown nud && nud.IsKeyboardFocusWithin)
        {
            nud.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (nud.IsKeyboardFocusWithin)
                {
                    nud.SelectAll();
                }
            }));
        }
    }

    private static void OnRadNumericUpDownPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadNumericUpDown nud)
        {
            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            var parent = (nud.Parent ?? System.Windows.Media.VisualTreeHelper.GetParent(nud)) as UIElement;
            parent?.RaiseEvent(eventArg);
        }
    }

    private static void OnRadNumericUpDownLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadNumericUpDown nud)
        {
            foreach (var btn in FindVisualChildren<System.Windows.Controls.Primitives.RepeatButton>(nud))
            {
                btn.Focusable = false;
                btn.IsTabStop = false;
                KeyboardNavigation.SetIsTabStop(btn, false);
            }
        }
    }

    private static void OnRadComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Telerik.Windows.Controls.RadComboBox combo)
        {
            ApplyComboBoxDropDownChevron(combo);
        }
    }

    public static void ApplyComboBoxDropDownChevron(Telerik.Windows.Controls.RadComboBox combo)
    {
        if (!combo.IsEditable)
        {
            return;
        }

        combo.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            try
            {
                var dropDownButton = combo.Template?.FindName("PART_DropDownButton", combo) as ToggleButton
                                     ?? FindVisualChildren<ToggleButton>(combo).FirstOrDefault();
                if (dropDownButton != null && System.Windows.Application.Current.TryFindResource("ComboBoxDropDownToggleButtonStyle") is Style style)
                {
                    dropDownButton.Style = style;
                    var binding = new System.Windows.Data.Binding(nameof(Telerik.Windows.Controls.RadComboBox.IsDropDownOpen))
                    {
                        Source = combo,
                        Mode = System.Windows.Data.BindingMode.TwoWay
                    };
                    dropDownButton.SetBinding(ToggleButton.IsCheckedProperty, binding);
                }
            }
            catch
            {
                // Graceful fallback
            }
        }));
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
            if (child is T match)
            {
                yield return match;
            }
            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    private static void DumpDiagnostics(LoginWindow loginWindow)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== merged dictionaries ===");
            foreach (var d in System.Windows.Application.Current.Resources.MergedDictionaries)
            {
                sb.AppendLine(d.Source?.OriginalString ?? "(no source)");
            }

            loginWindow.UpdateLayout();
            sb.AppendLine("=== resource resolution ===");
            DumpResolvedType(typeof(Telerik.Windows.Controls.RadButton), sb);
            DumpResolvedType(typeof(Telerik.Windows.Controls.RadComboBox), sb);
            DumpResolvedType(typeof(Telerik.Windows.Controls.RadWatermarkTextBox), sb);
            DumpResolvedType(typeof(Telerik.Windows.Controls.RadPasswordBox), sb);
            DumpResolvedType(typeof(Telerik.Windows.Controls.RadNumericUpDown), sb);
            DumpResolvedType(typeof(Telerik.Windows.Controls.RadBusyIndicator), sb);
            DumpNamedKey("RadButtonStyle", sb);
            DumpNamedKey("RadComboBoxStyle", sb);
            DumpNamedKey("RadWatermarkTextBoxStyle", sb);
            DumpNamedKey("RadPasswordBoxStyle", sb);
            DumpNamedKey("RadNumericUpDownStyle", sb);
            sb.AppendLine("=== visual tree ===");
            Walk(loginWindow, "", sb);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "theme_diag.txt"), sb.ToString());
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "theme_diag.txt"), "EX: " + ex);
        }
    }

    private static void DumpNamedKey(string key, StringBuilder sb)
    {
        try
        {
            var style = System.Windows.Application.Current.FindResource(key) as System.Windows.Style;
            var hasTemplate = style != null && style.Setters.OfType<System.Windows.Setter>().Any(s => s.Property == System.Windows.Controls.Control.TemplateProperty);
            sb.AppendLine($"NamedKey({key}) => {(style == null ? "MISSING" : style.TargetType?.Name ?? "?")} templateSetter={hasTemplate} base={style?.BasedOn?.TargetType?.Name ?? "null"}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"NamedKey({key}) EX: {ex.Message}");
        }
    }

    private static void DumpResolvedType(Type type, StringBuilder sb)
    {
        try
        {
            var style = System.Windows.Application.Current.FindResource(type) as System.Windows.Style;
            var chain = new StringBuilder();
            var cur = style;
            var depth = 0;
            while (cur != null && depth < 6)
            {
                var hasTemplate = cur.Setters.OfType<System.Windows.Setter>().Any(s => s.Property == System.Windows.Controls.Control.TemplateProperty);
                chain.Append($" [{depth}]T={cur.TargetType?.Name ?? "?"},templateSetter={hasTemplate}");
                cur = cur.BasedOn;
                depth++;
            }
            sb.AppendLine($"FindResource({type.Name}) => {chain}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"FindResource({type.Name}) EX: {ex.Message}");
        }
    }

    private static void Walk(DependencyObject o, string indent, StringBuilder sb)
    {
        try
        {
            var typeName = o.GetType().Name;
            var name = (o as FrameworkElement)?.Name ?? string.Empty;
            sb.AppendLine($"{indent}{typeName}{(string.IsNullOrEmpty(name) ? "" : " [" + name + "]")}");

            if (o is System.Windows.Controls.Control c)
            {
                var styleTarget = c.Style?.TargetType?.Name ?? "NULL";
                var baseTarget = c.Style?.BasedOn?.TargetType?.Name ?? "null";
                var hasTemplate = c.Template != null;
                sb.AppendLine($"{indent}  Style({styleTarget}, base={baseTarget}) Template={hasTemplate}");
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                Walk(VisualTreeHelper.GetChild(o, i), indent + "  ", sb);
            }
        }
        catch
        {
        }
    }

    private static IAccountRepository CreateAccountRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoAccountRepository()
            : new AccessAccountRepository(databasePath);
    }

    private static ICustomerRepository CreateCustomerRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoCustomerRepository()
            : new AccessCustomerRepository(databasePath);
    }

    private static IBillRepository CreateBillRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoBillRepository()
            : new AccessBillRepository(databasePath);
    }

    private static IPurchaseRepository CreatePurchaseRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoPurchaseRepository()
            : new AccessPurchaseRepository(databasePath);
    }

    private static IItemRepository CreateItemRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoItemRepository()
            : new AccessItemRepository(databasePath);
    }

    private static IPurityRepository CreatePurityRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoPurityRepository()
            : new AccessPurityRepository(databasePath);
    }

    private static IUserPermissionRepository CreateUserPermissionRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoUserPermissionRepository()
            : new AccessUserPermissionRepository(databasePath);
    }

    private static IPurchaseExpenseRepository CreatePurchaseExpenseRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoPurchaseExpenseRepository()
            : new AccessPurchaseExpenseRepository(databasePath);
    }

    private static INonBarcodeRepository CreateNonBarcodeRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoNonBarcodeRepository()
            : new AccessNonBarcodeRepository(databasePath);
    }

    private static IBarcodeCreationRepository CreateBarcodeCreationRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoBarcodeCreationRepository()
            : new AccessBarcodeCreationRepository(databasePath);
    }

    private static IKarigharRepository CreateKarigharRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoKarigharRepository()
            : new AccessKarigharRepository(databasePath);
    }

    private static IStaffRepository CreateStaffRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
            ConceptJewelleryPro.Infrastructure.Accounts.AccessStaffCommissionLedgerSchemaMigrator.EnsureSchemaAsync(connectionString).GetAwaiter().GetResult();
        }

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoStaffRepository()
            : new AccessStaffRepository(databasePath);
    }

    private static ISchemeCreationRepository CreateSchemeCreationRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
            ConceptJewelleryPro.Infrastructure.Accounts.AccessSchemeCreationSchemaMigrator.EnsureSchemaSync(connectionString);
        }

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoSchemeCreationRepository()
            : new AccessSchemeCreationRepository(databasePath);
    }

    private static ISettingsRepository CreateSettingsRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoSettingsRepository()
            : new AccessSettingsRepository(databasePath);
    }

    private static IVoucherRepository CreateVoucherRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoVoucherRepository()
            : new AccessVoucherRepository(databasePath);
    }

    private static IBarcodeReportRepository CreateBarcodeReportRepository(ConnectionSettingsViewModel settings, IBarcodeCreationRepository? barcodeCreationRepo = null)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoBarcodeReportRepository(barcodeCreationRepo)
            : new AccessBarcodeReportRepository(databasePath);
    }

    private static INonBarcodeReportRepository CreateNonBarcodeReportRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoNonBarcodeReportRepository()
            : new AccessNonBarcodeReportRepository(databasePath);
    }

    private static ILedgerReportRepository CreateLedgerReportRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
            ConceptJewelleryPro.Infrastructure.Accounts.AccessStaffCommissionLedgerSchemaMigrator.EnsureSchemaAsync(connectionString).GetAwaiter().GetResult();
        }

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoLedgerReportRepository()
            : new AccessLedgerReportRepository(databasePath);
    }

    private static ITrialBalanceRepository CreateTrialBalanceRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoTrialBalanceRepository()
            : new AccessTrialBalanceRepository(databasePath);
    }

    private static ITradingAccountRepository CreateTradingAccountRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoTradingAccountRepository()
            : new AccessTradingAccountRepository(databasePath);
    }

    private static IBalanceSheetRepository CreateBalanceSheetRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoBalanceSheetRepository()
            : new AccessBalanceSheetRepository(databasePath);
    }

    private static ISchemeDueRepository CreateSchemeDueRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
            ConceptJewelleryPro.Infrastructure.Accounts.AccessSchemeCreationSchemaMigrator.EnsureSchemaSync(connectionString);
        }

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoSchemeDueRepository()
            : new AccessSchemeDueRepository(databasePath);
    }

    private static IHireRepository CreateHireRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoHireRepository()
            : new AccessHireRepository(databasePath);
    }

    private static IAuditRepository CreateAuditRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoAuditRepository()
            : new AccessAuditRepository(databasePath);
    }

    private static ISalesVouTypeRepository CreateSalesVouTypeRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoSalesVouTypeRepository()
            : new AccessSalesVouTypeRepository(databasePath);
    }

    private static ICatalogueRepository CreateCatalogueRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoCatalogueRepository()
            : new AccessCatalogueRepository(databasePath);
    }

    private static IGstReportRepository CreateGstReportRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoGstReportRepository()
            : new AccessGstReportRepository(databasePath);
    }

    private static IOrderRepository CreateOrderRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};";
            ConceptJewelleryPro.Infrastructure.Accounts.AccessOrderSchemaMigrator.EnsureSchemaSync(connectionString);
        }

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoOrderRepository()
            : new AccessOrderRepository(databasePath);
    }

    private static IBoxMasterRepository CreateBoxMasterRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoBoxMasterRepository()
            : new AccessBoxMasterRepository(databasePath);
    }

    private static IWhatsAppBillTrackerRepository CreateWhatsAppBillTrackerRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoWhatsAppBillTrackerRepository()
            : new AccessWhatsAppBillTrackerRepository(databasePath);
    }

    private static ICouponRepository CreateCouponRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new ConceptJewelleryPro.Infrastructure.Coupons.DemoCouponRepository()
            : new ConceptJewelleryPro.Infrastructure.Coupons.AccessCouponRepository(databasePath);
    }

    private static IStockTransferRepository CreateStockTransferRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoStockTransferRepository()
            : new AccessStockTransferRepository(databasePath);
    }

    private static IDashboardRepository CreateDashboardRepository(
        ConnectionSettingsViewModel settings,
        IDatabaseBackupService backupService,
        DatabaseSettingsStore settingsStore)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath(settingsStore);

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoDashboardRepository()
            : new AccessDashboardRepository(() => databasePath, backupService, settingsStore);
    }

    private static ICardAdjustmentReceiptRepository CreateCardAdjustmentReceiptRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoCardAdjustmentReceiptRepository()
            : new AccessCardAdjustmentReceiptRepository(databasePath);
    }

    private static ICardSetupRepository CreateCardSetupRepository(ConnectionSettingsViewModel settings)
    {
        var databasePath = settings.IsAccessSelected && !string.IsNullOrWhiteSpace(settings.AccessDatabasePath)
            ? settings.AccessDatabasePath
            : ResolveProjectDatabasePath();

        return string.IsNullOrWhiteSpace(databasePath)
            ? new DemoCardSetupRepository()
            : new AccessCardSetupRepository(databasePath);
    }

    private static string? ResolveProjectDatabasePath(DatabaseSettingsStore? settingsStore = null)
    {
        if (settingsStore is not null)
        {
            try
            {
                var saved = settingsStore.Load();
                if (!string.IsNullOrWhiteSpace(saved.AccessDatabasePath) && File.Exists(saved.AccessDatabasePath))
                {
                    return saved.AccessDatabasePath;
                }
            }
            catch
            {
            }
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "RGRG2526.mdb");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}