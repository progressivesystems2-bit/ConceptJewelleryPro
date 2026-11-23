using System.Collections.ObjectModel;
using System.Windows;
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

public sealed class PurchaseViewModel : ViewModelBase
{
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IAccountRepository? _accountRepository;
    private readonly IItemRepository? _itemRepository;
    private readonly IPurityRepository? _purityRepository;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;

    private Purchase? _originalPurchaseSnapshot;
    private List<PurchaseSubform>? _originalItemsSnapshot;
    private List<PaymentSubform>? _originalPaymentsSnapshot;

    private readonly List<SalesVouType> _purchaseVouTypes = new();
    private readonly List<Account> _allAccounts = new();
    private readonly List<Item> _allItems = new();
    private readonly List<PurityMaster> _allPurities = new();
    private readonly RangeObservableCollection<string> _availableItemNames = new();
    private readonly RangeObservableCollection<string> _availablePurityNames = new();
    private readonly RangeObservableCollection<SupplierOption> _availableSuppliers = new();
    private readonly RangeObservableCollection<string> _paymentAccountNames = new();

    private string _vouType = string.Empty;
    private string _vouNo = string.Empty;
    private DateTime _billDate = DateTime.Today;
    private DateTime _dueDate = DateTime.Today;
    private string _suppBillNo = string.Empty;
    private DateTime? _suppDate = DateTime.Today;
    private SupplierOption? _selectedSupplier;
    private string _accountName = string.Empty;
    private string _customerName = string.Empty;
    private string _customerCode = string.Empty;
    private string _phone = string.Empty;
    private string _address = string.Empty;
    private string _address2 = string.Empty;
    private string _city = "CUTTACK";
    private string _pincode = string.Empty;
    private string _email = string.Empty;
    private string _vatNo = string.Empty;
    private string _remarks = string.Empty;
    private string _narration = string.Empty;
    private string _note = string.Empty;
    private string _status = "Saved";

    private decimal _grossAmt;
    private decimal _discountAllowed;
    private decimal _taxableAmount;
    private decimal _taxAmt;
    private decimal _addLess;
    private string _addLessNote = string.Empty;
    private bool _autoRoundOff = true;
    private decimal _netAmt;
    private decimal _totalPaid;
    private decimal _balanceAmt;

    private bool _isBusy;
    private bool _isEditing;
    private bool _isLoadingPurchase;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    private Purchase? _selectedPurchase;

    public PurchaseViewModel(
        IPurchaseRepository purchaseRepository,
        IAccountRepository? accountRepository = null,
        IItemRepository? itemRepository = null,
        IPurityRepository? purityRepository = null,
        ISettingsRepository? settingsRepository = null,
        string currentUser = "admin",
        IAuditRepository? auditRepository = null)
    {
        _purchaseRepository = purchaseRepository;
        _accountRepository = accountRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _settingsRepository = settingsRepository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;

        Items = new RangeObservableCollection<PurchaseSubformItemViewModel>();
        Payments = new RangeObservableCollection<PaymentSubformItemViewModel>();
        Purchases = new RangeObservableCollection<Purchase>();
        PurchaseTypes = new ObservableCollection<string>();
        Cities = new List<string> { "CUTTACK", "BHUBANESWAR", "PURI", "ROURKELA", "BERHAMPUR", "KOLKATA", "MUMBAI", "DELHI" };

        AddNewCommand = new AsyncRelayCommand(AddNewAsync);
        ModifyCommand = new RelayCommand(Modify);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        CancelCommand = new RelayCommand(Cancel);
        PrintCommand = new RelayCommand(Print);

        AddItemRowCommand = new RelayCommand(AddItemRow);
        RemoveItemRowCommand = new RelayCommand(parameter => RemoveItemRow(parameter as PurchaseSubformItemViewModel));
        RemoveSelectedGridLineCommand = new RelayCommand(RemoveSelectedGridLine);
        AddPaymentRowCommand = new RelayCommand(AddPaymentRow);
        RemovePaymentRowCommand = new RelayCommand(parameter => RemovePaymentRow(parameter as PaymentSubformItemViewModel));
        AutoRoundOffCommand = new RelayCommand(_ => ApplyAutoRoundOff());
        OpenListCommand = new AsyncRelayCommand(OpenListAsync);

        _ = InitializeAsync();
    }

    public RangeObservableCollection<PurchaseSubformItemViewModel> Items { get; }
    public RangeObservableCollection<PaymentSubformItemViewModel> Payments { get; }
    public RangeObservableCollection<Purchase> Purchases { get; }
    public ObservableCollection<string> PurchaseTypes { get; }
    public RangeObservableCollection<SupplierOption> AvailableSuppliers => _availableSuppliers;
    public RangeObservableCollection<string> PaymentAccountNames => _paymentAccountNames;
    public RangeObservableCollection<string> AvailableItemNames => _availableItemNames;
    public RangeObservableCollection<string> AvailablePurityNames => _availablePurityNames;
    public List<string> Cities { get; }

