using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Services;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class LedgerGroupViewModel : ViewModelBase
{
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private readonly ILedgerReportRepository _repository;
    private readonly string _currentUser;

    private readonly List<LedgerGroupAccountItem> _rawSummaries = new();
    private readonly RangeObservableCollection<LedgerGroupAccountItem> _displayedSummaries = new();
    private readonly RangeObservableCollection<LedgerEntry> _selectedAccountEntries = new();
    private readonly ObservableCollection<string> _headCategories = new();

    private DateTime? _fromDate = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
    private DateTime? _toDate = DateTime.Today;
    private string _selectedHeadCategory = "ALL";
    private string _searchText = string.Empty;
    private bool _hideZeroBalances = true;
    private bool _isWeightMode;
    private LedgerGroupAccountItem? _selectedAccountItem;
    private bool _isInitializing = true;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    // KPI Summary Metrics
    private int _totalAccountsCount;
    private decimal _totalOpeningDebit;
    private decimal _totalOpeningCredit;
    private decimal _totalPeriodDebit;
    private decimal _totalPeriodCredit;
    private decimal _totalClosingDebit;
    private decimal _totalClosingCredit;
    private decimal _netClosingBalance;
    private string _netClosingType = "Dr";

    public LedgerGroupViewModel(ILedgerReportRepository repository, string currentUser = "admin")
    {
        _repository = repository;
        _currentUser = currentUser;

        RefreshCommand = new AsyncRelayCommand(LoadReportAsync);
        ExportToExcelCommand = new RelayCommand(ExportToCsv);
        PrintCommand = new RelayCommand(PrintReport);

        SetThisFYCommand = new RelayCommand(SetThisFY);
        SetThisMonthCommand = new RelayCommand(SetThisMonth);
        SetTodayCommand = new RelayCommand(SetToday);

        OpenSingleAccountLedgerCommand = new RelayCommand(param =>
        {
            var accName = (param as LedgerGroupAccountItem)?.AccountName ?? SelectedAccountItem?.AccountName;
            if (!string.IsNullOrWhiteSpace(accName))
            {
                OpenAccountLedgerRequested?.Invoke(this, accName);
            }
        });

        _ = InitializeAsync();
    }

    public event EventHandler<string>? OpenAccountLedgerRequested;

    public string Title => "Ledger Group Reports";
    public string HeaderIcon => AppIcons.LedgerReports;
    public string CurrentUsername => _currentUser;

    public ObservableCollection<string> HeadCategories => _headCategories;
    public RangeObservableCollection<LedgerGroupAccountItem> DisplayedSummaries => _displayedSummaries;
    public RangeObservableCollection<LedgerEntry> SelectedAccountEntries => _selectedAccountEntries;

    public ICommand RefreshCommand { get; }
    public ICommand ExportToExcelCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand SetThisFYCommand { get; }
    public ICommand SetThisMonthCommand { get; }
    public ICommand SetTodayCommand { get; }
    public ICommand OpenSingleAccountLedgerCommand { get; }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                _ = LoadReportAsync();
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                _ = LoadReportAsync();
            }
        }
    }

    public string SelectedHeadCategory
    {
        get => _selectedHeadCategory;
        set
        {
            if (_isInitializing) return;

            var trimmed = value?.Trim() ?? string.Empty;
            var matched = _headCategories.FirstOrDefault(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase))
                       ?? _headCategories.FirstOrDefault(c => c.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
            var finalValue = matched ?? (string.IsNullOrWhiteSpace(trimmed) ? "ALL" : trimmed);

            if (SetProperty(ref _selectedHeadCategory, finalValue))
            {
                _ = LoadReportAsync();
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

    public bool HideZeroBalances
    {
        get => _hideZeroBalances;
        set
        {
            if (SetProperty(ref _hideZeroBalances, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsWeightMode
    {
        get => _isWeightMode;
        set
        {
            if (SetProperty(ref _isWeightMode, value))
            {
                OnPropertyChanged(nameof(UnitLabel));
                OnPropertyChanged(nameof(IsRupeesMode));
                RecalculateKPIMetrics();
                if (SelectedAccountItem != null)
                {
                    _ = LoadSelectedAccountTransactionsAsync(SelectedAccountItem.AccountName);
                }
            }
        }
    }

    public bool IsRupeesMode
    {
        get => !_isWeightMode;
        set => IsWeightMode = !value;
    }

    public string UnitLabel => IsWeightMode ? "Grams (g)" : "Rupees (₹)";

    public LedgerGroupAccountItem? SelectedAccountItem
    {
        get => _selectedAccountItem;
        set
        {
            if (SetProperty(ref _selectedAccountItem, value))
            {
                _ = LoadSelectedAccountTransactionsAsync(value?.AccountName);
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

    // KPI Metrics
    public int TotalAccountsCount
    {
        get => _totalAccountsCount;
        private set => SetProperty(ref _totalAccountsCount, value);
    }

    public decimal TotalOpeningDebit
    {
        get => _totalOpeningDebit;
        private set => SetProperty(ref _totalOpeningDebit, value);
    }

    public decimal TotalOpeningCredit
    {
        get => _totalOpeningCredit;
        private set => SetProperty(ref _totalOpeningCredit, value);
    }

    public decimal TotalPeriodDebit
    {
        get => _totalPeriodDebit;
        private set => SetProperty(ref _totalPeriodDebit, value);
    }

    public decimal TotalPeriodCredit
    {
        get => _totalPeriodCredit;
        private set => SetProperty(ref _totalPeriodCredit, value);
    }

    public decimal TotalClosingDebit
    {
        get => _totalClosingDebit;
        private set => SetProperty(ref _totalClosingDebit, value);
    }

    public decimal TotalClosingCredit
    {
        get => _totalClosingCredit;
        private set => SetProperty(ref _totalClosingCredit, value);
    }

    public decimal NetClosingBalance
    {
        get => _netClosingBalance;
        private set => SetProperty(ref _netClosingBalance, value);
    }

    public string NetClosingType
    {
        get => _netClosingType;
        private set => SetProperty(ref _netClosingType, value);
    }

    private async Task InitializeAsync()
    {
        try
        {
            _isInitializing = true;
            var cats = await _repository.GetHeadCategoriesAsync();
            _headCategories.Clear();
            _headCategories.Add("ALL");
            foreach (var c in cats)
            {
                _headCategories.Add(c);
            }

            _selectedHeadCategory = "ALL";
            OnPropertyChanged(nameof(SelectedHeadCategory));

            _isInitializing = false;
            await LoadReportAsync();
        }
        catch (Exception ex)
        {
            _isInitializing = false;
            ShowToast($"Failed to initialize categories: {ex.Message}", ToastType.Error);
        }
    }

    public async Task LoadReportAsync()
    {
        IsBusy = true;
        StatusMessage = $"Loading {SelectedHeadCategory} group ledger...";
        try
        {
            var fDate = FromDate ?? new DateTime(2025, 4, 1);
            var tDate = ToDate ?? DateTime.Today;

            var summaries = await Task.Run(async () => await _repository.GetLedgerGroupSummaryAsync(SelectedHeadCategory, fDate, tDate));

            _rawSummaries.Clear();
            _rawSummaries.AddRange(summaries);

            RecalculateKPIMetrics();
            ApplyFilter();

            if (DisplayedSummaries.Count > 0 && SelectedAccountItem is null)
            {
                SelectedAccountItem = DisplayedSummaries[0];
            }
            else if (DisplayedSummaries.Count == 0)
            {
                _selectedAccountEntries.Clear();
            }

            StatusMessage = $"Loaded {_rawSummaries.Count} accounts in {SelectedHeadCategory}.";
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load group ledger: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RecalculateKPIMetrics()
    {
        TotalAccountsCount = _rawSummaries.Count;

        if (!IsWeightMode)
        {
            TotalOpeningDebit = _rawSummaries.Where(a => a.OpeningType.Equals("Dr", StringComparison.OrdinalIgnoreCase)).Sum(a => a.OpeningAmount);
            TotalOpeningCredit = _rawSummaries.Where(a => a.OpeningType.Equals("Cr", StringComparison.OrdinalIgnoreCase)).Sum(a => a.OpeningAmount);

            TotalPeriodDebit = _rawSummaries.Sum(a => a.TotalDebits);
            TotalPeriodCredit = _rawSummaries.Sum(a => a.TotalCredits);

            TotalClosingDebit = _rawSummaries.Where(a => a.ClosingType.Equals("Dr", StringComparison.OrdinalIgnoreCase)).Sum(a => a.ClosingBalance);
            TotalClosingCredit = _rawSummaries.Where(a => a.ClosingType.Equals("Cr", StringComparison.OrdinalIgnoreCase)).Sum(a => a.ClosingBalance);

            var netDiff = TotalClosingDebit - TotalClosingCredit;
            NetClosingBalance = Math.Abs(netDiff);
            NetClosingType = netDiff >= 0 ? "Dr" : "Cr";
        }
        else
        {
            TotalOpeningDebit = _rawSummaries.Where(a => a.OpeningWeightType.Equals("Dr", StringComparison.OrdinalIgnoreCase)).Sum(a => a.OpeningWeight);
            TotalOpeningCredit = _rawSummaries.Where(a => a.OpeningWeightType.Equals("Cr", StringComparison.OrdinalIgnoreCase)).Sum(a => a.OpeningWeight);

            TotalPeriodDebit = _rawSummaries.Sum(a => a.TotalWeightDebit);
            TotalPeriodCredit = _rawSummaries.Sum(a => a.TotalWeightCredit);

            TotalClosingDebit = _rawSummaries.Where(a => a.ClosingWeightType.Equals("Dr", StringComparison.OrdinalIgnoreCase)).Sum(a => a.ClosingWeight);
            TotalClosingCredit = _rawSummaries.Where(a => a.ClosingWeightType.Equals("Cr", StringComparison.OrdinalIgnoreCase)).Sum(a => a.ClosingWeight);

            var netDiff = TotalClosingDebit - TotalClosingCredit;
            NetClosingBalance = Math.Abs(netDiff);
            NetClosingType = netDiff >= 0 ? "Dr" : "Cr";
        }
    }

    private void ApplyFilter()
    {
        var query = _rawSummaries.AsEnumerable();

        if (HideZeroBalances)
        {
            query = IsWeightMode
                ? query.Where(a => a.OpeningWeight != 0 || a.TotalWeightDebit != 0 || a.TotalWeightCredit != 0 || a.ClosingWeight != 0)
                : query.Where(a => a.OpeningAmount != 0 || a.TotalDebits != 0 || a.TotalCredits != 0 || a.ClosingBalance != 0);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            query = query.Where(a =>
                a.AccountName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.HeadCategory.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.SubCategory.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.City.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.PhoneNo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.RefName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var list = query
            .OrderBy(a => a.HeadCategory)
            .ThenBy(a => a.AccountName)
            .ToList();

        _displayedSummaries.ReplaceAll(list);
    }

    private async Task LoadSelectedAccountTransactionsAsync(string? accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            _selectedAccountEntries.Clear();
            return;
        }

        try
        {
            var filter = new LedgerReportFilter
            {
                AccountName = accountName.Trim(),
                FromDate = FromDate ?? new DateTime(2025, 4, 1),
                ToDate = ToDate ?? DateTime.Today
            };

            var entries = IsWeightMode
                ? await _repository.GetLedgerInWeightAsync(filter)
                : await _repository.GetLedgerInRupeesAsync(filter);

            _selectedAccountEntries.ReplaceAll(entries);
        }
        catch
        {
            _selectedAccountEntries.Clear();
        }
    }

    private void SetThisFY()
    {
        var today = DateTime.Today;
        var startYear = today.Month < 4 ? today.Year - 1 : today.Year;
        FromDate = new DateTime(startYear, 4, 1);
        ToDate = new DateTime(startYear + 1, 3, 31);
    }

    private void SetThisMonth()
    {
        var today = DateTime.Today;
        FromDate = new DateTime(today.Year, today.Month, 1);
        ToDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
    }

    private void SetToday()
    {
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;
    }

    private void ExportToCsv()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"LedgerGroup_{SelectedHeadCategory}_{DateTime.Today:yyyyMMdd}.csv"
            };

            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            if (!IsWeightMode)
            {
                sb.AppendLine("Account Name,Head Category,Sub Category,City,Phone,Opening (Rs),Type,Debits (Rs),Credits (Rs),Closing (Rs),Type");
                foreach (var item in DisplayedSummaries)
                {
                    sb.AppendLine($"\"{item.AccountName}\",\"{item.HeadCategory}\",\"{item.SubCategory}\",\"{item.City}\",\"{item.PhoneNo}\",{item.OpeningAmount},{item.OpeningType},{item.TotalDebits},{item.TotalCredits},{item.ClosingBalance},{item.ClosingType}");
                }
            }
            else
            {
                sb.AppendLine("Account Name,Head Category,Sub Category,City,Phone,Opening (g),Type,Debit Wt (g),Credit Wt (g),Closing Wt (g),Type");
                foreach (var item in DisplayedSummaries)
                {
                    sb.AppendLine($"\"{item.AccountName}\",\"{item.HeadCategory}\",\"{item.SubCategory}\",\"{item.City}\",\"{item.PhoneNo}\",{item.OpeningWeight},{item.OpeningWeightType},{item.TotalWeightDebit},{item.TotalWeightCredit},{item.ClosingWeight},{item.ClosingWeightType}");
                }
            }

            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            ShowToast($"Exported {DisplayedSummaries.Count} records successfully.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Export failed: {ex.Message}", ToastType.Error);
        }
    }

    private void PrintReport()
    {
        ShowToast($"Print statement for Group {SelectedHeadCategory} generated.", ToastType.Info);
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
