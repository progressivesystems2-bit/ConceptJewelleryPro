using System.Collections.ObjectModel;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Services;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum SchemeDueFilterType
{
    All,
    OverdueOnly,
    UpcomingOnly,
    DueThisWeek,
    PaidUpToDate
}

public sealed class SchemeDueListViewModel : ViewModelBase
{
    private readonly ISchemeDueRepository _schemeDueRepository;
    private readonly string _currentUser;

    private readonly List<SchemeDueListItem> _rawItems = new();
    private readonly RangeObservableCollection<SchemeDueListItem> _displayedItems = new();
    private readonly RangeObservableCollection<VoucherEntry> _selectedPaymentHistory = new();
    private readonly ObservableCollection<string> _staffList = new();

    private DateTime _asOnDate = DateTime.Today;
    private SchemeDueFilterType _filterType = SchemeDueFilterType.OverdueOnly;
    private string _selectedStaff = "All Staff";
    private string _searchText = string.Empty;
    private SchemeDueListItem? _selectedScheme;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    // KPI Metrics
    private int _totalActiveSchemesCount;
    private int _totalOverdueCount;
    private decimal _totalOverdueAmount;
    private int _totalUpcomingCount;
    private decimal _totalUpcomingAmount;
    private decimal _totalCollectionAmount;

    public SchemeDueListViewModel(
        ISchemeDueRepository schemeDueRepository,
        string currentUser = "admin")
    {
        _schemeDueRepository = schemeDueRepository;
        _currentUser = currentUser;

        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
        FilterAllCommand = new RelayCommand(() => SetFilter(SchemeDueFilterType.All));
        FilterOverdueCommand = new RelayCommand(() => SetFilter(SchemeDueFilterType.OverdueOnly));
        FilterUpcomingCommand = new RelayCommand(() => SetFilter(SchemeDueFilterType.UpcomingOnly));
        FilterThisWeekCommand = new RelayCommand(() => SetFilter(SchemeDueFilterType.DueThisWeek));
        FilterOnTrackCommand = new RelayCommand(() => SetFilter(SchemeDueFilterType.PaidUpToDate));

        CollectPaymentCommand = new RelayCommand(parameter => CollectPayment(parameter as SchemeDueListItem ?? SelectedScheme));
        PrintCommand = new RelayCommand(Print);
        ExportExcelCommand = new RelayCommand(ExportExcel);
        SendSmsReminderCommand = new RelayCommand(parameter => SendSmsReminder(parameter as SchemeDueListItem ?? SelectedScheme));

        _ = LoadDataAsync();
    }

    public event EventHandler<string>? CollectPaymentRequested;

    public string Title => "Scheme Due List";
    public string HeaderIcon => AppIcons.SchemeReceipts;
    public string CurrentUsername => _currentUser;

    public RangeObservableCollection<SchemeDueListItem> DisplayedItems => _displayedItems;
    public RangeObservableCollection<VoucherEntry> SelectedPaymentHistory => _selectedPaymentHistory;
    public ObservableCollection<string> StaffList => _staffList;

    public ICommand RefreshCommand { get; }
    public ICommand FilterAllCommand { get; }
    public ICommand FilterOverdueCommand { get; }
    public ICommand FilterUpcomingCommand { get; }
    public ICommand FilterThisWeekCommand { get; }
    public ICommand FilterOnTrackCommand { get; }
    public ICommand CollectPaymentCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand SendSmsReminderCommand { get; }

    public DateTime AsOnDate
    {
        get => _asOnDate;
        set
        {
            if (SetProperty(ref _asOnDate, value))
            {
                _ = LoadDataAsync();
            }
        }
    }

    public SchemeDueFilterType FilterType
    {
        get => _filterType;
        set
        {
            if (SetProperty(ref _filterType, value))
            {
                ApplyFilter();
                OnPropertyChanged(nameof(IsAllSelected));
                OnPropertyChanged(nameof(IsOverdueSelected));
                OnPropertyChanged(nameof(IsUpcomingSelected));
                OnPropertyChanged(nameof(IsThisWeekSelected));
                OnPropertyChanged(nameof(IsOnTrackSelected));
            }
        }
    }

