using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class SchemeInstallmentRow
{
    public string Title { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
}

public sealed class SchemeCreationViewModel : ViewModelBase, IConfirmCloseDocument
{
    private readonly ISchemeCreationRepository _repository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IStaffRepository _staffRepository;
    private readonly IAccountRepository? _accountRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;
    private readonly List<Customer> _allCustomers = [];

    private KarigharFormMode _formMode = KarigharFormMode.View;
    private SchemeCreationItem? _selectedScheme;
    private SchemeCreationItem? _originalSchemeSnapshot;
    private bool _isBusy;
    private bool _suppressSync;
    private int _selectedTabIndex;
    private string _searchText = string.Empty;

    // Form fields
    private string _scode = string.Empty;
    private int _nos;
    private string _customerName = string.Empty;
    private decimal _installmentAmt = 0m;
    private decimal _month = 11m;
    private decimal _totalAmt = 0m;
    private decimal _schemeDiscount = 0m;
    private decimal _totalValue = 0m;
    private DateTime _sdate = DateTime.Today;
    private DateTime _edate = DateTime.Today.AddMonths(12).AddDays(-5);
    private bool _closed;
    private string _dob = string.Empty;
    private string _doa = string.Empty;
    private string _staff = string.Empty;

    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public SchemeCreationViewModel(
        ISchemeCreationRepository repository,
        ICustomerRepository customerRepository,
        IStaffRepository staffRepository,
        IAccountRepository? accountRepository,
        string currentUser = "admin",
        IAuditRepository? auditRepository = null)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _staffRepository = staffRepository;
        _accountRepository = accountRepository;
        _auditRepository = auditRepository;
        _currentUser = currentUser;

        NewCommand = new AsyncRelayCommand(NewSchemeAsync, () => CanNew);
        SaveCommand = new AsyncRelayCommand(SaveSchemeAsync, () => CanSave);
        ModifyCommand = new RelayCommand(ModifyScheme, () => CanModify);
        DeleteCommand = new AsyncRelayCommand(DeleteSchemeAsync, () => CanDelete);
        CancelCommand = new RelayCommand(CancelScheme, () => CanCancel);
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);

        _ = InitializeAsync();
    }

    public SchemeCreationViewModel(
        ISchemeCreationRepository repository,
        ICustomerRepository customerRepository,
        IStaffRepository staffRepository,
        string currentUser = "admin",
        IAuditRepository? auditRepository = null)
        : this(repository, customerRepository, staffRepository, null, currentUser, auditRepository)
    {
    }

    public string Title => "Scheme Creation";

    public event EventHandler? RequestFocusCustomer;

    public ObservableCollection<Account> Customers { get; } = [];
    public ObservableCollection<StaffMember> StaffList { get; } = [];
    public ObservableCollection<SchemeCreationItem> SchemeList { get; } = [];
    public ObservableCollection<SchemeCreationItem> FilteredSchemeList { get; } = [];
    public ObservableCollection<SchemeInstallmentRow> Installments { get; } = [];

    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand ModifyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

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
    public bool CanModify => FormMode == KarigharFormMode.View && !string.IsNullOrWhiteSpace(Scode) && !IsBusy;
    public bool CanDelete => FormMode == KarigharFormMode.View && !string.IsNullOrWhiteSpace(Scode) && !IsBusy;
    public bool CanCancel => FormMode != KarigharFormMode.View && !IsBusy;

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

    public SchemeCreationItem? SelectedScheme
    {
        get => _selectedScheme;
        set
        {
            if (SetProperty(ref _selectedScheme, value))
            {
                OnPropertyChanged(nameof(CanModify));
                OnPropertyChanged(nameof(CanDelete));
                RefreshCommands();

                if (!_suppressSync && value is not null)
                {
                    PopulateFromItem(value);
                    FormMode = KarigharFormMode.View;
                }
            }
        }
    }

    #region Form Properties

    public string Scode
    {
        get => _scode;
        set => SetProperty(ref _scode, value);
    }

    public int Nos
    {
        get => _nos;
        set => SetProperty(ref _nos, value);
    }

    public string CustomerName
    {
        get => _customerName;
        set
        {
            if (SetProperty(ref _customerName, value))
            {
                UpdateCustomerDates(value);
            }
        }
    }

    public decimal InstallmentAmt
    {
        get => _installmentAmt;
        set
        {
            if (SetProperty(ref _installmentAmt, value))
            {
                RecalculateAmounts();
            }
        }
    }

    public decimal Month
    {
        get => _month;
        set
        {
            if (SetProperty(ref _month, value))
            {
                RecalculateAmounts();
                RecalculateInstallmentSchedule();
            }
        }
    }

    public decimal TotalAmt
    {
        get => _totalAmt;
        set => SetProperty(ref _totalAmt, value);
    }

    public decimal SchemeDiscount
    {
        get => _schemeDiscount;
        set
        {
            if (SetProperty(ref _schemeDiscount, value))
            {
                TotalValue = TotalAmt + value;
            }
        }
    }

    public decimal TotalValue
    {
        get => _totalValue;
        set => SetProperty(ref _totalValue, value);
    }

    public DateTime SDate
    {
        get => _sdate;
        set
        {
            if (SetProperty(ref _sdate, value))
            {
                RecalculateInstallmentSchedule();
            }
        }
    }

    public DateTime EDate
    {
        get => _edate;
        set => SetProperty(ref _edate, value);
    }

    public bool Closed
    {
        get => _closed;
        set => SetProperty(ref _closed, value);
    }

    public string DOB
    {
        get => _dob;
        set => SetProperty(ref _dob, value);
    }

    public string DOA
    {
        get => _doa;
        set => SetProperty(ref _doa, value);
    }

    public string Staff
    {
        get => _staff;
        set => SetProperty(ref _staff, value);
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

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;

            var accountsTask = _accountRepository != null
                ? _accountRepository.GetAllAsync()
                : Task.FromResult<IReadOnlyList<Account>>([]);
            var customersTask = _customerRepository.GetAllAsync();
            var staffTask = _staffRepository.GetAllAsync();
            var schemesTask = _repository.GetAllAsync(100);

            await Task.WhenAll(accountsTask, customersTask, staffTask, schemesTask);

            var accounts = await accountsTask;
            Customers.Clear();
            var debtors = accounts
                .Where(a => !string.IsNullOrWhiteSpace(a.AccountName) && IsDebtorHeadGroup(a.HeadCategory))
                .OrderBy(a => a.AccountName, StringComparer.OrdinalIgnoreCase);

            foreach (var d in debtors)
            {
                Customers.Add(d);
            }

            var customers = await customersTask;
            _allCustomers.Clear();
            _allCustomers.AddRange(customers);

            var staff = await staffTask;
            StaffList.Clear();
            foreach (var s in staff)
            {
                StaffList.Add(s);
            }

            var schemes = await schemesTask;
            _suppressSync = true;
            SchemeList.Clear();
            foreach (var sc in schemes)
            {
                SchemeList.Add(sc);
            }
            _suppressSync = false;

            ApplyFilter();

            if (SchemeList.Count > 0)
            {
                SelectedScheme = SchemeList.First();
            }
            else
            {
                PrepareBlankForm();
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Initialization error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadDataAsync()
    {
        try
        {
            var schemes = await _repository.GetAllAsync(100);
            _suppressSync = true;
            SchemeList.Clear();
            foreach (var sc in schemes)
            {
                SchemeList.Add(sc);
            }
            _suppressSync = false;
            ApplyFilter();

            if (_accountRepository != null)
            {
                var accounts = await _accountRepository.GetAllAsync();
                Customers.Clear();
                var debtors = accounts
                    .Where(a => !string.IsNullOrWhiteSpace(a.AccountName) && IsDebtorHeadGroup(a.HeadCategory))
                    .OrderBy(a => a.AccountName, StringComparer.OrdinalIgnoreCase);

                foreach (var d in debtors)
                {
                    Customers.Add(d);
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load schemes: {ex.Message}", ToastType.Error);
        }
    }

    private static bool IsDebtorHeadGroup(string? headCategory)
    {
        if (string.IsNullOrWhiteSpace(headCategory))
            return false;

        var h = headCategory.Trim();
        return string.Equals(h, "Debtors", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(h, "Sundry Debtors", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(h, "Debtor", StringComparison.OrdinalIgnoreCase);
    }

    private async Task NewSchemeAsync()
    {
        try
        {
            IsBusy = true;
            var nextScode = await _repository.GetNextScodeAsync();
            var nextNos = await _repository.GetNextNosAsync();

            PrepareBlankForm();
            Scode = nextScode;
            Nos = nextNos;
            SDate = DateTime.Today;
            SelectedTabIndex = 0;

            _suppressSync = true;
            SelectedScheme = null;
            _suppressSync = false;

            FormMode = KarigharFormMode.Add;
            ShowToast($"Ready for new scheme: {Scode}", ToastType.Info);
            RequestFocusCustomer?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowToast($"Error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ModifyScheme()
    {
        if (string.IsNullOrWhiteSpace(Scode))
        {
            ShowToast("Select a scheme to modify.", ToastType.Warning);
            return;
        }

        _originalSchemeSnapshot = SelectedScheme != null ? SnapshotScheme(SelectedScheme) : null;
        FormMode = KarigharFormMode.Modify;
        SelectedTabIndex = 0;
        ShowToast($"Editing Scheme #{Scode}", ToastType.Info);
        RequestFocusCustomer?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveSchemeAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            ShowToast("Please enter or select a Customer Name.", ToastType.Warning);
            return;
        }

        if (InstallmentAmt <= 0)
        {
            ShowToast("Please enter a valid Installment Amount.", ToastType.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Do you want to save Scheme #{Scode} for {CustomerName}?",
            "Scheme Creation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var isUpdate = FormMode == KarigharFormMode.Modify;

            var item = new SchemeCreationItem
            {
                Scode = Scode.Trim(),
                Nos = Nos,
                CustomerName = CustomerName.Trim(),
                InstallmentAmt = InstallmentAmt,
                Month = Month,
                TotalAmt = TotalAmt,
                SchemeDiscount = SchemeDiscount,
                TotalValue = TotalValue,
                SDate = SDate,
                EDate = EDate,
                Closed = Closed,
                Staff = Staff?.Trim() ?? string.Empty,
                Status = Closed ? "Closed" : "Saved",
                EntryUser = string.IsNullOrWhiteSpace(_currentUser) ? "admin" : _currentUser,
                EntryDate = DateTime.Today,
                EntryTime = DateTime.Now,
                ComputerName = Environment.MachineName
            };

            await _repository.SaveAsync(item, isUpdate);

            if (isUpdate && _auditRepository != null)
            {
                try
                {
                    await Infrastructure.Services.AuditService.LogSchemeModificationAsync(_auditRepository, _originalSchemeSnapshot, item, _currentUser);
                }
                catch
                {
                    // Non-blocking audit logging
                }
            }

            ShowToast($"Scheme #{item.Scode} saved successfully!", ToastType.Success);

            await LoadDataAsync();

            _suppressSync = true;
            SelectedScheme = SchemeList.FirstOrDefault(x => x.Scode == item.Scode);
            _suppressSync = false;

            FormMode = KarigharFormMode.View;
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save scheme: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteSchemeAsync()
    {
        if (string.IsNullOrWhiteSpace(Scode))
        {
            ShowToast("Select a scheme to delete.", ToastType.Warning);
            return;
        }

        var schemeToDelete = SelectedScheme ?? new SchemeCreationItem { Scode = Scode, CustomerName = CustomerName, InstallmentAmt = InstallmentAmt, TotalValue = TotalValue };
        var result = MessageBox.Show(
            $"Are you sure you want to delete Scheme #{Scode}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            await _repository.DeleteAsync(Scode);

            if (_auditRepository != null)
            {
                try
                {
                    await Infrastructure.Services.AuditService.LogSchemeDeletionAsync(_auditRepository, schemeToDelete, _currentUser);
                }
                catch
                {
                    // Non-blocking audit logging
                }
            }

            ShowToast($"Scheme #{Scode} deleted.", ToastType.Success);

            await LoadDataAsync();
            PrepareBlankForm();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to delete: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static SchemeCreationItem SnapshotScheme(SchemeCreationItem s)
    {
        return new SchemeCreationItem
        {
            Scode = s.Scode,
            Nos = s.Nos,
            CustomerName = s.CustomerName,
            InstallmentAmt = s.InstallmentAmt,
            Month = s.Month,
            TotalAmt = s.TotalAmt,
            SchemeDiscount = s.SchemeDiscount,
            TotalValue = s.TotalValue,
            SDate = s.SDate,
            EDate = s.EDate,
            Closed = s.Closed,
            Staff = s.Staff,
            Status = s.Status,
            EntryUser = s.EntryUser
        };
    }

    private void CancelScheme()
    {
        if (SelectedScheme is not null)
        {
            PopulateFromItem(SelectedScheme);
        }
        else
        {
            PrepareBlankForm();
        }

        FormMode = KarigharFormMode.View;
        ShowToast("Cancelled.", ToastType.Info);
    }

    private void PopulateFromItem(SchemeCreationItem item)
    {
        Scode = item.Scode;
        Nos = item.Nos;
        CustomerName = item.CustomerName;
        InstallmentAmt = item.InstallmentAmt;
        Month = item.Month;
        TotalAmt = item.TotalAmt;
        SchemeDiscount = item.SchemeDiscount;
        TotalValue = item.TotalValue;
        SDate = item.SDate;
        EDate = item.EDate;
        Closed = item.Closed;
        Staff = item.Staff;

        UpdateCustomerDates(item.CustomerName);
        RecalculateInstallmentSchedule();
    }

    private void PrepareBlankForm()
    {
        Scode = string.Empty;
        Nos = 0;
        CustomerName = string.Empty;
        InstallmentAmt = 0m;
        Month = 11m;
        TotalAmt = 0m;
        SchemeDiscount = 0m;
        TotalValue = 0m;
        SDate = DateTime.Today;
        EDate = DateTime.Today.AddMonths(12).AddDays(-5);
        Closed = false;
        DOB = string.Empty;
        DOA = string.Empty;
        Staff = StaffList.FirstOrDefault()?.Staff ?? string.Empty;

        RecalculateInstallmentSchedule();
        FormMode = KarigharFormMode.View;
    }

    private void RecalculateAmounts()
    {
        TotalAmt = InstallmentAmt * Month;
        if (InstallmentAmt <= 0)
        {
            SchemeDiscount = 0m;
            TotalValue = 0m;
        }
        else
        {
            if (SchemeDiscount == 0)
            {
                SchemeDiscount = InstallmentAmt;
            }
            TotalValue = TotalAmt + SchemeDiscount;
        }
    }

    private void RecalculateInstallmentSchedule()
    {
        Installments.Clear();
        var count = Math.Clamp((int)Month, 1, 36);

        var ordinalNames = new[]
        {
            "First", "Second", "Third", "Fourth", "Fifth", "Sixth",
            "Seventh", "Eighth", "Ninth", "Tenth", "Eleventh", "Twelfth",
            "13th", "14th", "15th", "16th", "17th", "18th",
            "19th", "20th", "21st", "22nd", "23rd", "24th"
        };

        var curDate = SDate;
        for (var i = 0; i < count; i++)
        {
            var title = i < ordinalNames.Length ? ordinalNames[i] : $"{i + 1}th";
            var dueDate = (i == 0) ? SDate : SDate.AddMonths(i).AddDays(-1);

            Installments.Add(new SchemeInstallmentRow
            {
                Title = title,
                DueDate = dueDate.ToString("dd/MM/yyyy")
            });
        }

        EDate = SDate.AddMonths(count + 1).AddDays(-5);
    }

    private void UpdateCustomerDates(string customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            DOB = string.Empty;
            DOA = string.Empty;
            return;
        }

        var matched = _allCustomers.FirstOrDefault(c => string.Equals(c.CompanyName, customerName, StringComparison.OrdinalIgnoreCase));
        if (matched is not null)
        {
            DOB = matched.DOB.HasValue ? matched.DOB.Value.ToString("dd/MM/yyyy") : string.Empty;
            DOA = matched.DOA.HasValue ? matched.DOA.Value.ToString("dd/MM/yyyy") : string.Empty;
        }
        else
        {
            DOB = string.Empty;
            DOA = string.Empty;
        }
    }

    private void ApplyFilter()
    {
        FilteredSchemeList.Clear();
        var query = SearchText?.Trim();

        foreach (var item in SchemeList)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                item.Scode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Staff.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Status.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSchemeList.Add(item);
            }
        }
    }

    private void RefreshCommands()
    {
        NewCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastMessage = message;
        ToastType = type;
    }

    public bool HasUnsavedChanges()
    {
        return FormMode != KarigharFormMode.View ||
               !string.IsNullOrWhiteSpace(CustomerName) ||
               InstallmentAmt > 0 ||
               SchemeDiscount > 0;
    }
}
