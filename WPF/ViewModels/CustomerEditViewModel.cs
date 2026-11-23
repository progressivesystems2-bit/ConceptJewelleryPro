using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class CustomerEditViewModel : ViewModelBase
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly Customer? _originalCustomer;
    private readonly Customer? _originalSnapshot;

    private string _initial = "Mr.";
    private string _code = string.Empty;
    private string _companyName = string.Empty;
    private string _address = string.Empty;
    private string _address2 = string.Empty;
    private string _city = string.Empty;
    private string _pincode = string.Empty;
    private string _group = "Retail";
    private int? _nos;
    private string _phone = string.Empty;
    private string _mobile = string.Empty;
    private string _email = string.Empty;
    private DateTime? _dob;
    private DateTime? _doa;
    private string _profession = string.Empty;
    private bool _print = true;
    private string _status = "Saved";

    private bool _isBusy;
    private string? _errorMessage;
    private string? _statusMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public CustomerEditViewModel(
        ICustomerRepository customerRepository,
        string currentUser,
        Customer? customerToEdit = null,
        IAuditRepository? auditRepository = null)
    {
        _customerRepository = customerRepository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;
        _originalCustomer = customerToEdit;

        if (customerToEdit != null)
        {
            _originalSnapshot = SnapshotCustomer(customerToEdit);
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel);

        if (_originalCustomer != null)
        {
            Title = $"Modify Customer - {_originalCustomer.CompanyName}";
            PopulateFromCustomer(_originalCustomer);
            Status = "Modify";
        }
        else
        {
            Title = "Create New Customer";
            Status = "Saved";
        }
    }

    public string Title { get; }

    public IReadOnlyList<string> Initials { get; } = new[] { "Mr.", "Mrs.", "Ms.", "Dr.", "M/s" };

    public IReadOnlyList<string> Groups { get; } = new[] { "Retail", "Wholesale", "VIP", "Regular", "Corporate" };

    public IReadOnlyList<string> StatusOptions { get; } = new[] { "Saved", "Modify" };

    public string Initial
    {
        get => _initial;
        set => SetProperty(ref _initial, value);
    }

    public string Code
    {
        get => _code;
        set
        {
            if (SetProperty(ref _code, value))
            {
                ClearError();
            }
        }
    }

    public string CompanyName
    {
        get => _companyName;
        set
        {
            if (SetProperty(ref _companyName, value))
            {
                ClearError();
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

    public string Group
    {
        get => _group;
        set => SetProperty(ref _group, value);
    }

    public int? Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string Mobile
    {
        get => _mobile;
        set => SetProperty(ref _mobile, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public DateTime? DOB
    {
        get => _dob;
        set => SetProperty(ref _dob, value);
    }

    public DateTime? DOA
    {
        get => _doa;
        set => SetProperty(ref _doa, value);
    }

    public string Profession
    {
        get => _profession;
        set => SetProperty(ref _profession, value);
    }

    public bool Print
    {
        get => _print;
        set => SetProperty(ref _print, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
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

    public void PopulateFromGstResult(GstLookupResult result)
    {
        CompanyName = !string.IsNullOrWhiteSpace(result.TradeName) ? result.TradeName : result.LegalName;
        Address = result.Address;
        Address2 = result.Address2;
        City = result.City;
        Pincode = result.PinCode;
        Group = "Corporate";
    }

    private void PopulateFromCustomer(Customer customer)
    {
        Initial = customer.Initial;
        Code = customer.Code;
        CompanyName = customer.CompanyName;
        Address = customer.Address;
        Address2 = customer.Address2;
        City = customer.City;
        Pincode = customer.Pincode;
        Group = customer.Group;
        Nos = customer.Nos;
        Phone = customer.Phone;
        Mobile = customer.Mobile;
        Email = customer.Email;
        DOB = customer.DOB;
        DOA = customer.DOA;
        Profession = customer.Profession;
        Print = customer.Print;
        Status = customer.Status;
    }

    private static Customer SnapshotCustomer(Customer c)
    {
        return new Customer
        {
            Initial = c.Initial,
            CompanyName = c.CompanyName,
            Address = c.Address,
            Address2 = c.Address2,
            City = c.City,
            Pincode = c.Pincode,
            Group = c.Group,
            Nos = c.Nos,
            Phone = c.Phone,
            Code = c.Code,
            Print = c.Print,
            Status = c.Status,
            Saved = c.Saved,
            User = c.User,
            Email = c.Email,
            Mobile = c.Mobile,
            DOB = c.DOB,
            DOA = c.DOA,
            Profession = c.Profession,
            Date = c.Date,
            Time = c.Time
        };
    }

    private void AutoloadLocationFromPincode(string? pincode)
    {
        if (string.IsNullOrWhiteSpace(pincode)) return;
        var clean = pincode.Trim();
        if (clean.Length == 6)
        {
            var res = PincodeService.Lookup(clean);
            if (res.HasValue && !string.IsNullOrWhiteSpace(res.Value.City))
            {
                City = res.Value.City;
            }
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(Code))
        {
            ErrorMessage = "Customer Code is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            ErrorMessage = "Customer / Company Name is required.";
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
            var isNew = _originalCustomer == null;
            var cust = _originalCustomer ?? new Customer();

            cust.Initial = Initial;
            cust.Code = Code.Trim();
            cust.CompanyName = CompanyName.Trim();
            cust.Address = Address;
            cust.Address2 = Address2;
            cust.City = City;
            cust.Pincode = Pincode;
            cust.Group = Group;
            cust.Nos = Nos ?? 0;
            cust.Phone = Phone;
            cust.Mobile = Mobile;
            cust.Email = Email;
            cust.DOB = DOB;
            cust.DOA = DOA;
            cust.Profession = Profession;
            cust.Print = Print;
            cust.Status = Status;
            cust.Saved = "Yes";
            cust.User = _currentUser;
            cust.Date = DateTime.Today;
            cust.Time = DateTime.Now.TimeOfDay;

            if (isNew)
            {
                await _customerRepository.AddAsync(cust);
            }
            else
            {
                await _customerRepository.UpdateAsync(cust);
                if (_auditRepository != null && _originalSnapshot != null)
                {
                    try
                    {
                        await AuditService.LogCustomerModificationAsync(_auditRepository, _originalSnapshot, cust, _currentUser);
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
            ErrorMessage = "Failed to save customer: " + ex.Message;
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

    private void ClearError()
    {
        ErrorMessage = null;
    }
}
