using System.Collections.ObjectModel;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Services;
using ConceptJewelleryPro.WPF.Views;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class InvoiceViewModel : ViewModelBase, IRefreshable
{
    public event EventHandler? RequestFocusInvoiceType;
    public event EventHandler? RequestSaveLayout;
    public event EventHandler? RequestRemoveGridLine;

    private readonly List<SalesVouType> _salesVouTypes = new();
    private readonly List<Item> _allItems = new();
    private readonly List<PurityMaster> _allPurities = new();
    private readonly RangeObservableCollection<string> _availableItemNames = new();
    private readonly RangeObservableCollection<string> _availableStockBarcodes = new();
    private readonly List<TempStockItem> _allTempStockItems = new();
    private readonly RangeObservableCollection<TempStockItem> _availableStockItems = new();
    private readonly Dictionary<string, (string HeadGroup, string Group)> _itemGroupMap = new(StringComparer.OrdinalIgnoreCase);

    private string _vouType = string.Empty;
    private string _vouNo = string.Empty;
    private DateTime _billDate = DateTime.Today;
    private DateTime _dueDate = DateTime.Today;
    private string _phone = string.Empty;
    private string _customerName = string.Empty;
    private string _customerCode = string.Empty;
    private string _address = string.Empty;
    private string _address2 = string.Empty;
    private string _city = "CUTTACK";
    private string _pincode = string.Empty;
    private string _email = string.Empty;
    private string _gstinPan = string.Empty;
    private bool _check;
    private bool _sms;
    private string _remarks = string.Empty;

    private int _q1 = 4; // DESIGNS (default GOOD = 4)
    private int _q2 = 4; // PRICING (default GOOD = 4)
    private int _q3 = 4; // STAFF BEHAVIOUR (default GOOD = 4)

    private decimal _grossAmt;
    private decimal _discount;
    private decimal _additionalBillDiscount;
    private decimal _netAmt;
    private decimal _totalPaid;
    private decimal _balanceAmt;
    private decimal _allowNtWtModUpto;

    private bool _isBusy;
    private bool _isEditing;
    private bool _isLoadingBill;
    private bool _isExistingInvoice;
    private bool _isOpeningPaymentDialog;
    private bool _isOpeningBillSettingsDialog;
    private bool _isOpeningImageAIBillDialog;
    private CancellationTokenSource? _customerLookupCts;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public event EventHandler? SessionTitleChanged;

    public bool IsExistingInvoice => _isExistingInvoice;

    public string SessionTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(VouNo))
            {
                if (!string.IsNullOrWhiteSpace(CustomerName))
                    return $"Invoice #{VouNo} - {CustomerName}";
                return $"Invoice #{VouNo}";
            }
            if (!string.IsNullOrWhiteSpace(CustomerName))
                return $"Invoice - {CustomerName}";
            return "Invoice";
        }
    }

    private void RaiseSessionTitleChanged()
    {
        OnPropertyChanged(nameof(SessionTitle));
        SessionTitleChanged?.Invoke(this, EventArgs.Empty);
    }

    private Bill? _selectedBill;
    private readonly ObservableCollection<string> _paymentAccountNames = new();
    private readonly List<Account> _allAccounts = new();
    private readonly List<string> _advanceOptions = new();
    private readonly List<string> _allItemTypes = new();

    private static readonly string[] AllowedHeadCategories = { "cash", "cash in hand", "bank a/c", "bank ac", "bank", "credit card", "card", "advances & deposits", "advances", "deposits", "debtors", "sundry debtors", "debtor", "purchases", "purchase", "purchase a/c", "purchase ac", "old gold", "old metal" };

    private readonly IBillRepository _billRepository;
    private readonly ICustomerRepository? _customerRepository;
    private readonly IAccountRepository? _accountRepository;
    private readonly IItemRepository? _itemRepository;
    private readonly IPurityRepository? _purityRepository;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IEInvoiceService _eInvoiceService;
    private readonly IWhatsAppBillTrackerRepository? _whatsAppBillTrackerRepository;
    private readonly ICouponEngineService? _couponEngineService;
    private readonly string _currentUser;
    private bool _waAutoSend;

    public InvoiceViewModel(
        IBillRepository billRepository,
        ICustomerRepository? customerRepository = null,
        IAccountRepository? accountRepository = null,
        IItemRepository? itemRepository = null,
        IPurityRepository? purityRepository = null,
        ISettingsRepository? settingsRepository = null,
        string currentUser = "admin",
        IAuditRepository? auditRepository = null,
        IWhatsAppService? whatsAppService = null,
        IEInvoiceService? eInvoiceService = null,
        IWhatsAppBillTrackerRepository? whatsAppBillTrackerRepository = null,
        ICouponEngineService? couponEngineService = null)
    {
        _billRepository = billRepository;
        _customerRepository = customerRepository;
        _accountRepository = accountRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _settingsRepository = settingsRepository;
        _auditRepository = auditRepository;
        _whatsAppBillTrackerRepository = whatsAppBillTrackerRepository;
        _couponEngineService = couponEngineService;
        _whatsAppService = whatsAppService ?? new WhatsAppService(settingsRepository, whatsAppBillTrackerRepository);
        _eInvoiceService = eInvoiceService ?? new EInvoiceService(settingsRepository);
        _currentUser = currentUser;

        Items = new RangeObservableCollection<BillSubformItemViewModel>();
        Payments = new RangeObservableCollection<PaymentSubformItemViewModel>();
        Bills = new RangeObservableCollection<Bill>();
        InvoiceTypes = new ObservableCollection<string>();
        Cities = new List<string> { "CUTTACK", "BHUBANESWAR", "PURI", "ROURKELA", "BERHAMPUR" };

        AddNewCommand = new AsyncRelayCommand(AddNewAsync);
        ModifyCommand = new RelayCommand(Modify);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        CancelCommand = new RelayCommand(Cancel);
        PrintCommand = new RelayCommand(Print);
        WhatsAppCommand = new AsyncRelayCommand(async () => await SendWhatsAppAsync(isManual: true));
        GenerateEInvoiceCommand = new AsyncRelayCommand(GenerateEInvoiceAsync);

        AddItemRowCommand = new RelayCommand(AddItemRow);
        RemoveItemRowCommand = new RelayCommand(parameter => RemoveItemRow(parameter as BillSubformItemViewModel));
        AddPaymentRowCommand = new RelayCommand(AddPaymentRow);
        RemovePaymentRowCommand = new RelayCommand(parameter => RemovePaymentRow(parameter as PaymentSubformItemViewModel));
        CreateDebtorPaymentCommand = new AsyncRelayCommand(parameter => CreateDebtorPaymentAsync(parameter as PaymentSubformItemViewModel), _ => IsEditing);
        RemoveSelectedGridLineCommand = new RelayCommand(RemoveSelectedGridLine);
        OpenPaymentDetailsCommand = new RelayCommand(parameter => OpenPaymentDetails(parameter as PaymentSubformItemViewModel));

        SaveLayoutCommand = new RelayCommand(SaveLayout);
        BillSettingsCommand = new RelayCommand(BillSettings);
        ImageAIBillCommand = new RelayCommand(ImageAIBill);
        OpenListCommand = new AsyncRelayCommand(OpenListAsync);
        SyncWhatsAppStatusCommand = new AsyncRelayCommand(SyncWhatsAppStatusForCurrentBillAsync);
        RefreshLookupsCommand = new AsyncRelayCommand(RefreshLookupsAsync);
        ApplyCouponCommand = new AsyncRelayCommand(ApplyCouponAsync);
        RemoveCouponCommand = new RelayCommand(RemoveCoupon);

        Items.CollectionChanged += OnItemsCollectionChanged;
        Payments.CollectionChanged += (_, _) => RecalculateTotals();

        EnsureDefaultRows();

        _ = LoadInitialDataAsync();
    }

    private void OnItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (BillSubformItemViewModel item in e.NewItems)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                item.PropertyChanged += Item_PropertyChanged;
                item.ValuesChanged -= Item_ValuesChanged;
                item.ValuesChanged += Item_ValuesChanged;
            }
        }
        if (e.OldItems is not null)
        {
            foreach (BillSubformItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                item.ValuesChanged -= Item_ValuesChanged;
            }
        }
        RecalculateTotals();
    }

    private void Item_ValuesChanged(object? sender, EventArgs e)
    {
        RecalculateTotals();
    }

    public RangeObservableCollection<BillSubformItemViewModel> Items { get; }

    public RangeObservableCollection<PaymentSubformItemViewModel> Payments { get; }

    public RangeObservableCollection<Bill> Bills { get; }

    public ObservableCollection<string> InvoiceTypes { get; }

    public IReadOnlyList<string> Cities { get; }

    public IReadOnlyList<string> PaymentAccountNames => _paymentAccountNames;

    public ICommand AddNewCommand { get; }

    public ICommand ModifyCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand PrintCommand { get; }

    public ICommand WhatsAppCommand { get; }

    public ICommand GenerateEInvoiceCommand { get; }

    public ICommand OpenListCommand { get; }

    public ICommand AddItemRowCommand { get; }

    public ICommand RemoveItemRowCommand { get; }

    public ICommand AddPaymentRowCommand { get; }
    public ICommand RemovePaymentRowCommand { get; }
    public AsyncRelayCommand CreateDebtorPaymentCommand { get; }
    public ICommand RemoveSelectedGridLineCommand { get; }

    public ICommand OpenPaymentDetailsCommand { get; }
    public ICommand SaveLayoutCommand { get; }
    public ICommand SaveSettingsCommand => SaveLayoutCommand;
    public ICommand BillSettingsCommand { get; }
    public ICommand ImageAIBillCommand { get; }
    public AsyncRelayCommand RefreshLookupsCommand { get; }

    public ISettingsRepository? SettingsRepository => _settingsRepository;

    public decimal AllowNtWtModUpto
    {
        get => _allowNtWtModUpto;
        set => SetProperty(ref _allowNtWtModUpto, value);
    }

    private string? _whatsAppDeliveryStatus;
    private string? _whatsAppReply;
    private string? _whatsAppMessageId;

    public string? WhatsAppDeliveryStatus
    {
        get => _whatsAppDeliveryStatus;
        set
        {
            if (SetProperty(ref _whatsAppDeliveryStatus, value))
            {
                OnPropertyChanged(nameof(HasWhatsAppDeliveryStatus));
            }
        }
    }

    public string? WhatsAppReply
    {
        get => _whatsAppReply;
        set => SetProperty(ref _whatsAppReply, value);
    }

    public string? WhatsAppMessageId
    {
        get => _whatsAppMessageId;
        set => SetProperty(ref _whatsAppMessageId, value);
    }

    public bool HasWhatsAppDeliveryStatus => !string.IsNullOrWhiteSpace(WhatsAppDeliveryStatus);

    public ICommand SyncWhatsAppStatusCommand { get; }

    private bool _sendWhatsAppOnSave;

    public bool SendWhatsAppOnSave
    {
        get => _sendWhatsAppOnSave;
        set => SetProperty(ref _sendWhatsAppOnSave, value);
    }

    public string CurrentUser => _currentUser;

    public string CurrentDateTimeString => DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

    public string CurrentDateString => DateTime.Now.ToString("dd/MM/yyyy");

    public string WorkstationName => Environment.MachineName;

    public RangeObservableCollection<string> AvailableItemNames => _availableItemNames;
    public RangeObservableCollection<string> AvailableStockBarcodes => _availableStockBarcodes;
    public RangeObservableCollection<TempStockItem> AvailableStockItems => _availableStockItems;
    public RangeObservableCollection<CustomerLookupItem> AvailableCustomers { get; } = new();
    private readonly List<CustomerLookupItem> _allCustomerLookupItems = new();
    private CustomerLookupItem? _selectedCustomer;

    public CustomerLookupItem? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                if (value != null)
                {
                    ApplyCustomerLookup(value);
                }
                else if (!_isApplyingCustomer && !_isLoadingBill)
                {
                    ClearAutoPopulatedCustomerDetails();
                }
            }
        }
    }

    public async Task LoadCustomerLookupItemsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sortedItems = await Task.Run(async () =>
            {
                var lookupMap = new Dictionary<string, CustomerLookupItem>(StringComparer.OrdinalIgnoreCase);

                // 1. Load from Customer table
                if (_customerRepository != null)
                {
                    var customers = await _customerRepository.GetAllAsync(cancellationToken);
                    foreach (var c in customers)
                    {
                        var name = c.CompanyName?.Trim();
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        var phone = !string.IsNullOrWhiteSpace(c.Mobile) ? c.Mobile.Trim() : (c.Phone?.Trim() ?? string.Empty);
                        var key = $"{name}|{phone}";

                        if (!lookupMap.ContainsKey(key))
                        {
                            var customerCity = c.City?.Trim() ?? string.Empty;
                            var customerPincode = c.Pincode?.Trim() ?? string.Empty;
                            var customerState = string.Empty;

                            if (!string.IsNullOrWhiteSpace(customerPincode) && customerPincode.Length == 6)
                            {
                                var pinRes = ConceptJewelleryPro.Shared.PincodeService.Lookup(customerPincode);
                                if (pinRes.HasValue)
                                {
                                    if (string.IsNullOrWhiteSpace(customerCity) && !string.IsNullOrWhiteSpace(pinRes.Value.City))
                                    {
                                        customerCity = pinRes.Value.City;
                                    }
                                    if (!string.IsNullOrWhiteSpace(pinRes.Value.State))
                                    {
                                        customerState = pinRes.Value.State;
                                    }
                                }
                            }

                            lookupMap[key] = new CustomerLookupItem
                            {
                                Name = name,
                                Phone = phone,
                                Address = c.Address?.Trim() ?? string.Empty,
                                Address2 = c.Address2?.Trim() ?? string.Empty,
                                City = customerCity,
                                Pincode = customerPincode,
                                State = customerState,
                                Email = c.Email?.Trim() ?? string.Empty,
                                Code = c.Code?.Trim() ?? string.Empty,
                                Group = c.Group?.Trim() ?? string.Empty,
                                SourceType = "Customer"
                            };
                        }
                    }
                }

                // 2. Load from AccountCreation table
                if (_accountRepository != null)
                {
                    var accounts = await _accountRepository.GetAllAsync(cancellationToken);
                    foreach (var a in accounts)
                    {
                        var name = a.AccountName?.Trim();
                        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("{", StringComparison.Ordinal)) continue;

                        var phone = !string.IsNullOrWhiteSpace(a.Mobile) ? a.Mobile.Trim() : (a.PhoneNo?.Trim() ?? string.Empty);
                        var key = $"{name}|{phone}";

                        if (!lookupMap.TryGetValue(key, out var existing))
                        {
                            var gstin = !string.IsNullOrWhiteSpace(a.VatNo) ? a.VatNo.Trim() : (a.PanNo?.Trim() ?? string.Empty);
                            var acCity = a.City?.Trim() ?? string.Empty;
                            var acPincode = a.PinCode?.Trim() ?? string.Empty;
                            var acState = !string.IsNullOrWhiteSpace(a.State) ? AccountCreationViewModel.MapStateName(a.State) : string.Empty;

                            if (string.IsNullOrWhiteSpace(acState) && !string.IsNullOrWhiteSpace(acPincode) && acPincode.Length == 6)
                            {
                                var pinRes = ConceptJewelleryPro.Shared.PincodeService.Lookup(acPincode);
                                if (pinRes.HasValue && !string.IsNullOrWhiteSpace(pinRes.Value.State))
                                {
                                    acState = pinRes.Value.State;
                                }
                            }

                            lookupMap[key] = new CustomerLookupItem
                            {
                                Name = name,
                                Phone = phone,
                                Address = a.Address?.Trim() ?? string.Empty,
                                Address2 = a.Address2?.Trim() ?? string.Empty,
                                City = acCity,
                                Pincode = acPincode,
                                State = acState,
                                GstinPan = gstin,
                                VatType = !string.IsNullOrWhiteSpace(a.VatType) ? a.VatType.Trim() : (string.IsNullOrWhiteSpace(gstin) ? "GSTIN" : (gstin.Length == 10 ? "PAN" : "GSTIN")),
                                Email = a.Email?.Trim() ?? string.Empty,
                                Code = a.PartyCode?.Trim() ?? string.Empty,
                                Group = !string.IsNullOrWhiteSpace(a.HeadCategory) ? a.HeadCategory.Trim() : (a.SubCategory?.Trim() ?? string.Empty),
                                SourceType = "Account"
                            };
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(existing.City) && !string.IsNullOrWhiteSpace(a.City))
                            {
                                existing.City = a.City.Trim();
                            }
                            if (string.IsNullOrWhiteSpace(existing.GstinPan))
                            {
                                existing.GstinPan = !string.IsNullOrWhiteSpace(a.VatNo) ? a.VatNo.Trim() : (a.PanNo?.Trim() ?? string.Empty);
                            }
                            if (string.IsNullOrWhiteSpace(existing.State) && !string.IsNullOrWhiteSpace(a.State))
                            {
                                existing.State = AccountCreationViewModel.MapStateName(a.State);
                            }
                            if (string.IsNullOrWhiteSpace(existing.VatType))
                            {
                                existing.VatType = a.VatType?.Trim() ?? string.Empty;
                            }
                        }
                    }
                }

                return lookupMap.Values
                    .OrderBy(x => x.Name)
                    .ToList();
            }, cancellationToken);

            _allCustomerLookupItems.Clear();
            _allCustomerLookupItems.AddRange(sortedItems);

            var app = System.Windows.Application.Current;
            if (app != null)
            {
                await app.Dispatcher.InvokeAsync(() =>
                {
                    AvailableCustomers.ReplaceAll(sortedItems);
                    OnPropertyChanged(nameof(AvailableCustomers));
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    public async Task LoadAvailableStockBarcodesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var targetItemHead = GetTargetItemHeadForVouType(VouType);
            var stockItems = await Task.Run(() => _billRepository.GetTempStockItemsAsync(targetItemHead, cancellationToken), cancellationToken);

            var barcodes = new List<string>(stockItems.Count);
            foreach (var item in stockItems)
            {
                if (!string.IsNullOrWhiteSpace(item.Barcode))
                {
                    barcodes.Add(item.Barcode.Trim());
                }
            }

            var app = System.Windows.Application.Current;
            if (app != null)
            {
                await app.Dispatcher.InvokeAsync(() =>
                {
                    _availableStockItems.ReplaceAll(stockItems);
                    _availableStockBarcodes.ReplaceAll(barcodes);

                    OnPropertyChanged(nameof(AvailableStockItems));
                    OnPropertyChanged(nameof(AvailableStockBarcodes));
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    public string VouType
    {
        get => _vouType;
        set
        {
            if (string.Equals(_vouType, value, StringComparison.OrdinalIgnoreCase)) return;

            var clean = value?.Trim() ?? string.Empty;
            var matched = _salesVouTypes.FirstOrDefault(v => string.Equals(v.VouType, clean, StringComparison.OrdinalIgnoreCase))
                       ?? _salesVouTypes.FirstOrDefault(v => v.VouType.StartsWith(clean, StringComparison.OrdinalIgnoreCase))
                       ?? _salesVouTypes.FirstOrDefault(v => v.VouType.Contains(clean, StringComparison.OrdinalIgnoreCase));

            var effective = matched?.VouType ?? clean;

            if (SetProperty(ref _vouType, effective))
            {
                if (!string.IsNullOrWhiteSpace(effective) && !_isLoadingBill && IsEditing)
                {
                    UpdateAvailableItemsForVouType(effective);
                    _ = LoadAvailableStockBarcodesAsync();
                    _ = AutoGenerateInvoiceNoAsync(effective);
                }
            }
        }
    }

    public void ResolveAndSetVouType(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;
        VouType = input;
    }

    public bool ConvertRetailToInvoice()
    {
        if (!IsEditing || string.IsNullOrWhiteSpace(_vouType)) return false;

        var updated = System.Text.RegularExpressions.Regex
            .Replace(_vouType, @"\bRETAIL\b", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Trim();
        updated = System.Text.RegularExpressions.Regex.Replace(updated, @"\s{2,}", " ");

        if (string.IsNullOrWhiteSpace(updated) || string.Equals(updated, _vouType.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        var exactMatch = _salesVouTypes.FirstOrDefault(v => string.Equals(v.VouType, updated, StringComparison.OrdinalIgnoreCase));
        var target = exactMatch?.VouType ?? updated;

        if (!InvoiceTypes.Any(t => string.Equals(t, target, StringComparison.OrdinalIgnoreCase)))
        {
            InvoiceTypes.Add(target);
        }

        if (SetProperty(ref _vouType, target, nameof(VouType)))
        {
            if (!_isLoadingBill && IsEditing)
            {
                UpdateAvailableItemsForVouType(target);
                _ = LoadAvailableStockBarcodesAsync();
                _ = AutoGenerateInvoiceNoAsync(target);
            }
        }

        return true;
    }

    public string? GetTargetItemHeadForVouType(string? vouType = null)
    {
        var type = !string.IsNullOrWhiteSpace(vouType) ? vouType : VouType;
        if (string.IsNullOrWhiteSpace(type)) return null;

        var matchedVou = _salesVouTypes.FirstOrDefault(v => string.Equals(v.VouType, type.Trim(), StringComparison.OrdinalIgnoreCase));
        return matchedVou?.ItemHead?.Trim();
    }

    public string? GetPrintFileForVouType(string? vouType = null)
    {
        var type = !string.IsNullOrWhiteSpace(vouType) ? vouType : VouType;
        if (string.IsNullOrWhiteSpace(type)) return null;

        var matchedVou = _salesVouTypes.FirstOrDefault(v => string.Equals(v.VouType, type.Trim(), StringComparison.OrdinalIgnoreCase))
                      ?? _salesVouTypes.FirstOrDefault(v => v.VouType.StartsWith(type.Trim(), StringComparison.OrdinalIgnoreCase))
                      ?? _salesVouTypes.FirstOrDefault(v => v.VouType.Contains(type.Trim(), StringComparison.OrdinalIgnoreCase));
        return matchedVou?.PrintFile?.Trim();
    }

    public bool IsItemHeadAllowed(string? itemName, string? itemHeadGroup = null)
    {
        var targetItemHead = GetTargetItemHeadForVouType();
        if (string.IsNullOrWhiteSpace(targetItemHead))
        {
            return true; // No ItemHead restriction on this voucher type
        }

        var allowedHeads = targetItemHead.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (allowedHeads.Length == 0) return true;

        var headGroup = itemHeadGroup;
        var group = string.Empty;
        if (string.IsNullOrWhiteSpace(headGroup) && !string.IsNullOrWhiteSpace(itemName))
        {
            if (_itemGroupMap.TryGetValue(itemName.Trim(), out var grp))
            {
                headGroup = grp.HeadGroup;
                group = grp.Group;
            }
        }

        if (string.IsNullOrWhiteSpace(headGroup) && string.IsNullOrWhiteSpace(group))
        {
            return true;
        }

        return allowedHeads.Any(h =>
            (!string.IsNullOrWhiteSpace(headGroup) && string.Equals(h, headGroup.Trim(), StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(group) && string.Equals(h, group.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private void UpdateAvailableItemsForVouType(string? vouType)
    {
        if (_allItems.Count == 0)
        {
            _availableItemNames.Clear();
            return;
        }

        var targetItemHead = GetTargetItemHeadForVouType(vouType);
        IEnumerable<Item> filtered;
        if (!string.IsNullOrWhiteSpace(targetItemHead))
        {
            var allowedHeads = targetItemHead.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            filtered = _allItems.Where(i =>
                allowedHeads.Any(h =>
                    (!string.IsNullOrWhiteSpace(i.HeadGroup) && string.Equals(i.HeadGroup.Trim(), h, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(i.Group) && string.Equals(i.Group.Trim(), h, StringComparison.OrdinalIgnoreCase))));
        }
        else
        {
            filtered = _allItems;
        }

        // Guardrail: Restrict items from ItemName selection dropdown if Allowed = 0
        filtered = filtered.Where(i => i.Allowed && i.IsActive);

        var names = filtered
            .Select(i => i.ItemName?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        if (names.Count == 0 && !string.IsNullOrWhiteSpace(targetItemHead))
        {
            names = _allItems
                .Where(i => i.Allowed && i.IsActive)
                .Select(i => i.ItemName?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();
        }

        _availableItemNames.ReplaceAll(names);
        OnPropertyChanged(nameof(AvailableItemNames));
    }

    public string GetHsnForItem(string? itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return "7113";
        var match = _allItems.FirstOrDefault(i => string.Equals(i.ItemName?.Trim(), itemName.Trim(), StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(match?.HSNCode) ? match.HSNCode.Trim() : "7113";
    }

    public string GetInvoiceHsnSummary()
    {
        var hsns = Items
            .Where(IsItemRowFilled)
            .Select(i => !string.IsNullOrWhiteSpace(i.HsnCode) ? i.HsnCode.Trim() : GetHsnForItem(i.ItemName))
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return hsns.Count > 0 ? string.Join(", ", hsns) : "7113";
    }

    public async Task AutoGenerateInvoiceNoAsync(string? vouType = null)
    {
        var type = !string.IsNullOrWhiteSpace(vouType) ? vouType : VouType;
        if (string.IsNullOrWhiteSpace(type) || !IsEditing) return;

        try
        {
            var nextVouNo = await _billRepository.GetNextVouNoAsync(type);
            if (!string.IsNullOrWhiteSpace(nextVouNo))
            {
                VouNo = nextVouNo;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to auto-generate Invoice No: " + ex.Message;
        }
    }

    public string VouNo
    {
        get => _vouNo;
        set
        {
            if (SetProperty(ref _vouNo, value))
            {
                RaiseSessionTitleChanged();
            }
        }
    }

    public DateTime BillDate
    {
        get => _billDate;
        set => SetProperty(ref _billDate, value);
    }

    public DateTime DueDate
    {
        get => _dueDate;
        set => SetProperty(ref _dueDate, value);
    }

    public string Phone
    {
        get => _phone;
        set
        {
            if (SetProperty(ref _phone, value))
            {
                if (!_isApplyingCustomer)
                {
                    _isPhoneAutoPopulated = false;
                }
                if (IsEditing && !_isLoadingBill && !string.IsNullOrWhiteSpace(value))
                {
                    _ = LookupCustomerByPhoneAsync(value);
                }
            }
        }
    }

    private async Task LookupCustomerByPhoneAsync(string phone)
    {
        if (_customerRepository is null)
        {
            return;
        }

        _customerLookupCts?.Cancel();
        var cts = new CancellationTokenSource();
        _customerLookupCts = cts;

        try
        {
            var cleanDigits = new string(phone.Where(char.IsDigit).ToArray());
            if (cleanDigits.Length < 10)
            {
                return;
            }

            await Task.Delay(250, cts.Token);

            var customer = await _customerRepository.FindByPhoneOrMobileAsync(phone, cts.Token);
            if (customer is not null && !cts.IsCancellationRequested)
            {
                _isPhoneAutoPopulated = false;
                CustomerName = customer.CompanyName;
                CustomerCode = customer.Code;
                CustomerGroup = !string.IsNullOrWhiteSpace(customer.Group) ? customer.Group : "General";
                Address = customer.Address;
                Address2 = customer.Address2;
                Pincode = customer.Pincode;
                _isAddressAutoPopulated = true;

                if (!string.IsNullOrWhiteSpace(customer.City))
                {
                    City = customer.City;
                }

                if (!string.IsNullOrWhiteSpace(customer.Email))
                {
                    Email = customer.Email;
                }

                StatusMessage = $"Customer '{customer.CompanyName}' auto-populated from mobile number.";
                _ = LoadCustomerHistoryAndMetricsAsync(phone, customer.CompanyName, customer.Code);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancelled debounce tasks
        }
        catch (Exception ex)
        {
            ErrorMessage = "Customer lookup failed: " + ex.Message;
        }
    }

    private bool _isApplyingCustomer;
    private bool _isPhoneAutoPopulated;
    private bool _isAddressAutoPopulated;

    public string CustomerName
    {
        get => _customerName;
        set
        {
            if (SetProperty(ref _customerName, value))
            {
                RaiseSessionTitleChanged();
                if (!_isApplyingCustomer && !_isLoadingBill)
                {
                    var clean = value?.Trim() ?? string.Empty;
                    if (_selectedCustomer != null)
                    {
                        if (!string.Equals(_selectedCustomer.Name?.Trim(), clean, StringComparison.OrdinalIgnoreCase))
                        {
                            _selectedCustomer = null;
                            OnPropertyChanged(nameof(SelectedCustomer));
                            ClearAutoPopulatedCustomerDetails();
                        }
                    }
                    else if (_isPhoneAutoPopulated || _isAddressAutoPopulated || !string.IsNullOrWhiteSpace(_customerCode))
                    {
                        ClearAutoPopulatedCustomerDetails();
                    }
                }
            }
        }
    }

    private void ClearAutoPopulatedCustomerDetails()
    {
        if (_isPhoneAutoPopulated)
        {
            _phone = string.Empty;
            _isPhoneAutoPopulated = false;
            OnPropertyChanged(nameof(Phone));
        }

        if (_isAddressAutoPopulated)
        {
            _address = string.Empty;
            _address2 = string.Empty;
            _pincode = string.Empty;
            _isAddressAutoPopulated = false;
            OnPropertyChanged(nameof(Address));
            OnPropertyChanged(nameof(Address2));
            OnPropertyChanged(nameof(Pincode));
        }

        _customerCode = string.Empty;
        OnPropertyChanged(nameof(CustomerCode));

        CustomerGroup = "General";
        LastPurchaseDate = null;
        AvgPurchaseValue = 0;
        TotalPurchaseValue = 0;
        CustomerPurchaseHistory.Clear();
        OnPropertyChanged(nameof(HasCustomerHistory));
        OnPropertyChanged(nameof(HasNoCustomerHistory));
    }

    public void ApplyCustomerLookup(CustomerLookupItem? item)
    {
        if (item is null || _isApplyingCustomer || _isLoadingBill) return;

        try
        {
            _isApplyingCustomer = true;

            _customerName = item.Name;
            OnPropertyChanged(nameof(CustomerName));

            _selectedCustomer = item;
            OnPropertyChanged(nameof(SelectedCustomer));

            if (!string.IsNullOrWhiteSpace(item.Phone))
            {
                _phone = item.Phone;
                _isPhoneAutoPopulated = true;
                OnPropertyChanged(nameof(Phone));
            }

            if (!string.IsNullOrWhiteSpace(item.Address))
            {
                _address = item.Address;
                _isAddressAutoPopulated = true;
                OnPropertyChanged(nameof(Address));
            }

            if (!string.IsNullOrWhiteSpace(item.Address2))
            {
                _address2 = item.Address2;
                OnPropertyChanged(nameof(Address2));
            }

            if (!string.IsNullOrWhiteSpace(item.Pincode))
            {
                _pincode = item.Pincode;
                OnPropertyChanged(nameof(Pincode));
                AutoloadLocationFromPincode(item.Pincode);
            }

            if (!string.IsNullOrWhiteSpace(item.City))
            {
                _city = item.City.Trim();
                OnPropertyChanged(nameof(City));
            }

            if (!string.IsNullOrWhiteSpace(item.State))
            {
                _state = PincodeService.MapStateName(item.State);
                OnPropertyChanged(nameof(State));
            }
            else if (!string.IsNullOrWhiteSpace(item.GstinPan) && item.GstinPan.Length >= 2 && char.IsDigit(item.GstinPan[0]) && char.IsDigit(item.GstinPan[1]))
            {
                var prefix = item.GstinPan.Substring(0, 2);
                var mapped = prefix switch
                {
                    "21" => "21-ODISHA",
                    "19" => "19-WEST BENGAL",
                    "10" => "10-BIHAR",
                    "20" => "20-JHARKHAND",
                    "37" => "37-ANDHRA PRADESH",
                    "36" => "36-TELANGANA",
                    "27" => "27-MAHARASHTRA",
                    "07" => "07-DELHI",
                    "29" => "29-KARNATAKA",
                    "33" => "33-TAMIL NADU",
                    "24" => "24-GUJARAT",
                    "09" => "09-UTTAR PRADESH",
                    "23" => "23-MADHYA PRADESH",
                    "08" => "08-RAJASTHAN",
                    "03" => "03-PUNJAB",
                    "06" => "06-HARYANA",
                    "22" => "22-CHHATTISGARH",
                    "18" => "18-ASSAM",
                    "32" => "32-KERALA",
                    "30" => "30-GOA",
                    _ => string.Empty
                };
                if (!string.IsNullOrWhiteSpace(mapped))
                {
                    _state = mapped;
                    OnPropertyChanged(nameof(State));
                }
            }

            if (!string.IsNullOrWhiteSpace(item.GstinPan))
            {
                _gstinPan = item.GstinPan;
                OnPropertyChanged(nameof(GstinPan));
            }

            if (!string.IsNullOrWhiteSpace(item.VatType))
            {
                _vatType = item.VatType;
                OnPropertyChanged(nameof(VatType));
            }

            if (!string.IsNullOrWhiteSpace(item.Email))
            {
                _email = item.Email;
                OnPropertyChanged(nameof(Email));
            }

            if (!string.IsNullOrWhiteSpace(item.Code))
            {
                _customerCode = item.Code;
                OnPropertyChanged(nameof(CustomerCode));
            }

            _customerGroup = !string.IsNullOrWhiteSpace(item.Group) ? item.Group : "General";
            OnPropertyChanged(nameof(CustomerGroup));
            OnPropertyChanged(nameof(CustomerGroupDisplay));

            StatusMessage = $"Loaded details for '{item.Name}'.";
            _ = LoadCustomerHistoryAndMetricsAsync(_phone, _customerName, _customerCode);
        }
        finally
        {
            _isApplyingCustomer = false;
        }
    }

    public string CustomerCode
    {
        get => _customerCode;
        set => SetProperty(ref _customerCode, value);
    }

    private int _selectedCustomerTab = 0;
    public int SelectedCustomerTab
    {
        get => _selectedCustomerTab;
        set
        {
            if (SetProperty(ref _selectedCustomerTab, value))
            {
                OnPropertyChanged(nameof(IsCustomerOverviewTabSelected));
                OnPropertyChanged(nameof(IsCustomerHistoryTabSelected));
            }
        }
    }

    public bool IsCustomerOverviewTabSelected
    {
        get => _selectedCustomerTab == 0;
        set
        {
            if (value) SelectedCustomerTab = 0;
        }
    }

    public bool IsCustomerHistoryTabSelected
    {
        get => _selectedCustomerTab == 1;
        set
        {
            if (value) SelectedCustomerTab = 1;
        }
    }

    private string _customerGroup = string.Empty;
    public string CustomerGroup
    {
        get => _customerGroup;
        set
        {
            if (SetProperty(ref _customerGroup, value))
            {
                OnPropertyChanged(nameof(CustomerGroupDisplay));
            }
        }
    }

    public string CustomerGroupDisplay => string.IsNullOrWhiteSpace(_customerGroup) ? "General" : _customerGroup;

    private DateTime? _lastPurchaseDate;
    public DateTime? LastPurchaseDate
    {
        get => _lastPurchaseDate;
        set
        {
            if (SetProperty(ref _lastPurchaseDate, value))
            {
                OnPropertyChanged(nameof(LastPurchaseDateFormatted));
            }
        }
    }

    public string LastPurchaseDateFormatted => LastPurchaseDate.HasValue ? LastPurchaseDate.Value.ToString("dd/MM/yyyy") : "—";

    private decimal _avgPurchaseValue;
    public decimal AvgPurchaseValue
    {
        get => _avgPurchaseValue;
        set
        {
            if (SetProperty(ref _avgPurchaseValue, value))
            {
                OnPropertyChanged(nameof(AvgPurchaseValueFormatted));
            }
        }
    }

    public string AvgPurchaseValueFormatted => AvgPurchaseValue > 0 ? $"₹ {AvgPurchaseValue:N2}" : "—";

    private decimal _totalPurchaseValue;
    public decimal TotalPurchaseValue
    {
        get => _totalPurchaseValue;
        set
        {
            if (SetProperty(ref _totalPurchaseValue, value))
            {
                OnPropertyChanged(nameof(TotalPurchaseValueFormatted));
            }
        }
    }

    public string TotalPurchaseValueFormatted => TotalPurchaseValue > 0 ? $"₹ {TotalPurchaseValue:N2}" : "—";

    public RangeObservableCollection<CustomerPurchaseHistoryItem> CustomerPurchaseHistory { get; } = new();

    public bool HasCustomerHistory => CustomerPurchaseHistory.Count > 0;
    public bool HasNoCustomerHistory => CustomerPurchaseHistory.Count == 0;

    private CancellationTokenSource? _customerHistoryCts;

    public async Task LoadCustomerHistoryAndMetricsAsync(string? phone, string? customerName, string? customerCode)
    {
        if (_billRepository == null) return;

        _customerHistoryCts?.Cancel();
        var cts = new CancellationTokenSource();
        _customerHistoryCts = cts;

        try
        {
            var summary = await _billRepository.GetCustomerPurchaseSummaryAsync(phone, customerName, customerCode, cts.Token);
            if (cts.IsCancellationRequested) return;

            var app = System.Windows.Application.Current;
            if (app != null)
            {
                await app.Dispatcher.InvokeAsync(() =>
                {
                    TotalPurchaseValue = summary.TotalPurchaseValue;
                    AvgPurchaseValue = summary.AvgPurchaseValue;
                    LastPurchaseDate = summary.LastPurchaseDate;

                    CustomerPurchaseHistory.ReplaceAll(summary.Items);
                    OnPropertyChanged(nameof(HasCustomerHistory));
                    OnPropertyChanged(nameof(HasNoCustomerHistory));
                });
            }
            else
            {
                TotalPurchaseValue = summary.TotalPurchaseValue;
                AvgPurchaseValue = summary.AvgPurchaseValue;
                LastPurchaseDate = summary.LastPurchaseDate;

                CustomerPurchaseHistory.ReplaceAll(summary.Items);
                OnPropertyChanged(nameof(HasCustomerHistory));
                OnPropertyChanged(nameof(HasNoCustomerHistory));
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled debounce / switch
        }
        catch
        {
            // Non-fatal
        }
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public string Address2
    {
        get => _address2;
        set => SetProperty(ref _address2, value);
    }

    public string City
    {
        get => _city;
        set => SetProperty(ref _city, value);
    }

    public string Pincode
    {
        get => _pincode;
        set
        {
            if (SetProperty(ref _pincode, value))
            {
                AutoloadLocationFromPincode(value);
            }
        }
    }

    private void AutoloadLocationFromPincode(string? pincode)
    {
        if (string.IsNullOrWhiteSpace(pincode))
        {
            return;
        }

        var clean = pincode.Trim();
        if (clean.Length == 6)
        {
            var result = ConceptJewelleryPro.Shared.PincodeService.Lookup(clean);
            if (result.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(result.Value.City))
                {
                    City = result.Value.City;
                }
                if (!string.IsNullOrWhiteSpace(result.Value.State))
                {
                    State = result.Value.State;
                }
            }
        }
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string GstinPan
    {
        get => _gstinPan;
        set => SetProperty(ref _gstinPan, value);
    }

    private string _vatType = "GSTIN";
    private string _state = "21-ODISHA";

    public string VatType
    {
        get => _vatType;
        set => SetProperty(ref _vatType, value);
    }

    public string State
    {
        get => _state;
        set => SetProperty(ref _state, AccountCreationViewModel.MapStateName(value));
    }

    public IReadOnlyList<string> VatTypes { get; } = new List<string> { "GSTIN", "PAN", "TIN", "AADHAAR", "PASSPORT" };

    public bool Check
    {
        get => _check;
        set => SetProperty(ref _check, value);
    }

    public bool Sms
    {
        get => _sms;
        set => SetProperty(ref _sms, value);
    }

    public string Remarks
    {
        get => _remarks;
        set => SetProperty(ref _remarks, value);
    }

    public int Q1
    {
        get => _q1;
        set => SetProperty(ref _q1, value);
    }

    public int Q2
    {
        get => _q2;
        set => SetProperty(ref _q2, value);
    }

    public int Q3
    {
        get => _q3;
        set => SetProperty(ref _q3, value);
    }

    public decimal GrossAmt
    {
        get => _grossAmt;
        private set => SetProperty(ref _grossAmt, value);
    }

    public decimal Discount
    {
        get => _discount;
        set
        {
            if (SetProperty(ref _discount, value))
            {
                if (!_isRecalculatingTotals && !_isLoadingBill)
                {
                    var itemDiscounts = Items.Sum(i => i.DisAmt);
                    _additionalBillDiscount = Math.Max(0m, value - itemDiscounts);
                }
                RecalculateTotals();
            }
        }
    }

    public decimal NetAmt
    {
        get => _netAmt;
        private set => SetProperty(ref _netAmt, value);
    }

    public decimal TotalPaid
    {
        get => _totalPaid;
        private set => SetProperty(ref _totalPaid, value);
    }

    public decimal BalanceAmt
    {
        get => _balanceAmt;
        private set => SetProperty(ref _balanceAmt, value);
    }

    private string _couponCode = string.Empty;
    private Coupon? _appliedCoupon;
    private decimal _couponDiscountAmount;
    private string _couponStatusText = string.Empty;

    public string CouponCode
    {
        get => _couponCode;
        set => SetProperty(ref _couponCode, value);
    }

    public Coupon? AppliedCoupon
    {
        get => _appliedCoupon;
        private set
        {
            if (SetProperty(ref _appliedCoupon, value))
            {
                OnPropertyChanged(nameof(HasAppliedCoupon));
            }
        }
    }

    public bool HasAppliedCoupon => AppliedCoupon != null;

    public decimal CouponDiscountAmount
    {
        get => _couponDiscountAmount;
        private set => SetProperty(ref _couponDiscountAmount, value);
    }

    public string CouponStatusText
    {
        get => _couponStatusText;
        private set => SetProperty(ref _couponStatusText, value);
    }

    public ICommand ApplyCouponCommand { get; }
    public ICommand RemoveCouponCommand { get; }

    private async Task ApplyCouponAsync()
    {
        if (_couponEngineService == null)
        {
            CouponStatusText = "Coupon engine not initialized.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CouponCode))
        {
            ShowToast("Enter a coupon code to apply.", ToastType.Warning);
            return;
        }

        var result = await _couponEngineService.ValidateCouponAsync(CouponCode, Phone, GrossAmt, BillDate);
        if (!result.IsValid || result.Coupon == null)
        {
            CouponStatusText = result.Message;
            ShowToast(result.Message, ToastType.Warning);
            return;
        }

        if (CouponDiscountAmount > 0)
        {
            Discount = Math.Max(0m, Discount - CouponDiscountAmount);
        }

        AppliedCoupon = result.Coupon;
        CouponDiscountAmount = result.DiscountAmount;
        Discount += result.DiscountAmount;
        CouponStatusText = $"{result.Coupon.CouponCode} applied (-₹{result.DiscountAmount:N2})";
        ShowToast(result.Message, ToastType.Success);
    }

    private void RemoveCoupon()
    {
        if (CouponDiscountAmount > 0)
        {
            Discount = Math.Max(0m, Discount - CouponDiscountAmount);
        }

        AppliedCoupon = null;
        CouponDiscountAmount = 0m;
        CouponCode = string.Empty;
        CouponStatusText = string.Empty;
    }

    public int TotalQty => Items.Sum(i => i.Qty);
    public decimal TotalGrWt => Items.Sum(i => i.GrWt);
    public decimal TotalNtWt => Items.Sum(i => i.NtWt);
    public decimal TotalMetalValue => Items.Sum(i => i.MetalValue);
    public decimal TotalMaking => Items.Sum(i => i.Making);
    public decimal TotalTaxAmt
    {
        get
        {
            var validItems = Items.Where(IsItemRowFilled).ToList();
            if (validItems.Count > 0)
            {
                var sumVat = validItems.Sum(i => i.VatAmt);
                if (sumVat > 0) return sumVat;
            }
            var diff = NetAmt - (GrossAmt - Discount);
            if (diff > 0) return diff;
            return Math.Round(NetAmt - (NetAmt / 1.03m), 2);
        }
    }

    public decimal CgstAmt => Math.Round(TotalTaxAmt / 2m, 2);

    public decimal SgstAmt => Math.Round(TotalTaxAmt - CgstAmt, 2);

    private string? _storeName;
    public string StoreName
    {
        get => !string.IsNullOrWhiteSpace(_storeName) ? _storeName : ConceptJewelleryPro.Infrastructure.Services.DateRangeService.GetCompanyDetails().CompanyName;
        set => _storeName = value;
    }

    private string? _storeGstin;
    public string StoreGstin
    {
        get => !string.IsNullOrWhiteSpace(_storeGstin) ? _storeGstin : ConceptJewelleryPro.Infrastructure.Services.DateRangeService.GetCompanyDetails().Gstin;
        set => _storeGstin = value;
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(IsNotEditing));
            }
        }
    }

    public bool IsNotEditing => !IsEditing;

    public string? ToastMessage
    {
        get => _toastMessage;
        internal set => SetProperty(ref _toastMessage, value);
    }

    public ToastType ToastType
    {
        get => _toastType;
        internal set => SetProperty(ref _toastType, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        internal set
        {
            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
            if (!string.IsNullOrWhiteSpace(value))
            {
                _errorMessage = string.Empty;
                OnPropertyChanged(nameof(ErrorMessage));
                ToastType = ToastType.Success;
                ToastMessage = value;
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        internal set
        {
            _errorMessage = value;
            OnPropertyChanged(nameof(ErrorMessage));
            if (!string.IsNullOrWhiteSpace(value))
            {
                _statusMessage = string.Empty;
                OnPropertyChanged(nameof(StatusMessage));
                ToastType = ToastType.Error;
                ToastMessage = value;
            }
        }
    }

    public event EventHandler<PaymentSubformItemViewModel>? RequestFocusPaymentAmount;

    public Bill? SelectedBill
    {
        get => _selectedBill;
        set
        {
            if (SetProperty(ref _selectedBill, value) && value is not null)
            {
                if (!string.Equals(VouNo, value.VouNo, StringComparison.OrdinalIgnoreCase))
                {
                    _ = LoadBillAsync(value.VouNo);
                }
            }
        }
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            IsBusy = true;

            var typesTask = LoadInvoiceTypesAsync();
            var payTask = LoadPaymentAccountsAsync();
            var purTask = LoadPuritiesAsync();
            var itemsTask = LoadItemsAsync();
            var custTask = LoadCustomerLookupItemsAsync();
            var settingsTask = LoadSettingsAsync();

            Task? advTask = null;
            if (_accountRepository != null)
            {
                advTask = Task.Run(async () =>
                {
                    try
                    {
                        var adv = await _accountRepository.GetAdvanceOptionsAsync();
                        var app = System.Windows.Application.Current;
                        if (app != null)
                        {
                            await app.Dispatcher.InvokeAsync(() =>
                            {
                                _advanceOptions.Clear();
                                _advanceOptions.AddRange(adv);
                            });
                        }
                    }
                    catch { }
                });
            }

            Task? itemTypesTask = null;
            if (_itemRepository != null)
            {
                itemTypesTask = Task.Run(async () =>
                {
                    try
                    {
                        var types = await _itemRepository.GetItemTypesAsync();
                        var app = System.Windows.Application.Current;
                        if (app != null)
                        {
                            await app.Dispatcher.InvokeAsync(() =>
                            {
                                _allItemTypes.Clear();
                                _allItemTypes.AddRange(types);
                            });
                        }
                    }
                    catch { }
                });
            }

            var stockTask = LoadAvailableStockBarcodesAsync();

            var tasks = new List<Task> { typesTask, payTask, purTask, itemsTask, custTask, settingsTask, stockTask };
            if (advTask != null) tasks.Add(advTask);
            if (itemTypesTask != null) tasks.Add(itemTypesTask);

            await Task.WhenAll(tasks);

            // Defer loading all historical bills to background task so invoice unlocks immediately
            _ = Task.Run(async () =>
            {
                try
                {
                    var list = await _billRepository.GetAllAsync();
                    var app = System.Windows.Application.Current;
                    if (app != null)
                    {
                        await app.Dispatcher.InvokeAsync(() => Bills.ReplaceAll(list));
                    }
                }
                catch
                {
                    // Non-fatal background load
                }
            });

            EnsureDefaultRows();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load initial data: " + ex.Message;
            EnsureDefaultRows();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSettingsAsync()
    {
        if (_settingsRepository is null) return;
        try
        {
            var settings = await _settingsRepository.GetAllAsync();
            if (settings.TryGetValue(AppSetting.AllowNtWtModUptoKey, out var modStr) && decimal.TryParse(modStr, out var modVal))
            {
                AllowNtWtModUpto = modVal;
            }
            else
            {
                AllowNtWtModUpto = 0.000m;
            }

            if (settings.TryGetValue(AppSetting.WaAutoSendKey, out var autoSendStr) && bool.TryParse(autoSendStr, out var autoSendVal))
            {
                _waAutoSend = autoSendVal;
            }
            else
            {
                _waAutoSend = false;
            }

            if (!_isExistingInvoice)
            {
                SendWhatsAppOnSave = _waAutoSend;
            }
        }
        catch
        {
            // Fallback default
            AllowNtWtModUpto = 0.000m;
            _waAutoSend = false;
            if (!_isExistingInvoice)
            {
                SendWhatsAppOnSave = false;
            }
        }
    }

    private async Task LoadPuritiesAsync()
    {
        if (_purityRepository is null) return;
        try
        {
            var purities = await _purityRepository.GetAllAsync();
            _allPurities.Clear();
            _allPurities.AddRange(purities);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load purities: " + ex.Message;
        }
    }

    private async Task LoadItemsAsync()
    {
        if (_itemRepository is null) return;
        try
        {
            var items = await _itemRepository.GetAllAsync();
            _allItems.Clear();
            _allItems.AddRange(items);

            _itemGroupMap.Clear();
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.ItemName))
                {
                    _itemGroupMap[item.ItemName.Trim()] = (item.HeadGroup ?? string.Empty, item.Group ?? string.Empty);
                }
            }

            UpdateAvailableItemsForVouType(VouType);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load items: " + ex.Message;
        }
    }

    private async Task LoadInvoiceTypesAsync()
    {
        var appSettings = AppSettings.Load();
        var currentCounter = appSettings.Counter;

        try
        {
            var vouTypes = await _billRepository.GetSalesVouTypesAsync(currentCounter, "Sales");
            _salesVouTypes.Clear();
            _salesVouTypes.AddRange(vouTypes);

            var names = vouTypes
                .Select(v => v.VouType)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            InvoiceTypes.Clear();
            if (names.Count > 0)
            {
                foreach (var name in names)
                {
                    InvoiceTypes.Add(name);
                }
            }
            else
            {
                InvoiceTypes.Add("TAX INVOICE GOLD");
                InvoiceTypes.Add("RETAIL INVOICE GOLD");
                InvoiceTypes.Add("TAX INVOICE ARTICLES");
                InvoiceTypes.Add("TAX INVOICE BULLION");
            }
        }
        catch
        {
            InvoiceTypes.Clear();
            InvoiceTypes.Add("TAX INVOICE GOLD");
            InvoiceTypes.Add("RETAIL INVOICE GOLD");
            InvoiceTypes.Add("TAX INVOICE ARTICLES");
            InvoiceTypes.Add("TAX INVOICE BULLION");
        }
    }

    private async Task LoadPaymentAccountsAsync()
    {
        var fallback = new List<string> { "CASH", "HDFC", "SBI", "ICICI BANK", "Credit Card", "UPI Payment" };

        if (_accountRepository is null)
        {
            _paymentAccountNames.Clear();
            foreach (var name in fallback)
            {
                _paymentAccountNames.Add(name);
            }
            OnPropertyChanged(nameof(PaymentAccountNames));
            return;
        }

        try
        {
            var accounts = await _accountRepository.GetAllAsync();
            var names = accounts
                .Where(a => !string.IsNullOrWhiteSpace(a.HeadCategory)
                    && AllowedHeadCategories.Any(cat => string.Equals(a.HeadCategory.Trim(), cat, StringComparison.OrdinalIgnoreCase)))
                .Select(a => a.AccountName?.Trim() ?? string.Empty)
                .Where(n => !string.IsNullOrWhiteSpace(n) && !n.StartsWith("{", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(n => n.Equals("CASH", StringComparison.OrdinalIgnoreCase) || n.Equals("Cash Account", StringComparison.OrdinalIgnoreCase))
                .ThenBy(n => n)
                .ToList();

            _allAccounts.Clear();
            _allAccounts.AddRange(accounts);

            _paymentAccountNames.Clear();
            foreach (var name in names.Count > 0 ? names : fallback)
            {
                _paymentAccountNames.Add(name);
            }
            OnPropertyChanged(nameof(PaymentAccountNames));
        }
        catch
        {
            _paymentAccountNames.Clear();
            foreach (var name in fallback)
            {
                _paymentAccountNames.Add(name);
            }
            OnPropertyChanged(nameof(PaymentAccountNames));
        }
    }

    public async Task RefreshAsync() => await RefreshLookupsAsync();

    public async Task RefreshLookupsAsync()
    {
        try
        {
            IsBusy = true;
            await Task.WhenAll(
                LoadItemsAsync(),
                LoadPuritiesAsync(),
                LoadPaymentAccountsAsync(),
                LoadInvoiceTypesAsync()
            );
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

    public async Task AddNewAsync()
    {
        ClearForm();
        _isExistingInvoice = false;
        AppliedCoupon = null;
        CouponDiscountAmount = 0m;
        CouponCode = string.Empty;
        CouponStatusText = string.Empty;
        SendWhatsAppOnSave = _waAutoSend;
        _selectedBill = null;
        OnPropertyChanged(nameof(SelectedBill));

        BillDate = DateTime.Today;
        DueDate = DateTime.Today;
        IsEditing = true;

        // Ensure blank rows immediately so the grid is never empty while loading
        EnsureDefaultRows();

        if (InvoiceTypes.Count > 0)
        {
            var defaultType = InvoiceTypes.FirstOrDefault(t => t.Contains("GOLD", StringComparison.OrdinalIgnoreCase)) ?? InvoiceTypes[0];
            if (!string.Equals(VouType, defaultType, StringComparison.OrdinalIgnoreCase))
            {
                VouType = defaultType;
            }
            else
            {
                UpdateAvailableItemsForVouType(defaultType);
                _ = LoadAvailableStockBarcodesAsync();
                await AutoGenerateInvoiceNoAsync(defaultType);
            }
        }
        else
        {
            VouType = string.Empty;
        }

        StatusMessage = "Ready for new invoice entry";
        ErrorMessage = string.Empty;
        RaiseSessionTitleChanged();

        EnsureDefaultRows();

        RequestFocusInvoiceType?.Invoke(this, EventArgs.Empty);
    }

    private void Modify()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ErrorMessage = "Please select an invoice to modify";
            return;
        }

        IsEditing = true;
        SendWhatsAppOnSave = false; // By default ticked off in modify mode
        StatusMessage = $"Editing Invoice #{VouNo}";
        ErrorMessage = string.Empty;

        if (Items.Count == 0 || IsItemRowFilled(Items.Last()))
        {
            AddItemRow();
        }
        if (Payments.Count == 0 || (Payments.Count > 0 && !string.IsNullOrWhiteSpace(Payments.Last().AccountName) && Payments.Last().Amount > 0))
        {
            AddPaymentRow();
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            _errorMessage = "Invoice No is required.";
            OnPropertyChanged(nameof(ErrorMessage));
            System.Windows.MessageBox.Show("Invoice No is required.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        RecalculateTotals();

        if (Math.Abs(NetAmt - TotalPaid) > 0.01m)
        {
            _errorMessage = $"Total payment (₹{TotalPaid:N2}) must be equal to Bill Net Amount (₹{NetAmt:N2}). Pending: ₹{BalanceAmt:N2}";
            OnPropertyChanged(nameof(ErrorMessage));
            System.Windows.MessageBox.Show(
                $"Total Payment (₹{TotalPaid:N2}) does not match Bill Net Amount (₹{NetAmt:N2}).\n\nPending Amount: ₹{BalanceAmt:N2}\n\nPlease adjust the payment amounts so total payment matches the bill amount before saving.",
                "Payment Validation Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (!Items.Any(IsItemRowFilled))
        {
            ErrorMessage = "Please add at least one valid item before saving";
            System.Windows.MessageBox.Show(
                "Please add at least one valid item to the invoice before saving.",
                "Validation Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        foreach (var row in Items.Where(IsItemRowFilled))
        {
            // Guardrail: Disallow items to be sold directly via Item Name if Allowed = 0 in ItemCreation
            if (string.IsNullOrWhiteSpace(row.Barcode) && !string.IsNullOrWhiteSpace(row.ItemName))
            {
                var matchItem = _allItems.FirstOrDefault(i => string.Equals(i.ItemName, row.ItemName, StringComparison.OrdinalIgnoreCase));
                if (matchItem != null && !matchItem.Allowed)
                {
                    ErrorMessage = $"Cannot save invoice. Item '{row.ItemName}' is restricted in Item Creation (Allowed = 0) and can only be sold via Barcode.";
                    System.Windows.MessageBox.Show(
                        $"Cannot save invoice.\n\nItem '{row.ItemName}' is restricted in Item Creation (Allowed = 0) and cannot be sold directly via Item Name.\n\nPlease scan its Barcode or remove this item before saving.",
                        "Item Restricted (Barcode Only)",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }

            if (!IsItemHeadAllowed(row.ItemName))
            {
                var targetHead = GetTargetItemHeadForVouType();
                ErrorMessage = $"Item '{row.ItemName}' is not allowed in voucher type '{VouType}' (Allowed ItemHead: {targetHead}).";
                System.Windows.MessageBox.Show(
                    $"Cannot save invoice.\n\nItem '{row.ItemName}' has a HeadGroup that does not match the voucher type '{VouType}' allowed ItemHead '{targetHead}'.\n\nPlease remove or change this item before saving.",
                    "Item HeadGroup Mismatch",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(row.Barcode) && row.OriginalNtWt > 0)
            {
                var diff = Math.Abs(row.NtWt - row.OriginalNtWt);
                if (diff > (_allowNtWtModUpto + 0.0001m))
                {
                    var minAllowed = Math.Max(0m, row.OriginalNtWt - _allowNtWtModUpto);
                    var maxAllowed = row.OriginalNtWt + _allowNtWtModUpto;
                    ErrorMessage = $"Cannot save invoice. Barcode '{row.Barcode}' Net Weight ({row.NtWt:N3}g) exceeds allowed modification limit of ±{_allowNtWtModUpto:N3}g from original weight ({row.OriginalNtWt:N3}g). Allowed range: {minAllowed:N3}g to {maxAllowed:N3}g.";
                    System.Windows.MessageBox.Show(
                        $"Cannot save invoice.\n\nBarcode '{row.Barcode}' Net Weight ({row.NtWt:N3}g) exceeds allowed modification limit of ±{_allowNtWtModUpto:N3}g from original weight ({row.OriginalNtWt:N3}g).\n\nAllowed Range: {minAllowed:N3}g to {maxAllowed:N3}g.\n\nPlease correct the Net Weight before saving.",
                        "Net Weight Limit Exceeded",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }
        }

        var result = System.Windows.MessageBox.Show(
            "Do you want to Save?",
            "Confirmation",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.Yes);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Saving invoice...";

            // For new bills, regenerate the latest next VouNo to avoid duplicate key conflicts in multi-session billing
            if (!_isExistingInvoice && !string.IsNullOrWhiteSpace(VouType))
            {
                var freshVouNo = await _billRepository.GetNextVouNoAsync(VouType);
                if (!string.IsNullOrWhiteSpace(freshVouNo))
                {
                    VouNo = freshVouNo;
                }
            }

            // Clean up any empty / unfilled rows from Items before saving
            if (Items.Count > 1)
            {
                var emptyRows = Items.Where(i => !IsItemRowFilled(i)).ToList();
                foreach (var emptyRow in emptyRows)
                {
                    Items.Remove(emptyRow);
                }
            }

            var filledItems = Items.Where(IsItemRowFilled).ToList();
            var totalTaxable = filledItems.Sum(i => i.AbeforeTax > 0 ? i.AbeforeTax : (i.Amount > 0 ? Math.Round(i.Amount / (1m + ((i.Tax > 0 ? i.Tax : 3m) / 100m)), 2) : 0m));
            var totalVatAmt = filledItems.Sum(i => i.VatAmt > 0 ? i.VatAmt : (i.Amount > 0 ? Math.Round(i.Amount - (i.Amount / (1m + ((i.Tax > 0 ? i.Tax : 3m) / 100m))), 2) : 0m));

            if (totalVatAmt == 0 && NetAmt > 0)
            {
                totalTaxable = Math.Round(NetAmt / 1.03m, 2);
                totalVatAmt = NetAmt - totalTaxable;
            }

            var bill = new Bill
            {
                VouNo = VouNo,
                VouType = VouType,
                BillDate = BillDate,
                DueDate = DueDate,
                Phone = Phone,
                CustomerName = CustomerName,
                CustomerCode = CustomerCode,
                Address = Address,
                Address2 = Address2,
                City = City,
                Pincode = Pincode,
                Email = Email,
                VatNo = GstinPan,
                Check = Check,
                GrossAmt = GrossAmt,
                DiscountAllowed = Discount,
                NetAmt = NetAmt,
                ABeforeTax = totalTaxable,
                Tax = 0.00m,
                TaxAmt = totalVatAmt,
                Q1 = Q1,
                Q2 = Q2,
                Q3 = Q3,
                Remarks = Remarks,
                EntryUser = _currentUser,
                EntryDate = DateTime.Today,
                EntryTime = DateTime.Now,
                ComputerName = WorkstationName,
                Status = "Saved"
            };

            var items = filledItems
                .Select(i =>
                {
                    decimal taxRate = i.Tax > 0 ? i.Tax : 3.00m;
                    decimal taxable = i.AbeforeTax > 0 ? i.AbeforeTax : (i.Amount > 0 ? Math.Round(i.Amount / (1m + (taxRate / 100m)), 2) : 0m);
                    decimal vatAmt = i.VatAmt > 0 ? i.VatAmt : (i.Amount > 0 ? Math.Round(i.Amount - taxable, 2) : 0m);

                    return new BillSubform
                    {
                        VouNo = VouNo,
                        Barcode = i.Barcode,
                        PcRate = i.PcRate,
                        ItemName = i.ItemName,
                        ENote = i.ENote,
                        Qty = i.Qty,
                        NtWt = i.NtWt,
                        Rate = i.PcRate ? 0.00m : i.Rate,
                        Amount = i.Amount,
                        GrossAmt = i.GrossAmt,
                        DisPer = i.DisPer,
                        DisAmt = i.DisAmt,
                        GrWt = i.GrWt,
                        MperGr = i.PcRate ? 0.00m : i.MperGr,
                        Making = i.PcRate ? 0.00m : i.Making,
                        MetalValue = i.PcRate ? 0.00m : i.MetalValue,
                        SalesMan = i.SalesMan,
                        VatPercent = taxRate,
                        VatAmt = vatAmt,
                        Taxable = taxable,
                        StoneValue = i.StoneValue,
                        OldBarcode = i.OldBarcode,
                        Purity = i.Purity
                    };
                }).ToList();

            var payments = Payments
                .Where(p => !string.IsNullOrWhiteSpace(p.AccountName) || p.Amount > 0)
                .Select(p => new PaymentSubform
                {
                    VouNo = VouNo,
                    AccountName = p.AccountName,
                    Amount = p.Amount,
                    HeadCategory = p.HeadCategory,
                    Narration = !string.IsNullOrWhiteSpace(p.Narration) ? p.Narration : p.AdvanceType,
                    ItemType = p.ItemType,
                    ItemName = !string.IsNullOrWhiteSpace(p.ItemName) ? p.ItemName : "OLD",
                    Wt = p.Wt,
                    LessWt = p.LessWt,
                    GrWt = p.GrWt,
                    Alloy = p.Alloy,
                    NtWt = p.NtWt,
                    Rate = p.Rate
                }).ToList();

            var existing = await _billRepository.GetByVouNoAsync(VouNo);
            if (existing is null)
            {
                await _billRepository.AddAsync(bill, items, payments);
            }
            else
            {
                IReadOnlyList<BillSubform>? origItems = null;
                IReadOnlyList<PaymentSubform>? origPayments = null;
                if (_auditRepository != null)
                {
                    try
                    {
                        origItems = await _billRepository.GetSubformItemsAsync(VouNo);
                        origPayments = await _billRepository.GetPaymentItemsAsync(VouNo);
                    }
                    catch
                    {
                        // Ignore snapshot retrieval error
                    }
                }

                await _billRepository.UpdateAsync(bill, items, payments);

                if (_auditRepository != null)
                {
                    try
                    {
                        await Infrastructure.Services.AuditService.LogInvoiceModificationAsync(
                            _auditRepository,
                            existing,
                            origItems?.ToList(),
                            origPayments?.ToList(),
                            bill,
                            items,
                            payments,
                            _currentUser);
                    }
                    catch
                    {
                        // Non-blocking audit logging
                    }
                }
            }

            _isExistingInvoice = true;
            RaiseSessionTitleChanged();

            if (_couponEngineService != null)
            {
                if (AppliedCoupon != null && CouponDiscountAmount > 0)
                {
                    try
                    {
                        await _couponEngineService.RedeemCouponForBillAsync(
                            AppliedCoupon.CouponCode,
                            VouNo,
                            Phone ?? string.Empty,
                            CustomerName ?? string.Empty,
                            GrossAmt,
                            CouponDiscountAmount,
                            NetAmt,
                            string.Empty,
                            StoreName ?? string.Empty,
                            _currentUser);
                    }
                    catch { }
                }

                if (NetAmt >= 1500m && !string.IsNullOrWhiteSpace(Phone))
                {
                    try
                    {
                        var bb = await _couponEngineService.ProcessBounceBackVoucherOnBillAsync(
                            VouNo,
                            Phone,
                            CustomerName ?? string.Empty,
                            NetAmt,
                            _currentUser);
                        if (bb != null)
                        {
                            ShowToast($"Next-visit coupon {bb.CouponCode} ({bb.FormattedDiscount}) issued!", ToastType.Success);
                        }
                    }
                    catch { }
                }
            }

            // Auto-create customer in Customer table if mobile number does not exist
            if (_customerRepository != null && !string.IsNullOrWhiteSpace(Phone))
            {
                try
                {
                    var mobile = Phone.Trim();
                    var existingCust = await _customerRepository.FindByPhoneOrMobileAsync(mobile);
                    if (existingCust is null)
                    {
                        existingCust = await _customerRepository.FindByCodeAsync(mobile);
                    }

                    if (existingCust is null)
                    {
                        var now = DateTime.Now;
                        var newCustomer = new Customer
                        {
                            CompanyName = !string.IsNullOrWhiteSpace(CustomerName) ? CustomerName.Trim() : mobile,
                            Code = mobile,
                            Mobile = mobile,
                            Phone = mobile,
                            Address = Address?.Trim() ?? string.Empty,
                            Address2 = Address2?.Trim() ?? string.Empty,
                            City = City?.Trim() ?? string.Empty,
                            Pincode = Pincode?.Trim() ?? string.Empty,
                            Date = DateTime.Today,
                            Time = now.TimeOfDay,
                            Status = "Saved",
                            Saved = "Yes",
                            User = _currentUser
                        };

                        await _customerRepository.AddAsync(newCustomer);
                    }
                }
                catch
                {
                    // Non-blocking for invoice save
                }
            }

            var savedVouNo = VouNo;
            StatusMessage = $"Invoice #{savedVouNo} saved successfully!";
            BarcodeEvents.NotifyBarcodesChanged();
            IsEditing = false;

            // Remove any blank trailing item/payment rows from the view so it cleanly shows saved data
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (!IsItemRowFilled(Items[i]))
                {
                    Items.RemoveAt(i);
                }
            }

            for (int i = Payments.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(Payments[i].AccountName) && Payments[i].Amount == 0)
                {
                    Payments.RemoveAt(i);
                }
            }

            RecalculateTotals();
            EnsureDefaultRows();

            var list = await Task.Run(() => _billRepository.GetAllAsync());
            Bills.ReplaceAll(list);

            await LoadAvailableStockBarcodesAsync();

            var currentSaved = Bills.FirstOrDefault(b => string.Equals(b.VouNo, savedVouNo, StringComparison.OrdinalIgnoreCase));
            if (currentSaved != null)
            {
                _selectedBill = currentSaved;
                OnPropertyChanged(nameof(SelectedBill));
            }

            // Auto-Send WhatsApp if enabled / ticked for this invoice save
            if (SendWhatsAppOnSave && !string.IsNullOrWhiteSpace(Phone))
            {
                _ = Task.Run(async () => await SendWhatsAppAsync(isManual: false));
            }

            // Prompt: "Do you want to Print YES / NO"
            var promptResult = System.Windows.MessageBox.Show(
                $"Invoice #{savedVouNo} saved successfully.\n\nDo you want to print?",
                "Print Tax Invoice",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.Yes);

            if (promptResult == System.Windows.MessageBoxResult.Yes)
            {
                var showPreview = true;
                try { showPreview = new LocalPrinterSettingsStore().Load().BillPrintPreviewBeforePrint; } catch { }

                if (showPreview)
                {
                    Print();
                }
                else
                {
                    DirectPrintToBillPrinter();
                }
            }
        }
        catch (Exception ex)
        {
            _errorMessage = "Error saving invoice: " + ex.Message;
            OnPropertyChanged(nameof(ErrorMessage));

            System.Windows.MessageBox.Show(
                $"Failed to save invoice #{VouNo}.\n\nError details:\n{ex.Message}",
                "Save Failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ErrorMessage = "Select an invoice to delete.";
            return;
        }

        var vouNoToDelete = VouNo;
        try
        {
            IsBusy = true;
            Bill? billToDelete = null;
            IReadOnlyList<BillSubform>? itemsToDelete = null;
            IReadOnlyList<PaymentSubform>? paymentsToDelete = null;

            if (_auditRepository != null)
            {
                try
                {
                    billToDelete = await _billRepository.GetByVouNoAsync(vouNoToDelete);
                    itemsToDelete = await _billRepository.GetSubformItemsAsync(vouNoToDelete);
                    paymentsToDelete = await _billRepository.GetPaymentItemsAsync(vouNoToDelete);
                }
                catch
                {
                    // Ignore snapshot retrieval error
                }
            }

            await _billRepository.DeleteAsync(vouNoToDelete);

            if (_auditRepository != null && billToDelete != null)
            {
                try
                {
                    await Infrastructure.Services.AuditService.LogInvoiceDeletionAsync(
                        _auditRepository,
                        billToDelete,
                        itemsToDelete?.ToList(),
                        paymentsToDelete?.ToList(),
                        _currentUser);
                }
                catch
                {
                    // Non-blocking audit logging
                }
            }

            StatusMessage = $"Invoice #{vouNoToDelete} deleted.";
            if (_couponEngineService != null)
            {
                try
                {
                    await _couponEngineService.RevertCouponForBillAsync(vouNoToDelete, "Bill Deleted");
                }
                catch { }
            }
            BarcodeEvents.NotifyBarcodesChanged();
            await AddNewAsync();

            var list = await Task.Run(() => _billRepository.GetAllAsync());
            Bills.ReplaceAll(list);

            await LoadAvailableStockBarcodesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error deleting invoice: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Cancel()
    {
        ClearForm();
        IsEditing = false;
        SendWhatsAppOnSave = _waAutoSend;
        StatusMessage = "Operation cancelled.";
        ErrorMessage = string.Empty;
    }

    private void Print()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ErrorMessage = "Please select or create an invoice to print.";
            return;
        }

        var showPreview = true;
        try { showPreview = new LocalPrinterSettingsStore().Load().BillPrintPreviewBeforePrint; } catch { }

        if (!showPreview)
        {
            DirectPrintToBillPrinter();
            return;
        }

        try
        {
            if (FastReportDocumentService.HasBillFrxTemplate(this))
            {
                var prepared = FastReportDocumentService.PrepareBillReport(this);
                var preview = new Views.FastReportPreviewWindow(prepared);
                preview.SetOwnerSafely();
                preview.ShowDialog();
            }
            else
            {
                var doc = InvoiceFlowDocumentBuilder.CreateInvoiceDocument(this);
                var preview = new Views.InvoicePrintPreviewWindow(doc);
                preview.SetOwnerSafely();
                preview.ShowDialog();
            }
            StatusMessage = $"Print preview displayed for invoice #{VouNo}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error generating print document: " + ex.Message;
        }
    }

    public async Task SendWhatsAppAsync(bool isManual = true)
    {
        if (string.IsNullOrWhiteSpace(Phone))
        {
            if (isManual)
            {
                ShowToast("Customer phone number is missing. Please enter phone number to send WhatsApp.", ToastType.Warning);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(VouNo))
        {
            if (isManual)
            {
                ShowToast("No invoice is currently loaded.", ToastType.Warning);
            }
            return;
        }

        IsBusy = true;
        try
        {
            // Look for existing invoice PDF in SavePath
            string? existingPdf = null;
            try
            {
                string? saveDir = null;
                if (_settingsRepository != null)
                {
                    var settings = await _settingsRepository.GetAllAsync();
                    if (settings.TryGetValue(AppSetting.SavePathKey, out var dir) && !string.IsNullOrWhiteSpace(dir))
                    {
                        saveDir = dir.Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(saveDir))
                {
                    saveDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ConceptJewellery", "Exports");
                }

                if (!string.IsNullOrWhiteSpace(saveDir))
                {
                    try
                    {
                        existingPdf = Services.InvoicePdfGenerator.SaveInvoicePdf(this, saveDir);
                    }
                    catch
                    {
                        // Fallback: check if an existing PDF exists in export directory
                        if (System.IO.Directory.Exists(saveDir))
                        {
                            var cleanVou = VouNo.Trim();
                            var matches = System.IO.Directory.GetFiles(saveDir, $"*{cleanVou}*.pdf");
                            if (matches.Length > 0)
                            {
                                existingPdf = matches[0];
                            }
                        }
                    }
                }
            }
            catch
            {
                // Non-fatal if export directory is inaccessible
            }

            byte[]? pdfBytes = null;
            if (string.IsNullOrWhiteSpace(existingPdf) || !System.IO.File.Exists(existingPdf))
            {
                try
                {
                    pdfBytes = Services.InvoicePdfGenerator.GenerateInvoicePdfBytes(this);
                }
                catch
                {
                    // Non-fatal
                }
            }

            var req = new WhatsAppMessageRequest
            {
                RecipientPhone = Phone.Trim(),
                CustomerName = !string.IsNullOrWhiteSpace(CustomerName) ? CustomerName.Trim() : "Valued Customer",
                InvoiceNo = VouNo.Trim(),
                BillDate = BillDate,
                NetAmount = NetAmt,
                Balance = BalanceAmt,
                StoreName = !string.IsNullOrWhiteSpace(StoreName) ? StoreName : "Concept Jewellery",
                PdfFilePath = existingPdf,
                PdfBytes = pdfBytes,
                PdfFileName = $"{VouNo.Trim()}.pdf"
            };

            var result = await _whatsAppService.SendInvoiceMessageAsync(req);
            if (result.Success)
            {
                WhatsAppDeliveryStatus = "Sent";
                if (!string.IsNullOrWhiteSpace(result.ExternalId))
                {
                    WhatsAppMessageId = result.ExternalId;
                }
                ShowToast(result.Message, ToastType.Success);
            }
            else
            {
                if (isManual)
                {
                    ShowToast(result.Message, ToastType.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            if (isManual)
            {
                ShowToast($"WhatsApp dispatch error: {ex.Message}", ToastType.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task GenerateEInvoiceAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("Please save or select an invoice first before generating E-Invoice.", ToastType.Warning);
            return;
        }

        var validItems = Items.Where(i => !string.IsNullOrWhiteSpace(i.ItemName) || i.Amount > 0 || i.GrWt > 0).ToList();
        if (validItems.Count == 0)
        {
            ShowToast("Invoice must have at least one line item to generate E-Invoice.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        StatusMessage = "Generating E-Invoice...";
        try
        {
            var reqItems = validItems.Select((it, idx) =>
            {
                var taxable = it.Amount > 0 ? it.Amount : it.GrossAmt;
                var gstAmt = it.VatAmt;
                var cgst = Math.Round(gstAmt / 2m, 2);
                var sgst = Math.Round(gstAmt / 2m, 2);

                var hsn = !string.IsNullOrWhiteSpace(it.HsnCode) ? it.HsnCode.Trim() : GetHsnForItem(it.ItemName);
                return new EInvoiceItemDto
                {
                    SlNo = idx + 1,
                    ItemName = !string.IsNullOrWhiteSpace(it.ItemName) ? it.ItemName : "Jewellery Item",
                    HsnCode = hsn,
                    Qty = it.Qty > 0 ? it.Qty : 1,
                    Unit = "GMS",
                    Rate = it.Rate > 0 ? it.Rate : taxable,
                    TaxableAmount = taxable,
                    GstRate = it.Tax,
                    CgstAmt = cgst,
                    SgstAmt = sgst,
                    IgstAmt = 0m,
                    TotalAmt = taxable + gstAmt
                };
            }).ToList();

            var req = new EInvoiceRequestDto
            {
                VouNo = VouNo.Trim(),
                BillDate = BillDate,
                BuyerName = !string.IsNullOrWhiteSpace(CustomerName) ? CustomerName.Trim() : "Customer",
                BuyerGstin = !string.IsNullOrWhiteSpace(GstinPan) ? GstinPan.Trim() : string.Empty,
                BuyerAddress = Address?.Trim() ?? string.Empty,
                BuyerCity = !string.IsNullOrWhiteSpace(City) ? City.Trim() : "CUTTACK",
                BuyerPincode = !string.IsNullOrWhiteSpace(Pincode) ? Pincode.Trim() : "753001",
                GrossAmount = GrossAmt,
                Discount = Discount,
                TaxableAmount = Math.Max(0m, GrossAmt - Discount),
                CgstAmount = CgstAmt,
                SgstAmount = SgstAmt,
                IgstAmount = 0m,
                NetAmount = NetAmt,
                Items = reqItems
            };

            var result = await _eInvoiceService.GenerateEInvoiceAsync(req);
            if (result.Success)
            {
                ShowToast(result.Message, ToastType.Success);
                System.Windows.MessageBox.Show(
                    result.Message,
                    $"E-Invoice Generated [{result.Mode}]",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                ShowToast(result.Message, ToastType.Error);
                System.Windows.MessageBox.Show(
                    result.Message,
                    $"E-Invoice Generation Failed [{result.Mode}]",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"E-Invoice error: {ex.Message}", ToastType.Error);
            System.Windows.MessageBox.Show(
                $"Failed to generate E-Invoice: {ex.Message}",
                "E-Invoice Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = string.Empty;
        ToastType = type;
        ToastMessage = message;
    }

    private void SaveLayout()
    {
        RequestSaveLayout?.Invoke(this, EventArgs.Empty);
    }

    private void BillSettings()
    {
        if (_isOpeningBillSettingsDialog) return;
        _isOpeningBillSettingsDialog = true;
        try
        {
            var dlg = new BillSettingsWindow(_settingsRepository);
            dlg.SetOwnerSafely();

            if (dlg.ShowDialog() == true)
            {
                _ = LoadSettingsAsync();
                StatusMessage = "Bill settings saved successfully.";
                ToastType = ToastType.Success;
                ToastMessage = string.Empty;
                ToastMessage = "Bill settings saved successfully.";
            }
        }
        finally
        {
            _isOpeningBillSettingsDialog = false;
        }
    }

    private void ImageAIBill()
    {
        if (_isOpeningImageAIBillDialog) return;
        _isOpeningImageAIBillDialog = true;
        try
        {
            var dlg = new ImageAIBillWindow();
            dlg.SetOwnerSafely();

            if (dlg.ShowDialog() == true && dlg.ExtractedBillResult != null)
            {
                ApplyExtractedBill(dlg.ExtractedBillResult);
            }
        }
        finally
        {
            _isOpeningImageAIBillDialog = false;
        }
    }

    private async Task OpenListAsync()
    {
        try
        {
            var listVm = new InvoiceListViewModel(_billRepository, _currentUser, _whatsAppService, _settingsRepository, _whatsAppBillTrackerRepository);
            var win = new InvoiceListWindow(listVm);
            win.SetOwnerSafely();

            if (win.ShowDialog() == true && win.SelectedInvoice != null)
            {
                var selectedVouNo = win.SelectedInvoice.VouNo;
                if (!string.IsNullOrWhiteSpace(selectedVouNo))
                {
                    await LoadBillAsync(selectedVouNo);

                    var currentSaved = Bills.FirstOrDefault(b => string.Equals(b.VouNo, selectedVouNo, StringComparison.OrdinalIgnoreCase));
                    if (currentSaved != null)
                    {
                        _selectedBill = currentSaved;
                        OnPropertyChanged(nameof(SelectedBill));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error opening invoice list: " + ex.Message;
        }
    }

    public void ApplyExtractedBill(ExtractedBillResult result)
    {
        if (IsNotEditing)
        {
            if (AddNewCommand.CanExecute(null))
            {
                _ = AddNewAsync();
            }
        }

        if (!string.IsNullOrWhiteSpace(result.CustomerName))
            CustomerName = result.CustomerName;
        if (!string.IsNullOrWhiteSpace(result.Phone))
            Phone = result.Phone;
        if (!string.IsNullOrWhiteSpace(result.Address))
            Address = result.Address;
        if (result.BillDate.HasValue)
            BillDate = result.BillDate.Value;
        if (!string.IsNullOrWhiteSpace(result.VouType))
            VouType = result.VouType;
        if (!string.IsNullOrWhiteSpace(result.Remarks))
            Remarks = result.Remarks;

        if (result.Items != null && result.Items.Count > 0)
        {
            Items.Clear();
            foreach (var item in result.Items)
            {
                var subItem = new BillSubformItemViewModel
                {
                    Barcode = item.Barcode,
                    ItemName = item.ItemName,
                    Qty = item.Qty,
                    PcRate = item.PcRate,
                    GrWt = item.GrWt,
                    NtWt = item.NtWt,
                    OriginalNtWt = item.NtWt,
                    Rate = item.Rate,
                    MetalValue = item.MetalValue,
                    MperGr = item.MperGr,
                    Making = item.Making,
                    StoneValue = item.StoneValue,
                    GrossAmt = item.GrossAmt,
                    DisPer = item.DisPer,
                    DisAmt = item.DisAmt,
                    Amount = item.Amount,
                    Tax = item.Tax,
                    SalesMan = item.SalesMan,
                    OldBarcode = item.OldBarcode,
                    ENote = item.ENote
                };
                Items.Add(subItem);
            }
        }

        if (result.Payments != null && result.Payments.Count > 0)
        {
            Payments.Clear();
            foreach (var pay in result.Payments)
            {
                Payments.Add(new PaymentSubformItemViewModel
                {
                    AccountName = pay.AccountName,
                    Amount = pay.Amount
                });
            }
        }

        RecalculateTotals();
        StatusMessage = $"Successfully imported {result.Items?.Count ?? 0} item(s) from AI Bill Image.";
        ToastType = ToastType.Success;
        ToastMessage = string.Empty;
        ToastMessage = $"Imported {result.Items?.Count ?? 0} item(s) from AI Bill.";
    }

    public void DirectPrintToBillPrinter()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ErrorMessage = "Please select or save an invoice before printing.";
            return;
        }

        try
        {
            var printerStore = new LocalPrinterSettingsStore();
            var printerSettings = printerStore.Load();
            var targetPrinterName = printerSettings.DefaultBillPrinter;

            if (FastReportDocumentService.HasBillFrxTemplate(this))
            {
                var prepared = FastReportDocumentService.PrepareBillReport(this);
                ConceptJewelleryPro.Infrastructure.Printing.FastReportPrintService.PrintPreparedDocument(prepared, targetPrinterName);
                StatusMessage = $"Invoice #{VouNo} sent directly to printer '{(!string.IsNullOrWhiteSpace(targetPrinterName) ? targetPrinterName : "Default")}'.";
                return;
            }

            var doc = InvoiceFlowDocumentBuilder.CreateInvoiceDocument(this);
            var printDialog = new System.Windows.Controls.PrintDialog();

            if (!string.IsNullOrWhiteSpace(targetPrinterName))
            {
                try
                {
                    using var printServer = new System.Printing.LocalPrintServer();
                    var queue = printServer.GetPrintQueues(new[]
                    {
                        System.Printing.EnumeratedPrintQueueTypes.Local,
                        System.Printing.EnumeratedPrintQueueTypes.Connections
                    }).FirstOrDefault(q => string.Equals(q.FullName, targetPrinterName, StringComparison.OrdinalIgnoreCase));

                    if (queue != null)
                    {
                        printDialog.PrintQueue = queue;
                    }
                }
                catch
                {
                    // Fall back to default printer if specific queue lookup fails
                }
            }

            if (doc.PageWidth > doc.PageHeight && printDialog.PrintTicket != null)
            {
                printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
            }

            doc.PageWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : doc.PageWidth;
            doc.PageHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : doc.PageHeight;
            doc.ColumnWidth = doc.PageWidth;

            System.Windows.Documents.IDocumentPaginatorSource idp = doc;
            printDialog.PrintDocument(idp.DocumentPaginator, $"Tax Invoice #{VouNo}");

            StatusMessage = $"Invoice #{VouNo} sent directly to printer '{printDialog.PrintQueue?.FullName ?? "Default"}'.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Direct print error: " + ex.Message;
            var res = System.Windows.MessageBox.Show(
                $"Failed to send print command directly to printer:\n{ex.Message}\n\nWould you like to open Print Preview instead?",
                "Print Error",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (res == System.Windows.MessageBoxResult.Yes)
            {
                Print();
            }
        }
    }

    public async Task LoadBillAsync(string vouNo)
    {
        try
        {
            IsBusy = true;
            _isLoadingBill = true;
            var bill = await Task.Run(() => _billRepository.GetByVouNoAsync(vouNo));
            if (bill is null)
            {
                ErrorMessage = "Invoice not found";
                return;
            }

            VouNo = bill.VouNo;
            VouType = bill.VouType;
            BillDate = bill.BillDate;
            DueDate = bill.DueDate;
            Phone = bill.Phone;
            CustomerName = bill.CustomerName;
            CustomerCode = bill.CustomerCode;
            _ = LoadCustomerHistoryAndMetricsAsync(bill.Phone, bill.CustomerName, bill.CustomerCode);
            Address = bill.Address;
            Address2 = bill.Address2;
            City = bill.City;
            Pincode = bill.Pincode;
            Email = bill.Email;
            GstinPan = bill.VatNo;
            Check = bill.Check;
            Discount = bill.DiscountAllowed;
            Q1 = bill.Q1;
            Q2 = bill.Q2;
            Q3 = bill.Q3;
            Remarks = bill.Remarks;

            var subItemsTask = Task.Run(() => _billRepository.GetSubformItemsAsync(vouNo));
            var payItemsTask = Task.Run(() => _billRepository.GetPaymentItemsAsync(vouNo));
            await Task.WhenAll(subItemsTask, payItemsTask);

            var subItems = await subItemsTask;
            var newItems = new List<BillSubformItemViewModel>(subItems.Count);
            foreach (var sub in subItems)
            {
                var match = _allItems.FirstOrDefault(i => string.Equals(i.ItemName?.Trim(), sub.ItemName?.Trim(), StringComparison.OrdinalIgnoreCase));
                decimal taxRate = sub.VatPercent > 0
                    ? sub.VatPercent
                    : (sub.VatAmt > 0 && sub.VatAmt <= 28m ? sub.VatAmt : (match?.Tax > 0 ? match.Tax : 3.00m));

                decimal itemTaxable = sub.Taxable > 0
                    ? sub.Taxable
                    : (sub.Amount > 0 ? Math.Round(sub.Amount / (1m + (taxRate / 100m)), 2) : 0m);

                decimal itemVatAmt = (sub.VatAmt > 28m || (sub.Amount > 0 && Math.Abs(sub.VatAmt - (sub.Amount - itemTaxable)) <= 1.0m))
                    ? sub.VatAmt
                    : (sub.Amount > 0 ? Math.Round(sub.Amount - itemTaxable, 2) : 0m);

                var itemVm = new BillSubformItemViewModel
                {
                    Barcode = sub.Barcode,
                    PcRate = sub.PcRate,
                    ItemName = sub.ItemName,
                    HsnCode = !string.IsNullOrWhiteSpace(match?.HSNCode) ? match.HSNCode.Trim() : "7113",
                    ENote = sub.ENote,
                    Qty = sub.Qty,
                    NtWt = sub.NtWt,
                    OriginalNtWt = sub.NtWt,
                    Rate = sub.Rate,
                    Amount = sub.Amount,
                    GrossAmt = sub.GrossAmt,
                    DisPer = sub.DisPer,
                    DisAmt = sub.DisAmt,
                    GrWt = sub.GrWt,
                    MperGr = sub.MperGr,
                    Making = sub.Making,
                    MetalValue = sub.MetalValue,
                    SalesMan = sub.SalesMan,
                    Tax = taxRate,
                    AbeforeTax = itemTaxable,
                    VatAmt = itemVatAmt,
                    StoneValue = sub.StoneValue,
                    Purity = sub.Purity,
                    OldBarcode = sub.OldBarcode
                };
                newItems.Add(itemVm);
            }
            Items.ReplaceAll(newItems);

            var payItems = await payItemsTask;
            var newPayments = new List<PaymentSubformItemViewModel>(payItems.Count);
            foreach (var pay in payItems)
            {
                var payVm = new PaymentSubformItemViewModel
                {
                    AccountName = pay.AccountName,
                    Amount = pay.Amount,
                    HeadCategory = pay.HeadCategory,
                    Narration = pay.Narration,
                    AdvanceType = pay.Narration,
                    ItemType = pay.ItemType,
                    ItemName = pay.ItemName,
                    Wt = pay.Wt,
                    LessWt = pay.LessWt,
                    GrWt = pay.GrWt,
                    Alloy = pay.Alloy,
                    NtWt = pay.NtWt,
                    Rate = pay.Rate
                };
                newPayments.Add(payVm);
            }
            Payments.ReplaceAll(newPayments);

            EnsureDefaultRows();
            RecalculateTotals();
            IsEditing = false;
            _isExistingInvoice = true;
            SendWhatsAppOnSave = false;

            if (_whatsAppBillTrackerRepository != null)
            {
                try
                {
                    var tracker = await _whatsAppBillTrackerRepository.GetByInvoiceNoAsync(vouNo);
                    if (tracker != null)
                    {
                        WhatsAppMessageId = tracker.Wamid;
                        WhatsAppDeliveryStatus = !string.IsNullOrWhiteSpace(tracker.DeliveryStatus) ? tracker.DeliveryStatus : tracker.Rating;
                        WhatsAppReply = tracker.Reply;
                    }
                    else
                    {
                        WhatsAppMessageId = null;
                        WhatsAppDeliveryStatus = null;
                        WhatsAppReply = null;
                    }
                }
                catch
                {
                    // Non-fatal if tracker lookup fails
                }
            }

            RaiseSessionTitleChanged();
            StatusMessage = $"Loaded Invoice #{vouNo}";
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load invoice: " + ex.Message;
            EnsureDefaultRows();
        }
        finally
        {
            _isLoadingBill = false;
            IsBusy = false;
        }
    }

    public void EnsureDefaultRows()
    {
        if (Items.Count == 0)
        {
            AddItemRow();
        }
        if (Payments.Count == 0)
        {
            AddPaymentRow();
        }
    }

    public void AddItemRow()
    {
        var item = new BillSubformItemViewModel
        {
            Qty = 1,
            NtWt = 0.000m,
            Rate = 0.00m,
            Amount = 0.00m,
            GrossAmt = 0.00m,
            Tax = 3.00m
        };
        Items.Add(item);
    }

    public void EnsureTrailingEmptyItemRow()
    {
        if (_isLoadingBill || !IsEditing) return;
        var last = Items.LastOrDefault();
        if (last == null || IsItemRowFilled(last))
        {
            AddItemRow();
        }
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not BillSubformItemViewModel item) return;

        if (!item.IsRecalculating && IsFinancialItemProperty(e.PropertyName))
        {
            RecalculateTotals();
        }

        if (e.PropertyName == nameof(BillSubformItemViewModel.Barcode))
        {
            if (!string.IsNullOrWhiteSpace(item.Barcode) && !_isLoadingBill)
            {
                EnsureTrailingEmptyItemRow();
            }
        }
        else if (e.PropertyName == nameof(BillSubformItemViewModel.ItemName))
        {
            if (!string.IsNullOrWhiteSpace(item.ItemName) && !_isLoadingBill)
            {
                OnItemNameSelected(item, item.ItemName);
                EnsureTrailingEmptyItemRow();
            }
        }
        else if (e.PropertyName == nameof(BillSubformItemViewModel.Purity))
        {
            if (!string.IsNullOrWhiteSpace(item.Purity) && !_isLoadingBill && !item.PcRate)
            {
                var purityMatch = FindMatchingPurity(item.Purity);
                if (purityMatch != null && purityMatch.RatePerGram > 0)
                {
                    item.Rate = purityMatch.RatePerGram;
                }
            }
        }
        else if (e.PropertyName == nameof(BillSubformItemViewModel.NtWt))
        {
            if (!string.IsNullOrWhiteSpace(item.Barcode) && item.OriginalNtWt > 0 && !_isLoadingBill)
            {
                var diff = Math.Abs(item.NtWt - item.OriginalNtWt);
                if (diff > (_allowNtWtModUpto + 0.0001m))
                {
                    var minAllowed = Math.Max(0m, item.OriginalNtWt - _allowNtWtModUpto);
                    var maxAllowed = item.OriginalNtWt + _allowNtWtModUpto;
                    var attemptedWt = item.NtWt;
                    item.NtWt = item.OriginalNtWt;
                    ErrorMessage = $"Barcode '{item.Barcode}' Net Weight ({attemptedWt:N3}g) exceeds allowed modification limit of ±{_allowNtWtModUpto:N3}g from original weight ({item.OriginalNtWt:N3}g). Allowed range: {minAllowed:N3}g to {maxAllowed:N3}g.";
                    System.Windows.MessageBox.Show(
                        $"Net Weight modification exceeds allowed limit of ±{_allowNtWtModUpto:N3}g for barcode '{item.Barcode}'.\n\nOriginal Net Weight: {item.OriginalNtWt:N3}g\nAllowed Range: {minAllowed:N3}g to {maxAllowed:N3}g\nAttempted Weight: {attemptedWt:N3}g\n\nReverting Net Weight back to original ({item.OriginalNtWt:N3}g).",
                        "Net Weight Modification Limit",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
        }
    }

    private static bool IsFinancialItemProperty(string? propertyName)
    {
        return propertyName is null
            or nameof(BillSubformItemViewModel.GrossAmt)
            or nameof(BillSubformItemViewModel.Amount)
            or nameof(BillSubformItemViewModel.Qty)
            or nameof(BillSubformItemViewModel.GrWt)
            or nameof(BillSubformItemViewModel.NtWt)
            or nameof(BillSubformItemViewModel.Rate)
            or nameof(BillSubformItemViewModel.MetalValue)
            or nameof(BillSubformItemViewModel.Making)
            or nameof(BillSubformItemViewModel.MperGr)
            or nameof(BillSubformItemViewModel.StoneValue)
            or nameof(BillSubformItemViewModel.DisPer)
            or nameof(BillSubformItemViewModel.DisAmt)
            or nameof(BillSubformItemViewModel.Tax)
            or nameof(BillSubformItemViewModel.PcRate);
    }

    public async Task LookupBarcodeAsync(BillSubformItemViewModel item, string barcode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            var clean = barcode.Trim();

            // 1. Fast in-memory lookup first from loaded stock items (zero DB latency)
            BarcodeDetails? details = null;
            var cachedStock = _availableStockItems.FirstOrDefault(s => string.Equals(s.Barcode, clean, StringComparison.OrdinalIgnoreCase));
            if (cachedStock != null)
            {
                details = new BarcodeDetails
                {
                    Barcode = cachedStock.Barcode,
                    ItemName = cachedStock.ItemName ?? string.Empty,
                    GrossWt = cachedStock.GrossWt,
                    NetWt = cachedStock.NetWt,
                    Qty = cachedStock.Qty > 0 ? (int)cachedStock.Qty : 1,
                    MPerGr = cachedStock.MPerGr,
                    Making = cachedStock.Making,
                    StoneValue = cachedStock.StoneValue,
                    Description = cachedStock.Description ?? string.Empty,
                    Description2 = cachedStock.Description2 ?? string.Empty,
                    Purity = cachedStock.Purity ?? string.Empty,
                    ItemCode = cachedStock.ItemCode ?? string.Empty,
                    BType = cachedStock.BType ?? string.Empty,
                    MRP = cachedStock.MRP,
                    CostRate = cachedStock.CostRate
                };
            }
            else
            {
                details = await Task.Run(() => _billRepository.GetBarcodeDetailsAsync(clean));
            }

            if (details is not null)
            {
                var match = _allItems.FirstOrDefault(i => string.Equals(i.ItemName, details.ItemName, StringComparison.OrdinalIgnoreCase));
                var headGroup = match?.HeadGroup;

                if (!IsItemHeadAllowed(details.ItemName, headGroup))
                {
                    var targetHead = GetTargetItemHeadForVouType();
                    var headGroupDesc = !string.IsNullOrWhiteSpace(headGroup) ? headGroup : "Different Group";
                    ErrorMessage = $"Item '{details.ItemName}' (HeadGroup: {headGroupDesc}) is not allowed in voucher type '{VouType}' (Allowed: {targetHead}).";
                    System.Windows.MessageBox.Show(
                        $"Barcode '{barcode}' contains item '{details.ItemName}' with HeadGroup '{headGroupDesc}'.\n\nThis item is not allowed for Voucher Type '{VouType}'.\nAllowed ItemHead: {targetHead}",
                        "Item Not Allowed in Voucher",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    item.Barcode = string.Empty;
                    item.ItemName = string.Empty;
                    return;
                }

                var finalBarcode = !string.IsNullOrWhiteSpace(details.Barcode) ? details.Barcode.Trim() : clean;
                var pcRate = string.Equals(details.BType?.Trim(), "PC", StringComparison.OrdinalIgnoreCase);
                var originalNtWt = details.NetWt > 0 ? details.NetWt : details.GrossWt;
                var ntWt = originalNtWt;
                var qty = details.Qty > 0 ? details.Qty : 1;
                var mperGr = pcRate ? 0 : details.MPerGr;
                var making = pcRate ? 0 : details.Making;
                var stoneValue = details.StoneValue;
                var eNote = details.Description ?? string.Empty;
                var oldBarcode = details.Description2 ?? string.Empty;

                // Rule 1: For barcodes, get Purity from BarcodeSubform table (details.Purity)
                var purity = !string.IsNullOrWhiteSpace(details.Purity) ? details.Purity : (match != null && match.Purity > 0 ? match.Purity.ToString("0.##") : string.Empty);

                decimal tax = 3.00m;
                decimal disPer = 0m;
                if (match is not null)
                {
                    var itemVat = match.Tax > 0 ? match.Tax : match.GSTRate;
                    tax = itemVat > 0 ? itemVat : 3.00m;
                    if (item.DisPer == 0 && match.Disper > 0) disPer = match.Disper;
                }

                // Match Purity with PurityMaster to get standard Purity name formatting
                var purityMatch = FindMatchingPurity(purity, match?.Purity);
                if (purityMatch != null && string.IsNullOrWhiteSpace(purity))
                {
                    purity = purityMatch.Purity;
                }

                decimal rate = 0m;
                if (pcRate)
                {
                    // PC items: Price directly based on MRP in BarcodeSubform, not weight-based
                    if (details.MRP > 0)
                    {
                        rate = details.MRP;
                    }
                    else if (details.CostRate > 0)
                    {
                        rate = details.CostRate;
                    }
                    else if (match is not null && match.SaleRate > 0)
                    {
                        rate = match.SaleRate;
                    }
                }
                else
                {
                    // Weight-based items: Use Purity Master rate per gram
                    if (purityMatch != null && purityMatch.RatePerGram > 0)
                    {
                        rate = purityMatch.RatePerGram;
                    }
                    else if (details.CostRate > 0 && item.Rate == 0)
                    {
                        rate = details.CostRate;
                    }
                    else if (match is not null && match.SaleRate > 0 && item.Rate == 0)
                    {
                        rate = match.SaleRate;
                    }
                }

                var hsnCode = !string.IsNullOrWhiteSpace(match?.HSNCode) ? match.HSNCode.Trim() : "7113";

                // Atomic assignment with zero intermediate notification storm
                item.ApplyBarcodeDetails(
                    barcode: finalBarcode,
                    itemName: details.ItemName,
                    pcRate: pcRate,
                    grWt: details.GrossWt,
                    ntWt: ntWt,
                    originalNtWt: originalNtWt,
                    qty: qty,
                    mperGr: mperGr,
                    making: making,
                    stoneValue: stoneValue,
                    eNote: eNote,
                    oldBarcode: oldBarcode,
                    purity: purity,
                    tax: tax,
                    rate: rate,
                    disPer: disPer,
                    hsnCode: hsnCode);

                EnsureTrailingEmptyItemRow();

                StatusMessage = $"Barcode '{barcode}' loaded: {details.ItemName} ({(pcRate ? $"{qty} Pc" : $"{ntWt:N3}g")})";
            }
            else
            {
                ErrorMessage = $"Barcode '{barcode}' not found.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error looking up barcode '{barcode}': {ex.Message}";
        }
    }

    private void OnItemNameSelected(BillSubformItemViewModel item, string itemName)
    {
        var cleanName = itemName?.Trim() ?? string.Empty;
        var match = _allItems.FirstOrDefault(i => string.Equals(i.ItemName?.Trim(), cleanName, StringComparison.OrdinalIgnoreCase));

        // Guardrail: Disallow items from being selected/sold directly by Item Name if Allowed = 0 in ItemCreation (Allowed via Barcode only)
        if (string.IsNullOrWhiteSpace(item.Barcode) && match is not null && !match.Allowed)
        {
            ErrorMessage = $"Item '{itemName}' cannot be sold via Item Name (Allowed = 0). Please scan barcode.";
            System.Windows.MessageBox.Show(
                $"Item '{itemName}' is restricted in Item Creation (Allowed = 0) and cannot be selected or sold directly via Item Name.\n\nThis item can only be sold by scanning its Barcode.",
                "Item Restricted (Barcode Only)",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            item.ItemName = string.Empty;
            return;
        }

        if (!IsItemHeadAllowed(itemName, match?.HeadGroup))
        {
            var targetHead = GetTargetItemHeadForVouType();
            var headGroupDesc = !string.IsNullOrWhiteSpace(match?.HeadGroup) ? match.HeadGroup : "Different Group";
            ErrorMessage = $"Item '{itemName}' (HeadGroup: {headGroupDesc}) is not allowed in voucher type '{VouType}' (Allowed: {targetHead}).";
            System.Windows.MessageBox.Show(
                $"Item '{itemName}' with HeadGroup '{headGroupDesc}' is not allowed for Voucher Type '{VouType}'.\n\nAllowed ItemHead: {targetHead}",
                "Item Not Allowed in Voucher",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            item.ItemName = string.Empty;
            return;
        }

        if (match is not null)
        {
            item.HsnCode = !string.IsNullOrWhiteSpace(match.HSNCode) ? match.HSNCode.Trim() : "7113";

            // Load Vat% based on itemname using ItemCreation table
            var itemVat = match.Tax > 0 ? match.Tax : match.GSTRate;
            item.Tax = itemVat > 0 ? itemVat : 3.00m;

            if (item.DisPer == 0 && match.Disper > 0) item.DisPer = match.Disper;

            // Rule 2: If ItemName typed (no barcode entered), get purity from ItemCreation table (match.RawPurity / match.Purity)
            if (string.IsNullOrWhiteSpace(item.Barcode))
            {
                var purityMatch = FindMatchingPurity(match.RawPurity, match.Purity);
                if (purityMatch != null)
                {
                    item.Purity = purityMatch.Purity;
                    if (purityMatch.RatePerGram > 0)
                    {
                        item.Rate = purityMatch.RatePerGram;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(match.RawPurity))
                {
                    item.Purity = match.RawPurity.Trim();
                }
                else if (match.Purity > 0)
                {
                    item.Purity = match.Purity.ToString("0.##");
                }
            }

            if (match.SaleRate > 0 && item.Rate == 0)
            {
                item.Rate = match.SaleRate;
            }

            item.Recalculate();
        }
        else
        {
            item.HsnCode = "7113";
            item.Tax = 3.00m;
            item.Recalculate();
        }
    }

    private PurityMaster? FindMatchingPurity(string? purityStr, decimal? numericPurity = null)
    {
        if (_allPurities.Count == 0) return null;

        var clean = purityStr?.Trim() ?? string.Empty;

        // 1. Exact match on Purity string
        if (!string.IsNullOrEmpty(clean))
        {
            var match = _allPurities.FirstOrDefault(p =>
                string.Equals(p.Purity?.Trim(), clean, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            var noSpace = clean.Replace(" ", "");
            var matchNoSpace = _allPurities.FirstOrDefault(p =>
                string.Equals(p.Purity?.Replace(" ", ""), noSpace, StringComparison.OrdinalIgnoreCase));
            if (matchNoSpace != null) return matchNoSpace;

            var cleanWithoutK = clean.TrimEnd('k', 'K').Trim();
            var kMatch = _allPurities.FirstOrDefault(p =>
                string.Equals(p.Purity?.Trim().TrimEnd('k', 'K').Trim(), cleanWithoutK, StringComparison.OrdinalIgnoreCase));
            if (kMatch != null) return kMatch;
        }

        // 2. Numeric match (e.g. 22 vs 22K, 91.6 vs 916, 75.0 vs 750, 22 vs 91.6%, etc.)
        decimal num = 0;
        if (numericPurity.HasValue && numericPurity.Value > 0)
        {
            num = numericPurity.Value;
        }
        else if (!string.IsNullOrEmpty(clean))
        {
            var digitsOnly = new string(clean.Where(c => char.IsDigit(c) || c == '.').ToArray());
            _ = decimal.TryParse(digitsOnly, out num);
        }

        if (num > 0)
        {
            foreach (var p in _allPurities)
            {
                var pClean = p.Purity?.Trim() ?? string.Empty;
                if (string.Equals(pClean, num.ToString("0.##"), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pClean, num.ToString("0"), StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }

                var pDigits = new string(pClean.Where(c => char.IsDigit(c) || c == '.').ToArray());
                if (decimal.TryParse(pDigits, out var pNum) && pNum > 0)
                {
                    if (Math.Abs(pNum - num) < 0.1m ||
                        (num < 100 && Math.Abs(pNum - (num * 10m)) < 0.5m) ||
                        (num > 100 && Math.Abs((pNum * 10m) - num) < 0.5m))
                    {
                        return p;
                    }

                    if (num <= 24m && pNum > 24m)
                    {
                        var pct = (num / 24m) * 100m;
                        if (Math.Abs(pNum - pct) < 1.0m || Math.Abs(pNum - (pct * 10m)) < 10m)
                        {
                            return p;
                        }
                    }
                    else if (num > 24m && pNum <= 24m)
                    {
                        var karat = (num > 100 ? num / 10m : num);
                        var expectedKarat = (karat / 100m) * 24m;
                        if (Math.Abs(pNum - expectedKarat) < 0.5m)
                        {
                            return p;
                        }
                    }
                }
            }
        }

        return null;
    }

    private void CheckAutoAddNextItemRow(BillSubformItemViewModel? item)
    {
        if (item is null || Items.Count == 0 || !IsEditing) return;
        if (Items.Last() == item && IsItemRowFilled(item))
        {
            AddItemRow();
        }
    }

    public static bool IsItemRowFilled(BillSubformItemViewModel item)
    {
        return !string.IsNullOrWhiteSpace(item.Barcode)
            || !string.IsNullOrWhiteSpace(item.ItemName)
            || item.Rate > 0
            || item.NtWt > 0
            || item.Amount > 0;
    }

    private void RemoveItemRow(BillSubformItemViewModel? item)
    {
        if (item is not null && Items.Contains(item))
        {
            Items.Remove(item);
            if (Items.Count == 0)
            {
                AddItemRow();
            }
            RecalculateTotals();
        }
    }

    private void RemoveSelectedGridLine()
    {
        if (!IsEditing) return;
        RequestRemoveGridLine?.Invoke(this, EventArgs.Empty);
    }

    public void AddPaymentRow()
    {
        var pay = new PaymentSubformItemViewModel
        {
            AccountName = string.Empty,
            Amount = 0.00m
        };
        pay.PropertyChanged += Payment_PropertyChanged;
        Payments.Add(pay);
    }

    private void CheckAutoAddNextPaymentRow(PaymentSubformItemViewModel? pay)
    {
        if (pay is null || Payments.Count == 0) return;
        if (Payments.Last() == pay && IsPaymentRowFilled(pay))
        {
            AddPaymentRow();
        }
    }

    private static bool IsPaymentRowFilled(PaymentSubformItemViewModel pay)
    {
        return !string.IsNullOrWhiteSpace(pay.AccountName) || pay.Amount > 0;
    }

    private void RemovePaymentRow(PaymentSubformItemViewModel? pay)
    {
        if (pay is not null && Payments.Contains(pay))
        {
            Payments.Remove(pay);
            if (Payments.Count == 0)
            {
                AddPaymentRow();
            }
            RecalculateTotals();
        }
    }

    public async Task CreateDebtorPaymentAsync(PaymentSubformItemViewModel? targetRow = null)
    {
        if (!IsEditing) return;

        // 1. Identify target debtor name
        string? debtorName = null;
        if (targetRow != null && !string.IsNullOrWhiteSpace(targetRow.AccountName))
        {
            var trimmed = targetRow.AccountName.Trim();
            var isNonDebtorAccount = _allAccounts.Any(a =>
                string.Equals(a.AccountName?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(a.HeadCategory?.Trim(), "Debtors", StringComparison.OrdinalIgnoreCase)
                && !(a.HeadCategory?.Contains("Debtor", StringComparison.OrdinalIgnoreCase) ?? false));

            if (!isNonDebtorAccount && !trimmed.Equals("CASH", StringComparison.OrdinalIgnoreCase))
            {
                debtorName = trimmed;
            }
        }

        if (string.IsNullOrWhiteSpace(debtorName))
        {
            debtorName = CustomerName?.Trim();
        }

        if (string.IsNullOrWhiteSpace(debtorName))
        {
            ShowToast("Please enter or select a customer on the invoice first.", ToastType.Warning);
            return;
        }

        try
        {
            // 2. Check for existing account in _allAccounts or repository
            var existing = _allAccounts.FirstOrDefault(a =>
                string.Equals(a.AccountName?.Trim(), debtorName, StringComparison.OrdinalIgnoreCase));

            if (existing == null && _accountRepository != null)
            {
                try
                {
                    var allDb = await _accountRepository.GetAllAsync();
                    _allAccounts.Clear();
                    _allAccounts.AddRange(allDb);
                    existing = _allAccounts.FirstOrDefault(a =>
                        string.Equals(a.AccountName?.Trim(), debtorName, StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    // Fallback to in-memory accounts if DB query fails
                }
            }

            string finalAccountName;
            if (existing != null)
            {
                finalAccountName = existing.AccountName?.Trim() ?? debtorName;
            }
            else
            {
                // 3. Automatically create new Debtor account in AccountCreation table
                var partyCode = GenerateUniquePartyCode(debtorName, CustomerCode);

                var newAccount = new Account
                {
                    HeadCategory = "Debtors",
                    SubCategory = "Debtors",
                    AccountName = debtorName,
                    PartyCode = partyCode,
                    RefName = debtorName,
                    Address = Address?.Trim() ?? string.Empty,
                    Address2 = Address2?.Trim() ?? string.Empty,
                    City = City?.Trim() ?? string.Empty,
                    PinCode = Pincode?.Trim() ?? string.Empty,
                    State = State?.Trim() ?? string.Empty,
                    PhoneNo = Phone?.Trim() ?? string.Empty,
                    Mobile = Phone?.Trim() ?? string.Empty,
                    VatType = !string.IsNullOrWhiteSpace(VatType) ? VatType.Trim() : "GSTIN",
                    VatNo = GstinPan?.Trim() ?? string.Empty,
                    InOut = "INSIDE",
                    User = _currentUser,
                    EntryDate = DateTime.Today,
                    Status = "Saved"
                };

                if (_accountRepository != null)
                {
                    await _accountRepository.AddAsync(newAccount);
                }

                _allAccounts.Add(newAccount);
                finalAccountName = newAccount.AccountName;

                if (string.IsNullOrWhiteSpace(CustomerCode))
                {
                    _customerCode = partyCode;
                    OnPropertyChanged(nameof(CustomerCode));
                }

                _ = LoadCustomerLookupItemsAsync();
            }

            // Ensure account name is in PaymentAccountNames collection
            if (!_paymentAccountNames.Contains(finalAccountName, StringComparer.OrdinalIgnoreCase))
            {
                _paymentAccountNames.Add(finalAccountName);
                OnPropertyChanged(nameof(PaymentAccountNames));
            }

            // 4. Locate or create target row in Payments
            PaymentSubformItemViewModel? target = null;
            if (targetRow != null && Payments.Contains(targetRow))
            {
                target = targetRow;
            }
            else
            {
                target = Payments.FirstOrDefault(p => string.IsNullOrWhiteSpace(p.AccountName) && p.Amount == 0);
            }

            if (target == null)
            {
                target = new PaymentSubformItemViewModel
                {
                    AccountName = string.Empty,
                    Amount = 0.00m
                };
                target.PropertyChanged += Payment_PropertyChanged;
                Payments.Add(target);
            }

            // 5. Calculate remaining balance amount
            var otherPayments = Payments.Where(p => p != target).Sum(p => p.Amount);
            var balance = Math.Max(0m, NetAmt - otherPayments);

            // 6. Assign debtor account name and balance amount
            _isOpeningPaymentDialog = true;
            try
            {
                target.AccountName = finalAccountName;
                target.HeadCategory = "Debtors";
                target.Amount = balance;
            }
            finally
            {
                _isOpeningPaymentDialog = false;
            }

            RecalculateTotals();

            ShowToast($"Debtor ledger '{finalAccountName}' applied with balance ₹{balance:N2}.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to create/assign debtor ledger: " + ex.Message;
            ShowToast(ErrorMessage, ToastType.Error);
        }
    }

    private string GenerateUniquePartyCode(string baseName, string? preferredCode)
    {
        if (!string.IsNullOrWhiteSpace(preferredCode))
        {
            var clean = preferredCode.Trim();
            if (clean.Length > 50) clean = clean.Substring(0, 50);
            if (!_allAccounts.Any(a => string.Equals(a.PartyCode?.Trim(), clean, StringComparison.OrdinalIgnoreCase)))
            {
                return clean;
            }
        }

        var cleanName = new string(baseName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(cleanName)) cleanName = "DEBTOR";
        if (cleanName.Length > 40) cleanName = cleanName.Substring(0, 40);

        var code = cleanName;
        int counter = 1;
        while (_allAccounts.Any(a => string.Equals(a.PartyCode?.Trim(), code, StringComparison.OrdinalIgnoreCase)))
        {
            var suffix = counter.ToString();
            var maxBaseLen = 50 - suffix.Length;
            code = (cleanName.Length > maxBaseLen ? cleanName.Substring(0, maxBaseLen) : cleanName) + suffix;
            counter++;
        }
        return code;
    }

    private void Payment_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not PaymentSubformItemViewModel pay) return;

        if (e.PropertyName == nameof(PaymentSubformItemViewModel.AccountName))
        {
            if (!_isOpeningPaymentDialog && !_isLoadingBill)
            {
                OpenPaymentDetails(pay);
            }
        }
        else if (e.PropertyName == nameof(PaymentSubformItemViewModel.Amount))
        {
            RecalculateTotals();
        }
    }

    public void OpenPaymentDetails(PaymentSubformItemViewModel? pay)
    {
        if (pay is null || string.IsNullOrWhiteSpace(pay.AccountName) || _isOpeningPaymentDialog) return;

        var match = _allAccounts.FirstOrDefault(a => string.Equals(a.AccountName?.Trim(), pay.AccountName?.Trim(), StringComparison.OrdinalIgnoreCase));
        var headCategory = match?.HeadCategory ?? pay.HeadCategory;

        if (string.Equals(headCategory?.Trim(), "Purchases", StringComparison.OrdinalIgnoreCase) ||
            (headCategory != null && (headCategory.IndexOf("Purchase", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      headCategory.IndexOf("Old Gold", StringComparison.OrdinalIgnoreCase) >= 0)))
        {
            try
            {
                _isOpeningPaymentDialog = true;
                var types = _allItemTypes.Count > 0 ? _allItemTypes : new List<string> { "GOLD PRIMARY", "GOLD", "SILVER", "DIAMOND", "PLATINUM" };
                var dlg = new OldPurchasePaymentDetailsWindow(
                    pay.AccountName,
                    types,
                    initialAmount: pay.Amount,
                    defaultItemType: !string.IsNullOrWhiteSpace(pay.ItemType) ? pay.ItemType : "GOLD PRIMARY",
                    initialWt: pay.Wt,
                    initialLessWt: pay.LessWt,
                    initialAlloy: pay.Alloy,
                    initialRate: pay.Rate,
                    initialNarration: pay.Narration);

                dlg.SetOwnerSafely();

                if (dlg.ShowDialog() == true && dlg.Result is not null)
                {
                    var res = dlg.Result;
                    pay.ItemType = res.ItemType;
                    pay.ItemName = res.ItemName;
                    pay.Wt = res.Wt;
                    pay.LessWt = res.LessWt;
                    pay.GrWt = res.GrWt;
                    pay.Alloy = res.Alloy;
                    pay.NtWt = res.NtWt;
                    pay.Rate = res.Rate;
                    pay.Amount = res.Amount;
                    pay.Narration = res.Narration;
                }
            }
            finally
            {
                _isOpeningPaymentDialog = false;
            }
        }
        else if (string.Equals(headCategory?.Trim(), "Debtors", StringComparison.OrdinalIgnoreCase) ||
            (headCategory != null && headCategory.IndexOf("Debtor", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            if (_advanceOptions.Count > 0)
            {
                try
                {
                    _isOpeningPaymentDialog = true;
                    var dlg = new AdvanceSelectionDialog(pay.AccountName, _advanceOptions, initialSelectedOption: pay.AdvanceType);
                    dlg.SetOwnerSafely();
                    if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.SelectedOption))
                    {
                        pay.AdvanceType = dlg.SelectedOption;
                        pay.Narration = dlg.SelectedOption;
                    }
                }
                finally
                {
                    _isOpeningPaymentDialog = false;
                }
            }
        }

        RecalculateTotals();
        RequestFocusPaymentAmount?.Invoke(this, pay);
    }

    private bool _isRecalculatingTotals;

    public void RecalculateTotals()
    {
        if (_isRecalculatingTotals) return;
        try
        {
            _isRecalculatingTotals = true;
            GrossAmt = Items.Sum(i => i.GrossAmt);
            var itemDiscounts = Items.Sum(i => i.DisAmt);

            if (_isLoadingBill)
            {
                _additionalBillDiscount = Math.Max(0m, _discount - itemDiscounts);
            }
            else
            {
                var totalDiscount = itemDiscounts + _additionalBillDiscount;
                if (_discount != totalDiscount)
                {
                    _discount = totalDiscount;
                    OnPropertyChanged(nameof(Discount));
                }
            }

            NetAmt = Math.Max(0m, GrossAmt - Discount);
            TotalPaid = Payments.Sum(p => p.Amount);
            BalanceAmt = NetAmt - TotalPaid;

            OnPropertyChanged(nameof(TotalQty));
            OnPropertyChanged(nameof(TotalGrWt));
            OnPropertyChanged(nameof(TotalNtWt));
            OnPropertyChanged(nameof(TotalMetalValue));
            OnPropertyChanged(nameof(TotalMaking));
            OnPropertyChanged(nameof(CgstAmt));
            OnPropertyChanged(nameof(SgstAmt));
        }
        finally
        {
            _isRecalculatingTotals = false;
        }
    }

    private void ClearForm()
    {
        _isExistingInvoice = false;
        AppliedCoupon = null;
        CouponDiscountAmount = 0m;
        CouponCode = string.Empty;
        CouponStatusText = string.Empty;
        _selectedBill = null;
        OnPropertyChanged(nameof(SelectedBill));
        VouType = string.Empty;
        VouNo = string.Empty;
        Phone = string.Empty;
        CustomerName = string.Empty;
        _selectedCustomer = null;
        _isPhoneAutoPopulated = false;
        _isAddressAutoPopulated = false;
        OnPropertyChanged(nameof(SelectedCustomer));
        CustomerCode = string.Empty;
        CustomerGroup = string.Empty;
        LastPurchaseDate = null;
        AvgPurchaseValue = 0;
        TotalPurchaseValue = 0;
        CustomerPurchaseHistory.Clear();
        OnPropertyChanged(nameof(HasCustomerHistory));
        OnPropertyChanged(nameof(HasNoCustomerHistory));
        SelectedCustomerTab = 0;
        Address = string.Empty;
        Address2 = string.Empty;
        City = string.Empty;
        Pincode = string.Empty;
        Email = string.Empty;
        GstinPan = string.Empty;
        Remarks = string.Empty;
        _additionalBillDiscount = 0.00m;
        Discount = 0.00m;
        GrossAmt = 0.00m;
        NetAmt = 0.00m;
        TotalPaid = 0.00m;
        BalanceAmt = 0.00m;
        Q1 = 4;
        Q2 = 4;
        Q3 = 4;
        Items.Clear();
        Payments.Clear();
        WhatsAppMessageId = null;
        WhatsAppDeliveryStatus = null;
        WhatsAppReply = null;
        EnsureDefaultRows();
        RecalculateTotals();
        RaiseSessionTitleChanged();
    }

    public async Task SyncWhatsAppStatusForCurrentBillAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("Please select or save an invoice first.", ToastType.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            string? msgId = WhatsAppMessageId;
            if (string.IsNullOrWhiteSpace(msgId) && _whatsAppBillTrackerRepository != null)
            {
                var tracker = await _whatsAppBillTrackerRepository.GetByInvoiceNoAsync(VouNo.Trim());
                if (tracker != null)
                {
                    msgId = tracker.Wamid;
                    WhatsAppMessageId = tracker.Wamid;
                    WhatsAppDeliveryStatus = !string.IsNullOrWhiteSpace(tracker.DeliveryStatus) ? tracker.DeliveryStatus : tracker.Rating;
                    WhatsAppReply = tracker.Reply;
                }
            }

            if (string.IsNullOrWhiteSpace(msgId))
            {
                ShowToast("No WhatsApp message has been recorded for this invoice yet.", ToastType.Info);
                return;
            }

            var newStatus = await _whatsAppService.FetchMessageStatusAsync(msgId);
            if (!string.IsNullOrWhiteSpace(newStatus))
            {
                WhatsAppDeliveryStatus = newStatus;
                if (_whatsAppBillTrackerRepository != null)
                {
                    await _whatsAppBillTrackerRepository.UpdateDeliveryStatusAsync(msgId, newStatus);
                }
                ShowToast($"WhatsApp Status: {newStatus}", ToastType.Success);
            }
            else
            {
                ShowToast($"WhatsApp status: {WhatsAppDeliveryStatus ?? "Pending / Not updated"}", ToastType.Info);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to fetch WhatsApp status: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