    public ICommand AddNewCommand { get; }
    public ICommand ModifyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand OpenListCommand { get; }
    public ICommand AddItemRowCommand { get; }
    public ICommand RemoveItemRowCommand { get; }
    public ICommand RemoveSelectedGridLineCommand { get; }
    public ICommand AddPaymentRowCommand { get; }
    public ICommand RemovePaymentRowCommand { get; }
    public ICommand AutoRoundOffCommand { get; }

    public string VouType
    {
        get => _vouType;
        set
        {
            if (SetProperty(ref _vouType, value))
            {
                if (IsEditing && !_isLoadingPurchase)
                {
                    _ = RefreshNextVouNoAsync();
                }
            }
        }
    }

    public string VouNo
    {
        get => _vouNo;
        set => SetProperty(ref _vouNo, value);
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

    public string SuppBillNo
    {
        get => _suppBillNo;
        set => SetProperty(ref _suppBillNo, value);
    }

    public DateTime? SuppDate
    {
        get => _suppDate;
        set => SetProperty(ref _suppDate, value);
    }

    private bool _isApplyingSupplier;

    public SupplierOption? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (SetProperty(ref _selectedSupplier, value) && value is not null)
            {
                ApplySupplier(value);
            }
        }
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

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string CustomerCode
    {
        get => _customerCode;
        set => SetProperty(ref _customerCode, value);
    }

    public string Phone
    {
        get => _phone;
        set
        {
            if (SetProperty(ref _phone, value))
            {
                if (IsEditing && !_isLoadingPurchase && !_isApplyingSupplier && !string.IsNullOrWhiteSpace(value))
                {
                    OnPhoneChanged(value);
                }
            }
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
        if (string.IsNullOrWhiteSpace(pincode)) return;
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

    private string _state = "21-ODISHA";
    public string State
    {
        get => _state;
        set => SetProperty(ref _state, AccountCreationViewModel.MapStateName(value));
    }

    private bool _check;
    public bool Check
    {
        get => _check;
        set => SetProperty(ref _check, value);
    }

    private string _vatType = "GSTIN";
    public string VatType
    {
        get => _vatType;
        set => SetProperty(ref _vatType, value);
    }

    public IReadOnlyList<string> VatTypes { get; } = new List<string> { "GSTIN", "PAN", "TIN", "AADHAAR", "URD" };

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

    private string _customerGroup = "Sundry Creditors";
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

    public string CustomerGroupDisplay => string.IsNullOrWhiteSpace(_customerGroup) ? "Sundry Creditors" : _customerGroup;

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

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string VatNo
    {
        get => _vatNo;
        set => SetProperty(ref _vatNo, value);
    }

    public string Remarks
    {
        get => _remarks;
        set => SetProperty(ref _remarks, value);
    }

    public string Narration
    {
        get => _narration;
        set => SetProperty(ref _narration, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public decimal GrossAmt
    {
        get => _grossAmt;
        set => SetProperty(ref _grossAmt, value);
    }

    public decimal DiscountAllowed
    {
        get => _discountAllowed;
        set
        {
            if (SetProperty(ref _discountAllowed, value))
            {
                RecalculateTotals();
            }
        }
    }

    public decimal TaxableAmount
    {
        get => _taxableAmount;
        set => SetProperty(ref _taxableAmount, value);
    }

    public decimal TaxAmt
    {
        get => _taxAmt;
        set => SetProperty(ref _taxAmt, value);
    }

    public bool AutoRoundOff
    {
        get => _autoRoundOff;
        set
        {
            if (SetProperty(ref _autoRoundOff, value) && value)
            {
                RecalculateTotals();
            }
        }
    }

    public decimal AddLess
    {
        get => _addLess;
        set
        {
            if (SetProperty(ref _addLess, value))
            {
                _autoRoundOff = false;
                OnPropertyChanged(nameof(AutoRoundOff));
                RecalculateTotals();
            }
        }
    }

    public string AddLessNote
    {
        get => _addLessNote;
        set => SetProperty(ref _addLessNote, value);
    }

    public decimal NetAmt
    {
        get => _netAmt;
        set => SetProperty(ref _netAmt, value);
    }

    public decimal TotalPaid
    {
        get => _totalPaid;
        set => SetProperty(ref _totalPaid, value);
    }

    public decimal BalanceAmt
    {
        get => _balanceAmt;
        set => SetProperty(ref _balanceAmt, value);
    }

    public decimal TotalQty => Items.Sum(i => i.Qty);
    public decimal TotalGrWt => Items.Sum(i => i.GrWt);
    public decimal TotalNtWt => Items.Sum(i => i.NtWt);
    public decimal TotalMetalValue => Items.Sum(i => i.MetalValue);
    public decimal TotalMaking => Items.Sum(i => i.Making);
    public decimal TotalStoneValue => Items.Sum(i => i.StoneValue);

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(IsNotEditing));
            }
        }
    }

    public bool CanEdit => IsEditing;
    public bool CanModify => !IsEditing && !string.IsNullOrWhiteSpace(VouNo);
    public bool IsNotEditing => !IsEditing;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
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

