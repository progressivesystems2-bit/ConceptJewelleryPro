using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Services;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class TrialBalanceViewModel : ViewModelBase
{
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private readonly ITrialBalanceRepository _repository;

    private bool _isInitializing;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    private DateTime? _fromDate = new DateTime(2025, 4, 1);
    private DateTime? _toDate = DateTime.Today;
    private string _selectedHeadCategory = "ALL";
    private string _searchText = string.Empty;
    private bool _hideZeroBalances = true;
    private TrialBalanceViewMode _viewMode = TrialBalanceViewMode.GroupSummary;

    private TrialBalanceSummary _summary = new();
    private CancellationTokenSource? _loadCts;

    public TrialBalanceViewModel(ITrialBalanceRepository repository)
    {
        _repository = repository;

        RefreshCommand = new AsyncRelayCommand(async () => await LoadTrialBalanceAsync(0));
        ExportToExcelCommand = new RelayCommand(ExportToCsv);
        PrintCommand = new RelayCommand(PrintReport);
        OpenVoucherAuditCommand = new RelayCommand(OpenVoucherAudit);
        ToggleViewModeCommand = new RelayCommand(() =>
        {
            ViewMode = ViewMode == TrialBalanceViewMode.GroupSummary ? TrialBalanceViewMode.Detailed : TrialBalanceViewMode.GroupSummary;
        });

        _ = InitializeAsync();
    }

    public string Title => "Trial Balance";

    public ObservableCollection<string> HeadCategories { get; } = [];
    public ObservableCollection<TrialBalanceItem> Items { get; } = [];
    public ObservableCollection<TrialBalanceGroupSummary> GroupSummaries { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ExportToExcelCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand OpenVoucherAuditCommand { get; }
    public RelayCommand ToggleViewModeCommand { get; }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value) && !_isInitializing)
            {
                _ = LoadTrialBalanceAsync(100);
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
                _ = LoadTrialBalanceAsync(100);
            }
        }
    }

    public string SelectedHeadCategory
    {
        get => _selectedHeadCategory;
        set
        {
            if (SetProperty(ref _selectedHeadCategory, value) && !_isInitializing)
            {
                _ = LoadTrialBalanceAsync(50);
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value) && !_isInitializing)
            {
                _ = LoadTrialBalanceAsync(300);
            }
        }
    }

    public bool HideZeroBalances
    {
        get => _hideZeroBalances;
        set
        {
            if (SetProperty(ref _hideZeroBalances, value) && !_isInitializing)
            {
                _ = LoadTrialBalanceAsync(50);
            }
        }
    }

    public TrialBalanceViewMode ViewMode
    {
        get => _viewMode;
        set
        {
            if (SetProperty(ref _viewMode, value))
            {
                OnPropertyChanged(nameof(IsGroupView));
                OnPropertyChanged(nameof(IsDetailedView));
                OnPropertyChanged(nameof(ViewModeButtonText));
            }
        }
    }

    public bool IsGroupView => ViewMode == TrialBalanceViewMode.GroupSummary;
    public bool IsDetailedView => ViewMode == TrialBalanceViewMode.Detailed;
    public string ViewModeButtonText => IsGroupView ? "Switch to Detailed View" : "Switch to Group View";

    public TrialBalanceSummary Summary
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
            var categories = await _repository.GetHeadCategoriesAsync();
            HeadCategories.Clear();
            HeadCategories.Add("ALL");
            foreach (var c in categories)
            {
                HeadCategories.Add(c);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load categories: {ex.Message}", ToastType.Error);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
            _ = LoadTrialBalanceAsync(0);
        }
    }

    public async Task LoadTrialBalanceAsync(int delayMs = 0)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, token);
            }

            IsBusy = true;

            var filter = new TrialBalanceFilter
            {
                FromDate = FromDate,
                ToDate = ToDate,
                SelectedHeadCategory = SelectedHeadCategory,
                SearchText = SearchText?.Trim() ?? string.Empty,
                HideZeroBalances = HideZeroBalances,
                ViewMode = ViewMode
            };

            var data = await _repository.GetTrialBalanceAsync(filter, token);
            token.ThrowIfCancellationRequested();

            Items.Clear();
            foreach (var item in data)
            {
                Items.Add(item);
            }

            // Compute Group Summaries for reconciliation
            GroupSummaries.Clear();
            var grouped = data
                .GroupBy(x => string.IsNullOrWhiteSpace(x.HeadCategory) ? "Uncategorized" : x.HeadCategory.Trim())
                .OrderBy(g => g.Key);

            foreach (var g in grouped)
            {
                GroupSummaries.Add(new TrialBalanceGroupSummary
                {
                    HeadCategory = g.Key,
                    AccountCount = g.Count(x => x.HasActivityOrBalance),
                    OpeningDebit = g.Sum(x => x.OpeningDebit),
                    OpeningCredit = g.Sum(x => x.OpeningCredit),
                    PeriodDebit = g.Sum(x => x.PeriodDebit),
                    PeriodCredit = g.Sum(x => x.PeriodCredit),
                    ClosingDebit = g.Sum(x => x.ClosingDebit),
                    ClosingCredit = g.Sum(x => x.ClosingCredit)
                });
            }

            // Compute Summary
            var summary = new TrialBalanceSummary
            {
                TotalOpeningDebit = data.Sum(x => x.OpeningDebit),
                TotalOpeningCredit = data.Sum(x => x.OpeningCredit),
                TotalPeriodDebit = data.Sum(x => x.PeriodDebit),
                TotalPeriodCredit = data.Sum(x => x.PeriodCredit),
                TotalClosingDebit = data.Sum(x => x.ClosingDebit),
                TotalClosingCredit = data.Sum(x => x.ClosingCredit),
                TotalAccountCount = data.Count,
                ActiveAccountCount = data.Count(x => x.HasActivityOrBalance)
            };

            Summary = summary;
        }
        catch (OperationCanceledException)
        {
            // Cancelled when user types rapidly
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load Trial Balance: {ex.Message}", ToastType.Error);
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
            var doc = TrialBalanceFlowDocumentBuilder.CreateDocument(this);
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                IDocumentPaginatorSource paginator = doc;
                printDialog.PrintDocument(paginator.DocumentPaginator, $"Trial Balance - {FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}");
                ShowToast("Trial Balance sent to printer successfully.", ToastType.Success);
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
                FileName = $"TrialBalance_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("TRIAL BALANCE REPORT");
            sb.AppendLine($"Period: {FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}");
            sb.AppendLine($"Category Filter: {SelectedHeadCategory}");
            sb.AppendLine();
            sb.AppendLine("Particulars/Account,HeadCategory,SubCategory,OpeningDr,OpeningCr,PeriodDr,PeriodCr,ClosingDr,ClosingCr,NetClosing");

            foreach (var item in Items)
            {
                sb.AppendLine($"\"{EscapeCsv(item.AccountName)}\",\"{EscapeCsv(item.HeadCategory)}\",\"{EscapeCsv(item.SubCategory)}\",{item.OpeningDebit},{item.OpeningCredit},{item.PeriodDebit},{item.PeriodCredit},{item.ClosingDebit},{item.ClosingCredit},\"{item.FormattedNetClosing}\"");
            }

            sb.AppendLine();
            sb.AppendLine($"TOTAL,,,{Summary.TotalOpeningDebit},{Summary.TotalOpeningCredit},{Summary.TotalPeriodDebit},{Summary.TotalPeriodCredit},{Summary.TotalClosingDebit},{Summary.TotalClosingCredit}");
            sb.AppendLine($"Balancing Status: {Summary.BalancingStatusText}");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            ShowToast("Trial Balance exported to CSV successfully.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Export failed: {ex.Message}", ToastType.Error);
        }
    }

    private void OpenVoucherAudit()
    {
        try
        {
            var auditVm = new VoucherAuditViewModel(_repository, FromDate, ToDate);
            var window = new Views.VoucherAuditWindow(auditVm);
            window.SetOwnerSafely();
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to open Voucher Audit: {ex.Message}", ToastType.Error);
        }
    }

    private static string EscapeCsv(string? value) => (value ?? string.Empty).Replace("\"", "\"\"");

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