    public bool IsAllSelected => FilterType == SchemeDueFilterType.All;
    public bool IsOverdueSelected => FilterType == SchemeDueFilterType.OverdueOnly;
    public bool IsUpcomingSelected => FilterType == SchemeDueFilterType.UpcomingOnly;
    public bool IsThisWeekSelected => FilterType == SchemeDueFilterType.DueThisWeek;
    public bool IsOnTrackSelected => FilterType == SchemeDueFilterType.PaidUpToDate;

    public string SelectedStaff
    {
        get => _selectedStaff;
        set
        {
            if (SetProperty(ref _selectedStaff, value))
            {
                ApplyFilter();
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

    public SchemeDueListItem? SelectedScheme
    {
        get => _selectedScheme;
        set
        {
            if (SetProperty(ref _selectedScheme, value))
            {
                _ = LoadPaymentHistoryAsync(value?.Scode);
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
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

    public int TotalActiveSchemesCount
    {
        get => _totalActiveSchemesCount;
        private set => SetProperty(ref _totalActiveSchemesCount, value);
    }

    public int TotalOverdueCount
    {
        get => _totalOverdueCount;
        private set => SetProperty(ref _totalOverdueCount, value);
    }

    public decimal TotalOverdueAmount
    {
        get => _totalOverdueAmount;
        private set => SetProperty(ref _totalOverdueAmount, value);
    }

    public int TotalUpcomingCount
    {
        get => _totalUpcomingCount;
        private set => SetProperty(ref _totalUpcomingCount, value);
    }

    public decimal TotalUpcomingAmount
    {
        get => _totalUpcomingAmount;
        private set => SetProperty(ref _totalUpcomingAmount, value);
    }

    public decimal TotalCollectionAmount
    {
        get => _totalCollectionAmount;
        private set => SetProperty(ref _totalCollectionAmount, value);
    }

    public void SetFilter(SchemeDueFilterType type)
    {
        FilterType = type;
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = "Analyzing scheme dues and payments...";
        try
        {
            var list = await Task.Run(async () => await _schemeDueRepository.GetSchemeDueListAsync(AsOnDate));

            _rawItems.Clear();
            _rawItems.AddRange(list);

            // Update staff list
            var staffs = _rawItems
                .Where(i => !string.IsNullOrWhiteSpace(i.Staff))
                .Select(i => i.Staff.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            _staffList.Clear();
            _staffList.Add("All Staff");
            foreach (var s in staffs)
            {
                _staffList.Add(s);
            }
            if (string.IsNullOrWhiteSpace(SelectedStaff) || !_staffList.Contains(SelectedStaff))
            {
                _selectedStaff = "All Staff";
                OnPropertyChanged(nameof(SelectedStaff));
            }

            RecalculateKPIMetrics();
            ApplyFilter();

            if (DisplayedItems.Count > 0 && SelectedScheme is null)
            {
                SelectedScheme = DisplayedItems[0];
            }
            else if (DisplayedItems.Count == 0)
            {
                _selectedPaymentHistory.Clear();
            }

            StatusMessage = $"Loaded {_rawItems.Count} schemes.";
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load scheme dues: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RecalculateKPIMetrics()
    {
        var activeSchemes = _rawItems.Where(i => !i.Closed).ToList();
        TotalActiveSchemesCount = activeSchemes.Count;

        var overdueSchemes = activeSchemes.Where(i => i.OverdueCount > 0).ToList();
        TotalOverdueCount = overdueSchemes.Count;
        TotalOverdueAmount = overdueSchemes.Sum(i => i.OverdueAmount);

        var upcomingSchemes = activeSchemes.Where(i => i.OverdueCount == 0 && i.NextDueDate.HasValue && i.NextDueDate.Value <= AsOnDate.AddDays(30) && i.PaidCount < i.TotalMonths).ToList();
        TotalUpcomingCount = upcomingSchemes.Count;
        TotalUpcomingAmount = upcomingSchemes.Sum(i => i.InstallmentAmt);

        TotalCollectionAmount = _rawItems.Sum(i => i.TotalPaidAmt);
    }

    private void ApplyFilter()
    {
        var query = _rawItems.AsEnumerable();

        // 1. Staff filter
        if (!string.IsNullOrWhiteSpace(SelectedStaff) && !SelectedStaff.Equals("All Staff", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => string.Equals(i.Staff, SelectedStaff.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // 2. Status / Category filter
        switch (FilterType)
        {
            case SchemeDueFilterType.OverdueOnly:
                query = query.Where(i => !i.Closed && i.OverdueCount > 0);
                break;
            case SchemeDueFilterType.UpcomingOnly:
                query = query.Where(i => !i.Closed && i.PaidCount < i.TotalMonths && i.NextDueDate.HasValue && i.NextDueDate.Value <= AsOnDate.AddDays(30) && i.OverdueCount == 0);
                break;
            case SchemeDueFilterType.DueThisWeek:
                query = query.Where(i => !i.Closed && i.PaidCount < i.TotalMonths && i.NextDueDate.HasValue && i.NextDueDate.Value <= AsOnDate.AddDays(7));
                break;
            case SchemeDueFilterType.PaidUpToDate:
                query = query.Where(i => i.OverdueCount == 0 && i.PaidCount > 0);
                break;
            case SchemeDueFilterType.All:
            default:
                break;
        }

        // 3. Search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            query = query.Where(i =>
                i.Scode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Phone.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Address.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.City.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Staff.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        // 4. Order by Overdue amount descending, then NextDueDate
        var result = query
            .OrderByDescending(i => i.OverdueCount > 0)
            .ThenByDescending(i => i.OverdueAmount)
            .ThenBy(i => i.NextDueDate)
            .ToList();

        _displayedItems.ReplaceAll(result);
    }

    private async Task LoadPaymentHistoryAsync(string? scode)
    {
        if (string.IsNullOrWhiteSpace(scode))
        {
            _selectedPaymentHistory.Clear();
            return;
        }

        try
        {
            var history = await _schemeDueRepository.GetSchemePaymentHistoryAsync(scode.Trim());
            _selectedPaymentHistory.ReplaceAll(history);
        }
        catch
        {
            _selectedPaymentHistory.Clear();
        }
    }

    private void CollectPayment(SchemeDueListItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Scode))
        {
            ShowToast("Please select a scheme first.", ToastType.Warning);
            return;
        }

        CollectPaymentRequested?.Invoke(this, item.Scode);
    }

    private void SendSmsReminder(SchemeDueListItem? item)
    {
        if (item is null) return;

        if (string.IsNullOrWhiteSpace(item.Phone))
        {
            ShowToast($"No mobile number recorded for {item.CustomerName}.", ToastType.Warning);
            return;
        }

        var message = item.OverdueCount > 0
            ? $"Dear {item.CustomerName}, your scheme ({item.Scode}) has {item.OverdueCount} overdue installment(s) of Rs. {item.OverdueAmount:N2}. Please visit to clear your dues."
            : $"Dear {item.CustomerName}, your scheme ({item.Scode}) next installment of Rs. {item.InstallmentAmt:N2} is due on {item.NextDueDate:dd/MM/yyyy}.";

        ShowToast($"SMS reminder generated for {item.CustomerName} ({item.Phone}).", ToastType.Success);
    }

    private void Print()
    {
        ShowToast("Print preview for Scheme Due List initiated.", ToastType.Info);
    }

    private void ExportExcel()
    {
        ShowToast($"Exporting {DisplayedItems.Count} scheme records to Excel...", ToastType.Info);
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
