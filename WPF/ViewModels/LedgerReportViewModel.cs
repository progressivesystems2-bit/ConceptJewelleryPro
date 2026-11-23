using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Services;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum LedgerReportTab
{
    LedgerInRupees,
    LedgerInWeight
}

public sealed class LedgerReportViewModel : ViewModelBase
{
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private readonly ILedgerReportRepository _repository;
    private bool _isInitializing;
    private bool _isUpdatingSelection;

    private readonly List<Account> _allAccounts = [];
    private LedgerReportTab _selectedTab = LedgerReportTab.LedgerInRupees;
    private DateTime? _fromDate = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
    private DateTime? _toDate = DateTime.Today;

    private string _selectedAccountName = string.Empty;
    private string _selectedPhoneNo = string.Empty;
    private string _selectedRefName = string.Empty;
    private string _accountCategory = string.Empty;

    private int _currentAccountIndex = 0;
    private int _totalAccountsCount = 0;

    private LedgerPrintOption _selectedPrintOption = LedgerPrintOption.Running;

    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    private LedgerAccountSummary _summary = new();
    private LedgerEntry? _selectedEntry;

    public LedgerReportViewModel(ILedgerReportRepository repository)
    {
        _repository = repository;

        RefreshCommand = new AsyncRelayCommand(async () => await LoadReportAsync());
        ExportToExcelCommand = new RelayCommand(ExportToCsv);
        PrintCommand = new RelayCommand(PrintReport);
        OpenVoucherCommand = new RelayCommand(param => OpenVoucher(param as LedgerEntry ?? SelectedEntry));
        SelectTabCommand = new RelayCommand(param =>
        {
            if (param is LedgerReportTab tab)
            {
                SelectedTab = tab;
            }
            else if (param is string s && Enum.TryParse<LedgerReportTab>(s, true, out var parsedTab))
            {
                SelectedTab = parsedTab;
            }
        });

        FirstRecordCommand = new RelayCommand(NavigateFirst, () => CurrentAccountIndex > 1);
        PreviousRecordCommand = new RelayCommand(NavigatePrevious, () => CurrentAccountIndex > 1);
        NextRecordCommand = new RelayCommand(NavigateNext, () => CurrentAccountIndex < TotalAccountsCount);
        LastRecordCommand = new RelayCommand(NavigateLast, () => CurrentAccountIndex < TotalAccountsCount);

        _ = InitializeAsync();
    }

    public string Title => "Ledger Reports";

    public event EventHandler<LedgerEntry>? OpenVoucherRequested;

    public ObservableCollection<string> AccountNames { get; } = [];
    public ObservableCollection<string> PhoneNumbers { get; } = [];
    public ObservableCollection<string> ReferenceNames { get; } = [];

    public ObservableCollection<LedgerEntry> LedgerEntries { get; } = [];
    public ObservableCollection<LedgerEntry> WeightEntries { get; } = [];

    public LedgerEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ExportToExcelCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand SelectTabCommand { get; }
    public RelayCommand OpenVoucherCommand { get; }

    public RelayCommand FirstRecordCommand { get; }
    public RelayCommand PreviousRecordCommand { get; }
    public RelayCommand NextRecordCommand { get; }
    public RelayCommand LastRecordCommand { get; }

    public void OpenVoucher(LedgerEntry? entry = null)
    {
        var target = entry ?? SelectedEntry;
        if (target == null)
            return;

        if (string.IsNullOrWhiteSpace(target.VouNo) || string.Equals(target.VouType, "Opening Balance", StringComparison.OrdinalIgnoreCase))
        {
            ShowToast("No voucher associated with this line.", ToastType.Warning);
            return;
        }

        OpenVoucherRequested?.Invoke(this, target);
    }

