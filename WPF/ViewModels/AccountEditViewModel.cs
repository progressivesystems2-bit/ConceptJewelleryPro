using System.Collections.ObjectModel;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class AccountEditViewModel : ViewModelBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly Account? _originalAccount;
    private readonly Account? _originalSnapshot;

    private string _headCategory = string.Empty;
    private string _subCategory = string.Empty;
    private string _accountName = string.Empty;
    private string _partyCode = string.Empty;
    private string _refName = string.Empty;
    private string _address = string.Empty;
    private string _address2 = string.Empty;
    private string _city = string.Empty;
    private string _pinCode = string.Empty;
    private string _state = string.Empty;
    private string _stdCode = string.Empty;
    private string _phoneNo = string.Empty;
    private string _mobile = string.Empty;
    private string _vatType = "GSTIN";
    private string _vatNo = string.Empty;
    private string _inOut = "INSIDE";
    private decimal? _opAmt1;
    private decimal? _opGoldWeight;
    private decimal? _creditLimit;
    private decimal? _creditDays;
    private decimal? _interest;
    private string _email = string.Empty;
    private string _bankAc = string.Empty;

    private bool _isBusy;
    private string? _errorMessage;
    private string? _statusMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public AccountEditViewModel(IAccountRepository accountRepository, string currentUser, Account? accountToEdit = null, IAuditRepository? auditRepository = null)
    {
        _accountRepository = accountRepository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;
        _originalAccount = accountToEdit;

        if (accountToEdit != null)
        {
            // Snapshot original values for audit logging
            _originalSnapshot = new Account
            {
                HeadCategory = accountToEdit.HeadCategory,
                SubCategory = accountToEdit.SubCategory,
                AccountName = accountToEdit.AccountName,
                PartyCode = accountToEdit.PartyCode,
                RefName = accountToEdit.RefName,
                Address = accountToEdit.Address,
                Address2 = accountToEdit.Address2,
                City = accountToEdit.City,
                PinCode = accountToEdit.PinCode,
                State = accountToEdit.State,
                StdCode = accountToEdit.StdCode,
                PhoneNo = accountToEdit.PhoneNo,
                Mobile = accountToEdit.Mobile,
                VatType = accountToEdit.VatType,
                VatNo = accountToEdit.VatNo,
                InOut = accountToEdit.InOut,
                OpAmt1 = accountToEdit.OpAmt1,
                OpGoldWeight = accountToEdit.OpGoldWeight,
                CreditLimit = accountToEdit.CreditLimit,
                CreditDays = accountToEdit.CreditDays,
                Interest = accountToEdit.Interest,
                Email = accountToEdit.Email,
                BankAc = accountToEdit.BankAc,
                User = accountToEdit.User
            };
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel);

        LoadDefaultHeadCategories();
        _ = LoadHeadCategoriesAsync();

        if (_originalAccount != null)
        {
            Title = $"Modify Account - {_originalAccount.AccountName}";
            PopulateFromAccount(_originalAccount);
        }
        else
        {
            Title = "Create New Account";
        }
    }

    public string Title { get; }

    public ObservableCollection<string> HeadCategories { get; } = new();
    public IReadOnlyList<string> InOutOptions { get; } = new[] { "INSIDE", "OUTSIDE" };
    public IReadOnlyList<string> VatTypes { get; } = new[] { "GSTIN", "TIN" };

    public string HeadCategory
    {
        get => _headCategory;
        set
        {
            if (SetProperty(ref _headCategory, value))
            {
                if (!string.IsNullOrWhiteSpace(value) && !HeadCategories.Contains(value))
                {
                    HeadCategories.Add(value);
                }
            }
        }
    }

    public string SubCategory
    {
        get => _subCategory;
        set => SetProperty(ref _subCategory, value);
    }

    public string AccountName
    {
        get => _accountName;
        set => SetProperty(ref _accountName, value);
    }

    public string PartyCode
    {
        get => _partyCode;
        set => SetProperty(ref _partyCode, value);
    }

    public string RefName
    {
        get => _refName;
        set => SetProperty(ref _refName, value);
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

    public string PinCode
    {
        get => _pinCode;
        set
        {
            if (SetProperty(ref _pinCode, value))
            {
                AutoloadLocationFromPincode(value);
            }
        }
    }

    public string State
    {
        get => _state;
        set => SetProperty(ref _state, PincodeService.MapStateName(value));
    }

    public string StdCode
    {
        get => _stdCode;
        set => SetProperty(ref _stdCode, value);
    }

    public string PhoneNo
    {
        get => _phoneNo;
        set => SetProperty(ref _phoneNo, value);
    }

    public string Mobile
    {
        get => _mobile;
        set => SetProperty(ref _mobile, value);
    }

    public string VatType
    {
        get => _vatType;
        set => SetProperty(ref _vatType, value);
    }

    public string VatNo
    {
        get => _vatNo;
        set => SetProperty(ref _vatNo, value);
    }

    public string InOut
    {
        get => _inOut;
        set => SetProperty(ref _inOut, value);
    }

    public decimal? OpAmt1
    {
        get => _opAmt1;
        set => SetProperty(ref _opAmt1, value);
    }

    public decimal? OpGoldWeight
    {
        get => _opGoldWeight;
        set => SetProperty(ref _opGoldWeight, value);
    }

    public decimal? CreditLimit
    {
        get => _creditLimit;
        set => SetProperty(ref _creditLimit, value);
    }

    public decimal? CreditDays
    {
        get => _creditDays;
        set => SetProperty(ref _creditDays, value);
    }

    public decimal? Interest
    {
        get => _interest;
        set => SetProperty(ref _interest, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string BankAc
    {
        get => _bankAc;
        set => SetProperty(ref _bankAc, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
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

    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event EventHandler<bool>? RequestClose;

    public void PopulateFromGstResult(Infrastructure.Services.GstLookupResult result)
    {
        AccountName = !string.IsNullOrWhiteSpace(result.TradeName) ? result.TradeName : result.LegalName;
        RefName = result.LegalName;
        Address = result.Address;
        Address2 = result.Address2;
        City = result.City;
        PinCode = result.PinCode;
        State = result.State;
        VatNo = result.Gstin;
        VatType = "GSTIN";
    }

    private void PopulateFromAccount(Account acc)
    {
        HeadCategory = acc.HeadCategory;
        SubCategory = acc.SubCategory;
        AccountName = acc.AccountName;
        PartyCode = acc.PartyCode;
        RefName = acc.RefName;
        Address = acc.Address;
        Address2 = acc.Address2;
        City = acc.City;
        PinCode = acc.PinCode;
        State = acc.State;
        StdCode = acc.StdCode;
        PhoneNo = acc.PhoneNo;
        Mobile = acc.Mobile;
        VatType = acc.VatType;
        VatNo = acc.VatNo;
        InOut = acc.InOut;
        OpAmt1 = acc.OpAmt1;
        OpGoldWeight = acc.OpGoldWeight;
        CreditLimit = acc.CreditLimit;
        CreditDays = acc.CreditDays;
        Interest = acc.Interest;
        Email = acc.Email;
        BankAc = acc.BankAc;
    }

    private void AutoloadLocationFromPincode(string? pincode)
    {
        if (string.IsNullOrWhiteSpace(pincode)) return;
        var clean = pincode.Trim();
        if (clean.Length == 6)
        {
            var res = PincodeService.Lookup(clean);
            if (res.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(res.Value.City)) City = res.Value.City;
                if (!string.IsNullOrWhiteSpace(res.Value.State)) State = res.Value.State;
            }
        }
    }

    private void LoadDefaultHeadCategories()
    {
        var defaults = new[]
        {
            "Advances & Deposits", "Approval Account", "Artisians", "Bank A/c", "Bank OD A/C",
            "Capital", "Cash in Hand", "Closing Stock", "Credit Card", "Creditors",
            "Current Assets", "Current Liabilities", "Debtors", "Deposits(Assets)", "Depreciation",
            "Direct Expenses", "Direct Incomes", "Duties & Taxes", "Equity Shares", "Fixed Assets",
            "Hire Account", "Indirect Expenses", "Indirect Incomes", "Intangible Asset", "Investments",
            "Loans Given", "Loans Taken", "Opening Stock", "OutStanding Liability", "Provisions",
            "Purchase Return", "Purchases", "Reserve & Surplus", "Sales", "Sales Return",
            "Suspense A/c", "Unsecured Loans"
        };

        HeadCategories.Clear();
        foreach (var cat in defaults)
        {
            HeadCategories.Add(cat);
        }

        if (!string.IsNullOrWhiteSpace(_headCategory) && !HeadCategories.Contains(_headCategory))
        {
            HeadCategories.Add(_headCategory);
        }
    }

    private async Task LoadHeadCategoriesAsync()
    {
        try
        {
            var categories = await _accountRepository.GetHeadCategoriesAsync();
            if (categories != null && categories.Count > 0)
            {
                if (System.Windows.Application.Current?.Dispatcher != null &&
                    !System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => UpdateCategoriesDirect(categories));
                }
                else
                {
                    UpdateCategoriesDirect(categories);
                }
            }
        }
        catch
        {
            // Keep default categories if database query fails
        }
    }

    private void UpdateCategoriesDirect(IReadOnlyList<string> categories)
    {
        HeadCategories.Clear();
        foreach (var cat in categories)
        {
            HeadCategories.Add(cat);
        }
        if (!string.IsNullOrWhiteSpace(_headCategory) && !HeadCategories.Contains(_headCategory))
        {
            HeadCategories.Add(_headCategory);
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(AccountName))
        {
            ErrorMessage = "Account Name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PartyCode))
        {
            ErrorMessage = "Party Code is required.";
            return;
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

        IsBusy = true;
        try
        {
            var isNew = _originalAccount == null;
            var acc = _originalAccount ?? new Account();

            acc.HeadCategory = HeadCategory;
            acc.SubCategory = SubCategory;
            acc.AccountName = AccountName;
            acc.PartyCode = PartyCode;
            acc.RefName = RefName;
            acc.Address = Address;
            acc.Address2 = Address2;
            acc.City = City;
            acc.PinCode = PinCode;
            acc.State = State;
            acc.StdCode = StdCode;
            acc.PhoneNo = PhoneNo;
            acc.Mobile = Mobile;
            acc.VatType = VatType;
            acc.VatNo = VatNo;
            acc.InOut = InOut;
            acc.OpAmt1 = OpAmt1 ?? 0m;
            acc.OpGoldWeight = OpGoldWeight ?? 0m;
            acc.CreditLimit = CreditLimit ?? 0m;
            acc.CreditDays = CreditDays ?? 0m;
            acc.Interest = Interest ?? 0m;
            acc.Email = Email;
            acc.BankAc = BankAc;
            acc.User = _currentUser;

            if (isNew)
            {
                await _accountRepository.AddAsync(acc);
            }
            else
            {
                await _accountRepository.UpdateAsync(acc);
                if (_auditRepository != null && _originalSnapshot != null)
                {
                    try
                    {
                        await Infrastructure.Services.AuditService.LogAccountModificationAsync(_auditRepository, _originalSnapshot, acc, _currentUser);
                    }
                    catch
                    {
                        // Non-blocking audit logging
                    }
                }
            }

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to save account: " + ex.Message;
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