    public Purchase? SelectedPurchase
    {
        get => _selectedPurchase;
        set
        {
            if (SetProperty(ref _selectedPurchase, value) && value is not null)
            {
                _ = LoadPurchaseAsync(value.VouNo);
            }
        }
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await LoadMasterDataAsync();
            await LoadPurchasesListAsync();
            await AddNewAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Initialization error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadMasterDataAsync()
    {
        try
        {
            var appSettings = AppSettings.Load();
            var currentCounter = appSettings.Counter;

            var vouTypes = await _purchaseRepository.GetPurchaseVouTypesAsync(currentCounter, "Purchase");
            if (vouTypes.Count == 0 && !string.IsNullOrWhiteSpace(currentCounter))
            {
                vouTypes = await _purchaseRepository.GetPurchaseVouTypesAsync(null, "Purchase");
            }

            _purchaseVouTypes.Clear();
            _purchaseVouTypes.AddRange(vouTypes);

            var names = vouTypes
                .Select(v => v.VouType)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            PurchaseTypes.Clear();
            foreach (var name in names)
            {
                PurchaseTypes.Add(name);
            }

            if (PurchaseTypes.Count > 0 && (string.IsNullOrWhiteSpace(VouType) || !PurchaseTypes.Contains(VouType)))
            {
                VouType = PurchaseTypes[0];
            }

            if (_accountRepository != null)
            {
                var accounts = await Task.Run(async () => await _accountRepository.GetAllAsync());
                _allAccounts.Clear();
                _allAccounts.AddRange(accounts);

                var supplierList = new List<SupplierOption>();
                var payList = new List<string>();

                foreach (var a in accounts)
                {
                    if (!string.IsNullOrWhiteSpace(a.AccountName))
                    {
                        var opt = MapAccountToSupplierOption(a);
                        supplierList.Add(opt);

                        var hc = a.HeadCategory?.Trim() ?? string.Empty;
                        var hcLower = hc.ToLowerInvariant();
                        if (hcLower.Contains("bank") || hcLower.Contains("cash") || hcLower.Contains("card") || hcLower.Contains("old gold") || hcLower.Contains("purchase") || hcLower.Contains("creditor"))
                        {
                            payList.Add(opt.Name);
                        }
                    }
                }

                supplierList = supplierList.OrderBy(s => s.Name).ToList();
                _availableSuppliers.ReplaceAll(supplierList);
                _paymentAccountNames.ReplaceAll(payList.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList());
            }

            if (_itemRepository != null)
            {
                var items = await _itemRepository.GetAllAsync();
                _allItems.Clear();
                _allItems.AddRange(items);

                var distinctNames = items
                    .Where(i => !string.IsNullOrWhiteSpace(i.ItemName))
                    .Select(i => i.ItemName.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                _availableItemNames.ReplaceAll(distinctNames);
            }

            if (_purityRepository != null)
            {
                var purities = await _purityRepository.GetAllAsync();
                _allPurities.Clear();
                _allPurities.AddRange(purities);

                var purityNames = purities
                    .Where(p => !string.IsNullOrWhiteSpace(p.Purity))
                    .Select(p => p.Purity.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p)
                    .ToList();

                _availablePurityNames.ReplaceAll(purityNames);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading master data: {ex.Message}";
        }
    }

    private async Task LoadPurchasesListAsync()
    {
        try
        {
            var list = await Task.Run(async () => await _purchaseRepository.GetAllAsync());
            Purchases.ReplaceAll(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading purchases: {ex.Message}";
        }
    }

    private async Task RefreshNextVouNoAsync()
    {
        try
        {
            var shortName = _purchaseVouTypes.FirstOrDefault(v => v.VouType.Equals(VouType, StringComparison.OrdinalIgnoreCase))?.ShortName;
            VouNo = await _purchaseRepository.GetNextVouNoAsync(VouType, shortName);
        }
        catch
        {
            VouNo = "PUR-001";
        }
    }

    public async Task AddNewAsync()
    {
        ClearForm();
        IsEditing = true;
        if (PurchaseTypes.Count > 0 && (string.IsNullOrWhiteSpace(VouType) || !PurchaseTypes.Contains(VouType)))
        {
            VouType = PurchaseTypes[0];
        }
        await RefreshNextVouNoAsync();
        AddItemRow();
        AddPaymentRow();
    }

    public void Modify()
    {
        if (!string.IsNullOrWhiteSpace(VouNo))
        {
            IsEditing = true;
        }
    }

    public void Cancel()
    {
        IsEditing = false;
        if (SelectedPurchase is not null)
        {
            _ = LoadPurchaseAsync(SelectedPurchase.VouNo);
        }
        else
        {
            ClearForm();
        }
    }

    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo))
        {
            ShowToast("Voucher number is required.", ToastType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(AccountName))
        {
            ShowToast("Please select a supplier account.", ToastType.Warning);
            return;
        }

        var validItems = Items.Where(i => !string.IsNullOrWhiteSpace(i.ItemName) || i.Amount > 0 || i.GrWt > 0).ToList();
        if (validItems.Count == 0)
        {
            ShowToast("Please enter at least one purchase item.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var purchase = new Purchase
            {
                VouNo = VouNo.Trim(),
                VouType = VouType,
                BillDate = BillDate,
                DueDate = DueDate,
                SuppBillNo = SuppBillNo,
                SuppDate = SuppDate,
                AccountName = AccountName,
                CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? AccountName : CustomerName,
                CustomerCode = CustomerCode,
                Phone = Phone,
                Address = Address,
                Address2 = !string.IsNullOrWhiteSpace(State) ? State : Address2,
                City = City,
                Pincode = Pincode,
                Email = Email,
                VatNo = VatNo,
                VatType = VatType,
                Check = Check,
                GrossAmt = GrossAmt,
                DiscountAllowed = DiscountAllowed,
                TaxableAmount = TaxableAmount,
                TaxAmt = TaxAmt,
                AddLess = AddLess,
                AddLessNote = AddLessNote,
                NetAmt = NetAmt,
                Remarks = Remarks,
                Narration = Narration,
                Note = Note,
                Status = "Saved",
                TotalQty = TotalQty,
                GrWt = TotalGrWt,
                NtWt = TotalNtWt,
                EntryUser = _currentUser,
                EntryDate = DateTime.Today,
                EntryTime = DateTime.Now
            };

            var domainItems = validItems.Select((item, idx) => item.ToDomainModel(purchase.VouNo, idx + 1)).ToList();
            var validPayments = Payments.Where(p => p.Amount > 0 || !string.IsNullOrWhiteSpace(p.AccountName)).Select(p => p.ToDomainModel(purchase.VouNo)).ToList();

            var existing = await _purchaseRepository.GetByVouNoAsync(purchase.VouNo);
            if (existing is not null)
            {
                await _purchaseRepository.UpdateAsync(purchase, domainItems, validPayments);

                if (_auditRepository != null)
                {
                    await AuditService.LogPurchaseModificationAsync(
                        _auditRepository,
                        _originalPurchaseSnapshot,
                        _originalItemsSnapshot,
                        _originalPaymentsSnapshot,
                        purchase,
                        domainItems,
                        validPayments,
                        _currentUser);
                }

                ShowToast($"Purchase {purchase.VouNo} updated successfully.", ToastType.Success);
            }
            else
            {
                await _purchaseRepository.AddAsync(purchase, domainItems, validPayments);
                ShowToast($"Purchase {purchase.VouNo} saved successfully.", ToastType.Success);
            }

            BarcodeEvents.NotifyBarcodesChanged();

            IsEditing = false;
            await LoadPurchasesListAsync();
            SelectedPurchase = Purchases.FirstOrDefault(p => p.VouNo.Equals(purchase.VouNo, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving purchase: {ex.Message}";
            ShowToast($"Save failed: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteAsync()
    {
        if (string.IsNullOrWhiteSpace(VouNo)) return;

        if (MessageBox.Show($"Are you sure you want to delete purchase {VouNo}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var targetPurchase = _originalPurchaseSnapshot ?? await _purchaseRepository.GetByVouNoAsync(VouNo);
            var targetItems = _originalItemsSnapshot ?? (await _purchaseRepository.GetSubformItemsAsync(VouNo)).ToList();
            var targetPayments = _originalPaymentsSnapshot ?? (await _purchaseRepository.GetPaymentItemsAsync(VouNo)).ToList();

            await _purchaseRepository.DeleteAsync(VouNo);

            if (_auditRepository != null && targetPurchase != null)
            {
                await AuditService.LogPurchaseDeletionAsync(_auditRepository, targetPurchase, targetItems, targetPayments, _currentUser);
            }

            ShowToast($"Purchase {VouNo} deleted successfully.", ToastType.Success);
            BarcodeEvents.NotifyBarcodesChanged();
            await LoadPurchasesListAsync();
            await AddNewAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting purchase: {ex.Message}";
            ShowToast($"Delete failed: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadPurchaseAsync(string vouNo)
    {
        if (string.IsNullOrWhiteSpace(vouNo)) return;

        _isLoadingPurchase = true;
        IsBusy = true;
        try
        {
            var p = await _purchaseRepository.GetByVouNoAsync(vouNo);
            if (p is null) return;

            VouNo = p.VouNo;
            VouType = p.VouType;
            BillDate = p.BillDate;
            DueDate = p.DueDate;
            SuppBillNo = p.SuppBillNo;
            SuppDate = p.SuppDate;
            AccountName = p.AccountName;
            SelectedSupplier = _availableSuppliers.FirstOrDefault(s => string.Equals(s.Name, p.AccountName, StringComparison.OrdinalIgnoreCase));
            CustomerName = p.CustomerName;
            CustomerCode = p.CustomerCode;
            Phone = p.Phone;
            Address = p.Address;
            Address2 = p.Address2;
            City = p.City;
            Pincode = p.Pincode;
            Email = p.Email;
            VatNo = p.VatNo;
            Check = p.Check;
            VatType = !string.IsNullOrWhiteSpace(p.VatType) ? p.VatType : "GSTIN";
            State = !string.IsNullOrWhiteSpace(p.Address2) ? p.Address2 : "21-ODISHA";
            Remarks = p.Remarks;
            Narration = p.Narration;
            Note = p.Note;
            Status = p.Status;
            DiscountAllowed = p.DiscountAllowed;
            AddLess = p.AddLess;
            AddLessNote = p.AddLessNote;
            _autoRoundOff = p.AddLess == 0;
            OnPropertyChanged(nameof(AutoRoundOff));
            RefreshSupplierMetrics(p.AccountName);

            var subItems = await _purchaseRepository.GetSubformItemsAsync(vouNo);
            Items.Clear();
            foreach (var sub in subItems)
            {
                var itemVm = PurchaseSubformItemViewModel.FromDomainModel(sub);
                itemVm.PropertyChanged += Item_PropertyChanged;
                Items.Add(itemVm);
            }

            var paymentItems = await _purchaseRepository.GetPaymentItemsAsync(vouNo);
            Payments.Clear();
            foreach (var pay in paymentItems)
            {
                var payVm = PaymentSubformItemViewModel.FromDomainModel(pay);
                payVm.PropertyChanged += Payment_PropertyChanged;
                Payments.Add(payVm);
            }

            _originalPurchaseSnapshot = new Purchase
            {
                VouNo = p.VouNo,
                VouType = p.VouType,
                BillDate = p.BillDate,
                DueDate = p.DueDate,
                SuppBillNo = p.SuppBillNo,
                SuppDate = p.SuppDate,
                AccountName = p.AccountName,
                CustomerName = p.CustomerName,
                CustomerCode = p.CustomerCode,
                Phone = p.Phone,
                Address = p.Address,
                Address2 = p.Address2,
                City = p.City,
                Pincode = p.Pincode,
                Email = p.Email,
                VatNo = p.VatNo,
                Remarks = p.Remarks,
                Narration = p.Narration,
                Note = p.Note,
                Status = p.Status,
                GrossAmt = p.GrossAmt,
                DiscountAllowed = p.DiscountAllowed,
                TaxableAmount = p.TaxableAmount,
                TaxAmt = p.TaxAmt,
                AddLess = p.AddLess,
                AddLessNote = p.AddLessNote,
                NetAmt = p.NetAmt,
                TotalQty = p.TotalQty,
                GrWt = p.GrWt,
                NtWt = p.NtWt
            };
            _originalItemsSnapshot = subItems.Select(x => new PurchaseSubform
            {
                VouNo = x.VouNo,
                SlNo = x.SlNo,
                Barcode = x.Barcode,
                ItemName = x.ItemName,
                Purity = x.Purity,
                Qty = x.Qty,
                GrWt = x.GrWt,
                LessWt = x.LessWt,
                NtWt = x.NtWt,
                Rate = x.Rate,
                Amount = x.Amount,
                MetalValue = x.MetalValue,
                Making = x.Making,
                StoneValue = x.StoneValue,
                GstPer = x.GstPer,
                GstAmt = x.GstAmt
            }).ToList();
            _originalPaymentsSnapshot = paymentItems.Select(x => new PaymentSubform
            {
                VouNo = x.VouNo,
                AccountName = x.AccountName,
                Amount = x.Amount,
                BankName = x.BankName,
                CardType = x.CardType,
                ChequeNo = x.ChequeNo,
                Narration = x.Narration
            }).ToList();

            RecalculateTotals();
            IsEditing = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading purchase: {ex.Message}";
        }
        finally
        {
            _isLoadingPurchase = false;
            IsBusy = false;
        }
    }

    public void AddItemRow()
    {
        var item = new PurchaseSubformItemViewModel
        {
            SlNo = Items.Count + 1,
            GstPer = 3.00m
        };
        item.PropertyChanged += Item_PropertyChanged;
        Items.Add(item);
        RecalculateTotals();
    }

    private void RemoveItemRow(PurchaseSubformItemViewModel? item)
    {
        if (item is not null && Items.Contains(item))
        {
            item.PropertyChanged -= Item_PropertyChanged;
            Items.Remove(item);

            int sl = 1;
            foreach (var it in Items)
            {
                it.SlNo = sl++;
            }

            if (Items.Count == 0)
            {
                AddItemRow();
            }
            RecalculateTotals();
        }
    }

    private void RemoveSelectedGridLine()
    {
        if (!CanEdit) return;
        if (Items.Count > 0)
        {
            RemoveItemRow(Items.LastOrDefault());
        }
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

    private void RemovePaymentRow(PaymentSubformItemViewModel? pay)
    {
        if (pay is not null && Payments.Contains(pay))
        {
            pay.PropertyChanged -= Payment_PropertyChanged;
            Payments.Remove(pay);
            if (Payments.Count == 0)
            {
                AddPaymentRow();
            }
            RecalculateTotals();
        }
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isLoadingPurchase) return;

        if (e.PropertyName == nameof(PurchaseSubformItemViewModel.ItemName) && sender is PurchaseSubformItemViewModel itemVm)
        {
            if (!string.IsNullOrWhiteSpace(itemVm.ItemName))
            {
                var matchedItem = _allItems.FirstOrDefault(i => string.Equals(i.ItemName, itemVm.ItemName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (matchedItem != null)
                {
                    if (string.IsNullOrWhiteSpace(itemVm.Hsncode) && !string.IsNullOrWhiteSpace(matchedItem.HSNCode))
                    {
                        itemVm.Hsncode = matchedItem.HSNCode;
                    }
                    if (string.IsNullOrWhiteSpace(itemVm.Purity) && !string.IsNullOrWhiteSpace(matchedItem.PurityDisplay))
                    {
                        itemVm.Purity = matchedItem.PurityDisplay;
                    }
                    if (itemVm.GstPer == 3.00m && matchedItem.GSTRate > 0)
                    {
                        itemVm.GstPer = matchedItem.GSTRate;
                    }
                    if (itemVm.Rate == 0 && matchedItem.PurchaseRate > 0)
                    {
                        itemVm.Rate = matchedItem.PurchaseRate;
                    }
                }
            }
        }

        RecalculateTotals();
    }

    private void Payment_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isLoadingPurchase) return;
        RecalculateTotals();
    }

    private void OnAccountNameChanged(string accountName)
    {
        if (_isApplyingSupplier || _isLoadingPurchase) return;

        if (string.IsNullOrWhiteSpace(accountName))
        {
            RefreshSupplierMetrics(null);
            return;
        }

        var clean = accountName.Trim();

        // 1. Check if SelectedSupplier already matches
        if (SelectedSupplier != null && string.Equals(SelectedSupplier.Name, clean, StringComparison.OrdinalIgnoreCase))
        {
            ApplySupplier(SelectedSupplier);
            return;
        }

        // 2. Find in _availableSuppliers by exact name or party code
        var supplier = _availableSuppliers.FirstOrDefault(s =>
            string.Equals(s.Name, clean, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(s.PartyCode) && string.Equals(s.PartyCode, clean, StringComparison.OrdinalIgnoreCase)));

        // 3. If not found, search by prefix (StartsWith)
        if (supplier is null)
        {
            supplier = _availableSuppliers.FirstOrDefault(s =>
                s.Name.StartsWith(clean, StringComparison.OrdinalIgnoreCase));
        }

        // 4. If not found, search by contains
        if (supplier is null)
        {
            supplier = _availableSuppliers.FirstOrDefault(s =>
                s.Name.IndexOf(clean, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // 5. If found, apply supplier
        if (supplier is not null)
        {
            ApplySupplier(supplier);
        }
        else
        {
            // Fallback: check _allAccounts directly
            var matchedAcc = _allAccounts.FirstOrDefault(a =>
                string.Equals(a.AccountName, clean, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(a.PartyCode) && string.Equals(a.PartyCode, clean, StringComparison.OrdinalIgnoreCase)) ||
                a.AccountName.StartsWith(clean, StringComparison.OrdinalIgnoreCase));

            if (matchedAcc is not null)
            {
                var opt = MapAccountToSupplierOption(matchedAcc);
                ApplySupplier(opt);
            }
            else
            {
                CustomerName = clean;
                RefreshSupplierMetrics(clean);
            }
        }
    }

    private void ApplySupplier(SupplierOption? supplier)
    {
        if (supplier is null || _isLoadingPurchase) return;

        try
        {
            _isApplyingSupplier = true;

            if (_selectedSupplier != supplier)
            {
                _selectedSupplier = supplier;
                OnPropertyChanged(nameof(SelectedSupplier));
            }

            if (!string.Equals(_accountName, supplier.Name, StringComparison.OrdinalIgnoreCase))
            {
                _accountName = supplier.Name;
                OnPropertyChanged(nameof(AccountName));
            }

            CustomerName = !string.IsNullOrWhiteSpace(supplier.Name) ? supplier.Name : _accountName;
            CustomerCode = supplier.PartyCode ?? string.Empty;

            var phone = !string.IsNullOrWhiteSpace(supplier.Mobile) ? supplier.Mobile : supplier.Phone;
            if (!string.IsNullOrWhiteSpace(phone))
            {
                Phone = phone;
            }

            Address = supplier.Address ?? string.Empty;
            Address2 = supplier.Address2 ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(supplier.PinCode))
            {
                Pincode = supplier.PinCode;
            }

            if (!string.IsNullOrWhiteSpace(supplier.City))
            {
                City = supplier.City;
            }
            else if (!string.IsNullOrWhiteSpace(Pincode) && Pincode.Length == 6)
            {
                var pinResult = PincodeService.Lookup(Pincode);
                if (pinResult.HasValue && !string.IsNullOrWhiteSpace(pinResult.Value.City))
                {
                    City = pinResult.Value.City;
                }
            }

            if (!string.IsNullOrWhiteSpace(supplier.State))
            {
                State = AccountCreationViewModel.MapStateName(supplier.State);
            }
            else if (!string.IsNullOrWhiteSpace(Pincode) && Pincode.Length == 6)
            {
                var pinResult = PincodeService.Lookup(Pincode);
                if (pinResult.HasValue && !string.IsNullOrWhiteSpace(pinResult.Value.State))
                {
                    State = AccountCreationViewModel.MapStateName(pinResult.Value.State);
                }
            }

            if (!string.IsNullOrWhiteSpace(supplier.VatNo))
            {
                VatNo = supplier.VatNo;
            }

            if (!string.IsNullOrWhiteSpace(supplier.VatType))
            {
                VatType = supplier.VatType;
            }

            if (!string.IsNullOrWhiteSpace(supplier.Email))
            {
                Email = supplier.Email;
            }

            RefreshSupplierMetrics(supplier.Name);
        }
        finally
        {
            _isApplyingSupplier = false;
        }
    }

    private void OnPhoneChanged(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || _isApplyingSupplier || _isLoadingPurchase) return;

        var clean = new string(phone.Where(char.IsDigit).ToArray());
        if (clean.Length < 10) return;

        var last10 = clean.Length > 10 ? clean[^10..] : clean;

        var match = _availableSuppliers.FirstOrDefault(s =>
        {
            var m = new string((s.Mobile ?? "").Where(char.IsDigit).ToArray());
            var p = new string((s.Phone ?? "").Where(char.IsDigit).ToArray());
            return m.EndsWith(last10, StringComparison.Ordinal) || p.EndsWith(last10, StringComparison.Ordinal);
        });

        if (match is not null)
        {
            ApplySupplier(match);
        }
        else
        {
            var accMatch = _allAccounts.FirstOrDefault(a =>
            {
                var m = new string((a.Mobile ?? "").Where(char.IsDigit).ToArray());
                var p = new string((a.PhoneNo ?? "").Where(char.IsDigit).ToArray());
                return m.EndsWith(last10, StringComparison.Ordinal) || p.EndsWith(last10, StringComparison.Ordinal);
            });

            if (accMatch is not null)
            {
                var opt = MapAccountToSupplierOption(accMatch);
                ApplySupplier(opt);
            }
        }
    }

    private static SupplierOption MapAccountToSupplierOption(ConceptJewelleryPro.Domain.Entities.Account a)
    {
        var phone = !string.IsNullOrWhiteSpace(a.Mobile)
            ? a.Mobile.Trim()
            : (!string.IsNullOrWhiteSpace(a.PhoneNo) ? a.PhoneNo.Trim() : string.Empty);

        var state = !string.IsNullOrWhiteSpace(a.State)
            ? AccountCreationViewModel.MapStateName(a.State)
            : string.Empty;

        return new SupplierOption
        {
            Name = a.AccountName?.Trim() ?? string.Empty,
            Phone = phone,
            Mobile = a.Mobile?.Trim() ?? string.Empty,
            Address = a.Address?.Trim() ?? string.Empty,
            Address2 = a.Address2?.Trim() ?? string.Empty,
            City = a.City?.Trim() ?? string.Empty,
            PinCode = a.PinCode?.Trim() ?? string.Empty,
            State = state,
            VatNo = !string.IsNullOrWhiteSpace(a.VatNo) ? a.VatNo.Trim() : (a.PanNo?.Trim() ?? string.Empty),
            VatType = a.VatType?.Trim() ?? string.Empty,
            Email = a.Email?.Trim() ?? string.Empty,
            PartyCode = a.PartyCode?.Trim() ?? string.Empty,
            HeadCategory = a.HeadCategory?.Trim() ?? string.Empty
        };
    }

    public void RefreshSupplierMetrics(string? accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            CustomerGroup = "Sundry Creditors";
            TotalPurchaseValue = 0;
            AvgPurchaseValue = 0;
            LastPurchaseDate = null;
            CustomerPurchaseHistory.Clear();
            OnPropertyChanged(nameof(HasCustomerHistory));
            OnPropertyChanged(nameof(HasNoCustomerHistory));
            return;
        }

        var cleanName = accountName.Trim();
        var matchedSupplier = _availableSuppliers.FirstOrDefault(s => string.Equals(s.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        CustomerGroup = !string.IsNullOrWhiteSpace(matchedSupplier?.HeadCategory) ? matchedSupplier.HeadCategory : "Sundry Creditors";

        var pastPurchases = Purchases
            .Where(p => !string.IsNullOrWhiteSpace(p.AccountName) && string.Equals(p.AccountName.Trim(), cleanName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.BillDate)
            .ToList();

        if (pastPurchases.Count > 0)
        {
            TotalPurchaseValue = pastPurchases.Sum(p => p.NetAmt);
            AvgPurchaseValue = pastPurchases.Average(p => p.NetAmt);
            LastPurchaseDate = pastPurchases.First().BillDate;

            var historyList = pastPurchases.Take(25).Select(p => new CustomerPurchaseHistoryItem
            {
                Date = p.BillDate,
                VouNo = p.VouNo,
                ItemName = !string.IsNullOrWhiteSpace(p.SuppBillNo) ? $"Inv #{p.SuppBillNo}" : p.VouType,
                Qty = p.TotalQty > 0 ? (int)Math.Round(p.TotalQty) : 1,
                Amount = p.NetAmt
            }).ToList();

            CustomerPurchaseHistory.ReplaceAll(historyList);
        }
        else
        {
            TotalPurchaseValue = 0;
            AvgPurchaseValue = 0;
            LastPurchaseDate = null;
            CustomerPurchaseHistory.Clear();
        }

        OnPropertyChanged(nameof(HasCustomerHistory));
        OnPropertyChanged(nameof(HasNoCustomerHistory));
    }

    private void ApplyAutoRoundOff()
    {
        _autoRoundOff = true;
        OnPropertyChanged(nameof(AutoRoundOff));
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        GrossAmt = Items.Sum(i => i.GrossAmt);
        TaxableAmount = Math.Max(0m, GrossAmt - DiscountAllowed);
        TaxAmt = Items.Sum(i => i.GstAmt);

        if (_autoRoundOff)
        {
            var unrounded = TaxableAmount + TaxAmt;
            var rounded = Math.Round(unrounded, MidpointRounding.AwayFromZero);
            _addLess = rounded - unrounded;
            OnPropertyChanged(nameof(AddLess));
            NetAmt = rounded;
        }
        else
        {
            NetAmt = Math.Round(TaxableAmount + TaxAmt + AddLess, 2);
        }

        TotalPaid = Payments.Sum(p => p.Amount);
        BalanceAmt = NetAmt - TotalPaid;

        OnPropertyChanged(nameof(TotalQty));
        OnPropertyChanged(nameof(TotalGrWt));
        OnPropertyChanged(nameof(TotalNtWt));
        OnPropertyChanged(nameof(TotalMetalValue));
        OnPropertyChanged(nameof(TotalMaking));
        OnPropertyChanged(nameof(TotalStoneValue));
    }

    private void ClearForm()
    {
        VouNo = string.Empty;
        SuppBillNo = string.Empty;
        SuppDate = DateTime.Today;
        BillDate = DateTime.Today;
        DueDate = DateTime.Today;
        _selectedSupplier = null;
        OnPropertyChanged(nameof(SelectedSupplier));
        AccountName = string.Empty;
        CustomerName = string.Empty;
        CustomerCode = string.Empty;
        Phone = string.Empty;
        Address = string.Empty;
        Address2 = string.Empty;
        City = "CUTTACK";
        Pincode = string.Empty;
        Email = string.Empty;
        VatNo = string.Empty;
        Check = false;
        VatType = "GSTIN";
        State = "21-ODISHA";
        Remarks = string.Empty;
        Narration = string.Empty;
        Note = string.Empty;
        Status = "Saved";
        DiscountAllowed = 0.00m;
        GrossAmt = 0.00m;
        TaxableAmount = 0.00m;
        TaxAmt = 0.00m;
        AddLess = 0.00m;
        AddLessNote = string.Empty;
        _autoRoundOff = true;
        OnPropertyChanged(nameof(AutoRoundOff));
        NetAmt = 0.00m;
        TotalPaid = 0.00m;
        BalanceAmt = 0.00m;

        CustomerGroup = "Sundry Creditors";
        TotalPurchaseValue = 0;
        AvgPurchaseValue = 0;
        LastPurchaseDate = null;
        CustomerPurchaseHistory.Clear();
        OnPropertyChanged(nameof(HasCustomerHistory));
        OnPropertyChanged(nameof(HasNoCustomerHistory));

        foreach (var item in Items)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }
        Items.Clear();

        foreach (var pay in Payments)
        {
            pay.PropertyChanged -= Payment_PropertyChanged;
        }
        Payments.Clear();

        RecalculateTotals();
    }

    public async Task OpenListAsync()
    {
        try
        {
            var regVm = new PurchaseRegisterViewModel(_purchaseRepository, _currentUser);
            var win = new PurchaseRegisterWindow(regVm);
            win.SetOwnerSafely();

            if (win.ShowDialog() == true && win.SelectedPurchase != null)
            {
                var selectedVouNo = win.SelectedPurchase.VouNo;
                if (!string.IsNullOrWhiteSpace(selectedVouNo))
                {
                    await LoadPurchaseAsync(selectedVouNo);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening purchase register: {ex.Message}";
        }
    }

    public void Print()
    {
        ShowToast("Print preview for Purchase invoice triggered.", ToastType.Info);
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}

public sealed class SupplierOption
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string VatNo { get; set; } = string.Empty;
    public string VatType { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PartyCode { get; set; } = string.Empty;
    public string HeadCategory { get; set; } = string.Empty;
}