    public LedgerReportTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsLedgerInRupeesActive));
                OnPropertyChanged(nameof(IsLedgerInWeightActive));
                OnPropertyChanged(nameof(IsRupeeGridVisible));
                OnPropertyChanged(nameof(IsWeightGridVisible));
                OnPropertyChanged(nameof(IsRupeeFooterVisible));
                OnPropertyChanged(nameof(IsWeightFooterVisible));

                if (!_isInitializing)
                {
                    _ = LoadReportAsync();
                }
            }
        }
    }

    public bool IsLedgerInRupeesActive => SelectedTab == LedgerReportTab.LedgerInRupees;
    public bool IsLedgerInWeightActive => SelectedTab == LedgerReportTab.LedgerInWeight;

    public bool IsRupeeGridVisible => SelectedTab == LedgerReportTab.LedgerInRupees;
    public bool IsWeightGridVisible => SelectedTab == LedgerReportTab.LedgerInWeight;

    public bool IsRupeeFooterVisible => SelectedTab == LedgerReportTab.LedgerInRupees;
    public bool IsWeightFooterVisible => SelectedTab == LedgerReportTab.LedgerInWeight;

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value) && !_isInitializing)
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
            if (SetProperty(ref _toDate, value) && !_isInitializing)
            {
                _ = LoadReportAsync();
            }
        }
    }

    public string SelectedAccountName
    {
        get => _selectedAccountName;
        set
        {
            if (SetProperty(ref _selectedAccountName, value))
            {
                if (!_isUpdatingSelection)
                {
                    SyncFromAccountName(value);
                }
                if (!_isInitializing)
                {
                    var isExact = _allAccounts.Any(a => string.Equals(a.AccountName, value, StringComparison.OrdinalIgnoreCase));
                    _ = DebouncedLoadReportAsync(isExact ? 80 : 350);
                }
            }
        }
    }

    public string SelectedPhoneNo
    {
        get => _selectedPhoneNo;
        set
        {
            if (SetProperty(ref _selectedPhoneNo, value))
            {
                if (!_isUpdatingSelection)
                {
                    SyncFromPhone(value);
                }
            }
        }
    }

    public string SelectedRefName
    {
        get => _selectedRefName;
        set
        {
            if (SetProperty(ref _selectedRefName, value))
            {
                if (!_isUpdatingSelection)
                {
                    SyncFromRefName(value);
                }
            }
        }
    }

    public string AccountCategory
    {
        get => _accountCategory;
        set => SetProperty(ref _accountCategory, value);
    }

    public int CurrentAccountIndex
    {
        get => _currentAccountIndex;
        set
        {
            if (SetProperty(ref _currentAccountIndex, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int TotalAccountsCount
    {
        get => _totalAccountsCount;
        set => SetProperty(ref _totalAccountsCount, value);
    }

    public LedgerPrintOption SelectedPrintOption
    {
        get => _selectedPrintOption;
        set
        {
            if (SetProperty(ref _selectedPrintOption, value))
            {
                OnPropertyChanged(nameof(IsOptionRunning));
                OnPropertyChanged(nameof(IsOptionRunningDayTotal));
                OnPropertyChanged(nameof(IsOptionWithNarration));
                OnPropertyChanged(nameof(IsOptionMonthWise));
                OnPropertyChanged(nameof(IsOptionWithAcknowledgment));
            }
        }
    }

    public bool IsOptionRunning
    {
        get => SelectedPrintOption == LedgerPrintOption.Running;
        set { if (value) SelectedPrintOption = LedgerPrintOption.Running; }
    }

    public bool IsOptionRunningDayTotal
    {
        get => SelectedPrintOption == LedgerPrintOption.RunningDayTotal;
        set { if (value) SelectedPrintOption = LedgerPrintOption.RunningDayTotal; }
    }

    public bool IsOptionWithNarration
    {
        get => SelectedPrintOption == LedgerPrintOption.WithNarration;
        set { if (value) SelectedPrintOption = LedgerPrintOption.WithNarration; }
    }

    public bool IsOptionMonthWise
    {
        get => SelectedPrintOption == LedgerPrintOption.MonthWise;
        set { if (value) SelectedPrintOption = LedgerPrintOption.MonthWise; }
    }

    public bool IsOptionWithAcknowledgment
    {
        get => SelectedPrintOption == LedgerPrintOption.WithAcknowledgment;
        set { if (value) SelectedPrintOption = LedgerPrintOption.WithAcknowledgment; }
    }

    public LedgerAccountSummary Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
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

    private async Task InitializeAsync()
    {
        _isInitializing = true;
        IsBusy = true;
        try
        {
            var accounts = await _repository.GetLedgerAccountsAsync();
            _allAccounts.Clear();
            _allAccounts.AddRange(accounts);

            AccountNames.Clear();
            PhoneNumbers.Clear();
            ReferenceNames.Clear();

            foreach (var acc in _allAccounts)
            {
                if (!string.IsNullOrWhiteSpace(acc.AccountName) && !AccountNames.Contains(acc.AccountName))
                    AccountNames.Add(acc.AccountName);

                if (!string.IsNullOrWhiteSpace(acc.PhoneNo) && !PhoneNumbers.Contains(acc.PhoneNo))
                    PhoneNumbers.Add(acc.PhoneNo);

                if (!string.IsNullOrWhiteSpace(acc.RefName) && !ReferenceNames.Contains(acc.RefName))
                    ReferenceNames.Add(acc.RefName);
            }

            TotalAccountsCount = _allAccounts.Count;

            if (_allAccounts.Count > 0)
            {
                var validDates = _allAccounts.Where(a => a.EntryDate.Year >= 2000).Select(a => a.EntryDate).ToList();
                if (validDates.Count > 0)
                {
                    var minYear = validDates.Min().Year;
                    _fromDate = new DateTime(minYear, 4, 1);
                    OnPropertyChanged(nameof(FromDate));
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to initialize ledger accounts: {ex.Message}", ToastType.Error);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
            _ = LoadReportAsync();
        }
    }

    public void ResetSelection()
    {
        _isUpdatingSelection = true;
        SelectedAccountName = string.Empty;
        SelectedPhoneNo = string.Empty;
        SelectedRefName = string.Empty;
        AccountCategory = string.Empty;
        CurrentAccountIndex = 0;
        _isUpdatingSelection = false;
        LedgerEntries.Clear();
        WeightEntries.Clear();
        Summary = new LedgerAccountSummary();
    }

    private void SyncFromAccountName(string accountName)
    {
        var acc = _allAccounts.FirstOrDefault(a => string.Equals(a.AccountName, accountName, StringComparison.OrdinalIgnoreCase));
        if (acc != null)
        {
            _isUpdatingSelection = true;
            SelectedPhoneNo = acc.PhoneNo;
            SelectedRefName = acc.RefName;
            AccountCategory = acc.HeadCategory;
            CurrentAccountIndex = _allAccounts.IndexOf(acc) + 1;
            _isUpdatingSelection = false;
        }
    }

    private void SyncFromPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        var acc = _allAccounts.FirstOrDefault(a => string.Equals(a.PhoneNo, phone, StringComparison.OrdinalIgnoreCase));
        if (acc != null)
        {
            _isUpdatingSelection = true;
            SelectedAccountName = acc.AccountName;
            SelectedRefName = acc.RefName;
            AccountCategory = acc.HeadCategory;
            CurrentAccountIndex = _allAccounts.IndexOf(acc) + 1;
            _isUpdatingSelection = false;
            if (!_isInitializing) _ = LoadReportAsync();
        }
    }

    private void SyncFromRefName(string refName)
    {
        if (string.IsNullOrWhiteSpace(refName)) return;
        var acc = _allAccounts.FirstOrDefault(a => string.Equals(a.RefName, refName, StringComparison.OrdinalIgnoreCase));
        if (acc != null)
        {
            _isUpdatingSelection = true;
            SelectedAccountName = acc.AccountName;
            SelectedPhoneNo = acc.PhoneNo;
            AccountCategory = acc.HeadCategory;
            CurrentAccountIndex = _allAccounts.IndexOf(acc) + 1;
            _isUpdatingSelection = false;
            if (!_isInitializing) _ = LoadReportAsync();
        }
    }

    private void NavigateFirst()
    {
        if (_allAccounts.Count > 0)
        {
            SelectAccountAtIndex(0);
        }
    }

    private void NavigatePrevious()
    {
        if (CurrentAccountIndex > 1)
        {
            SelectAccountAtIndex(CurrentAccountIndex - 2);
        }
    }

    private void NavigateNext()
    {
        if (CurrentAccountIndex < TotalAccountsCount)
        {
            SelectAccountAtIndex(CurrentAccountIndex);
        }
    }

    private void NavigateLast()
    {
        if (_allAccounts.Count > 0)
        {
            SelectAccountAtIndex(_allAccounts.Count - 1);
        }
    }

    private void SelectAccountAtIndex(int index)
    {
        if (index >= 0 && index < _allAccounts.Count)
        {
            var acc = _allAccounts[index];
            _isUpdatingSelection = true;
            SelectedAccountName = acc.AccountName;
            SelectedPhoneNo = acc.PhoneNo;
            SelectedRefName = acc.RefName;
            AccountCategory = acc.HeadCategory;
            CurrentAccountIndex = index + 1;
            _isUpdatingSelection = false;
            _ = LoadReportAsync();
        }
    }

    private CancellationTokenSource? _loadReportCts;

    public Task LoadReportAsync() => DebouncedLoadReportAsync(0);

    public async Task DebouncedLoadReportAsync(int delayMs = 300)
    {
        _loadReportCts?.Cancel();
        _loadReportCts = new CancellationTokenSource();
        var token = _loadReportCts.Token;

        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, token);
            }

            if (string.IsNullOrWhiteSpace(SelectedAccountName))
            {
                LedgerEntries.Clear();
                WeightEntries.Clear();
                Summary = new LedgerAccountSummary();
                return;
            }

            IsBusy = true;

            var filter = new LedgerReportFilter
            {
                AccountName = SelectedAccountName,
                PhoneNo = SelectedPhoneNo,
                RefName = SelectedRefName,
                FromDate = FromDate,
                ToDate = ToDate,
                PrintOption = SelectedPrintOption
            };

            var fromDt = FromDate ?? new DateTime(DateTime.Today.Year, 4, 1);
            var toDt = ToDate ?? DateTime.Today;

            var summaryTask = _repository.GetAccountSummaryAsync(SelectedAccountName, fromDt, toDt, token);

            if (SelectedTab == LedgerReportTab.LedgerInRupees)
            {
                var entries = await _repository.GetLedgerInRupeesAsync(filter, token);
                token.ThrowIfCancellationRequested();
                LedgerEntries.Clear();
                foreach (var e in entries)
                {
                    LedgerEntries.Add(e);
                }
            }
            else if (SelectedTab == LedgerReportTab.LedgerInWeight)
            {
                var weightEntries = await _repository.GetLedgerInWeightAsync(filter, token);
                token.ThrowIfCancellationRequested();
                WeightEntries.Clear();
                foreach (var e in weightEntries)
                {
                    WeightEntries.Add(e);
                }
            }

            Summary = await summaryTask;
            AccountCategory = Summary.Category;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancelled loads when typing
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load ledger report: {ex.Message}", ToastType.Error);
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    private void PrintReport()
    {
        try
        {
            var doc = LedgerFlowDocumentBuilder.CreateLedgerDocument(this);
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                IDocumentPaginatorSource paginator = doc;
                printDialog.PrintDocument(paginator.DocumentPaginator, $"Ledger - {SelectedAccountName}");
                ShowToast("Report sent to printer successfully.", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Print failed: {ex.Message}", ToastType.Error);
        }
    }

    private void ExportToCsv()
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"Ledger_{SelectedAccountName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() != true) return;

            var sb = new StringBuilder();

            if (SelectedTab == LedgerReportTab.LedgerInRupees)
            {
                sb.AppendLine($"Statement of Account (Ledger): {SelectedAccountName}");
                sb.AppendLine($"Period: {FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}");
                sb.AppendLine($"Category: {AccountCategory}, Phone: {SelectedPhoneNo}");
                sb.AppendLine();
                sb.AppendLine("VouDate,VouNo,VouType,Head/Particulars,Debits,Credits,Balance,Dr/Cr,Narration");

                foreach (var e in LedgerEntries)
                {
                    sb.AppendLine($"\"{e.FormattedDate}\",\"{e.VouNo}\",\"{e.VouType}\",\"{EscapeCsv(e.Head)}\",{e.Debit},{e.Credit},{e.Balance},\"{e.BalanceType}\",\"{EscapeCsv(e.Narration)}\"");
                }

                sb.AppendLine();
                sb.AppendLine($"Opening Balance,{Summary.OpeningAmount} {Summary.OpeningType}");
                sb.AppendLine($"Total Debits,{Summary.TotalDebits}");
                sb.AppendLine($"Total Credits,{Summary.TotalCredits}");
                sb.AppendLine($"Closing Balance,{Summary.ClosingBalance} {Summary.ClosingType}");
            }
            else if (SelectedTab == LedgerReportTab.LedgerInWeight)
            {
                sb.AppendLine($"Statement of Account (Weight Ledger): {SelectedAccountName}");
                sb.AppendLine($"Period: {FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}");
                sb.AppendLine();
                sb.AppendLine("VouDate,VouNo,VouType,Item/Particulars,WeightDebit(g),WeightCredit(g),WeightBalance(g),Dr/Cr,Narration");

                foreach (var e in WeightEntries)
                {
                    sb.AppendLine($"\"{e.FormattedDate}\",\"{e.VouNo}\",\"{e.VouType}\",\"{EscapeCsv(e.Head)}\",{e.WeightDebit},{e.WeightCredit},{e.WeightBalance},\"{e.WeightBalanceType}\",\"{EscapeCsv(e.Narration)}\"");
                }

                sb.AppendLine();
                sb.AppendLine($"Opening Weight,{Summary.OpeningGoldWeight} {Summary.OpeningWeightType}");
                sb.AppendLine($"Total Issued Weight,{Summary.TotalWeightIssued}");
                sb.AppendLine($"Total Received Weight,{Summary.TotalWeightReceived}");
                sb.AppendLine($"Closing Weight,{Summary.ClosingGoldWeight} {Summary.ClosingWeightType}");
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            ShowToast("Exported to CSV successfully.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Export failed: {ex.Message}", ToastType.Error);
        }
    }

    private static string EscapeCsv(string? value) => (value ?? string.Empty).Replace("\"", "\"\"");

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
