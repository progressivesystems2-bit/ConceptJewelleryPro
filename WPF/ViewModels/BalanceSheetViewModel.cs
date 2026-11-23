using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Services;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class BalanceSheetViewModel : ViewModelBase
{
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private readonly IBalanceSheetRepository _repository;
    private readonly string _currentUser;

    private bool _isInitializing;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    private DateTime? _fromDate = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
    private DateTime? _toDate = DateTime.Today;

    private decimal? _manualClosingStock;
    private decimal? _manualClosingGoldWeight;
    private decimal? _manualClosingSilverWeight;

    private bool _hideZeroBalances = true;
    private BalanceSheetViewMode _viewMode = BalanceSheetViewMode.HorizontalTFormat;

    private BalanceSheetStatement _statement = new();
    private CancellationTokenSource? _loadCts;

    public BalanceSheetViewModel(IBalanceSheetRepository repository, string currentUser = "admin")
    {
        _repository = repository;
        _currentUser = currentUser;

        RefreshCommand = new AsyncRelayCommand(async () => await LoadBalanceSheetAsync(0));
        ExportToExcelCommand = new RelayCommand(ExportToCsv);
        PrintCommand = new RelayCommand(PrintReport);

        SetThisFYCommand = new RelayCommand(SetThisFY);
        SetLastFYCommand = new RelayCommand(SetLastFY);
        SetTodayCommand = new RelayCommand(SetToday);

        ToggleViewModeCommand = new RelayCommand(() =>
        {
            ViewMode = ViewMode == BalanceSheetViewMode.HorizontalTFormat
                ? BalanceSheetViewMode.VerticalSchedule
                : BalanceSheetViewMode.HorizontalTFormat;
        });

        OpenAccountLedgerCommand = new RelayCommand(param =>
        {
            var accName = (param as BalanceSheetItem)?.AccountName ?? (param as string);
            if (!string.IsNullOrWhiteSpace(accName) &&
                !accName.Contains("Profit & Loss", StringComparison.OrdinalIgnoreCase) &&
                !accName.Contains("Closing Stock", StringComparison.OrdinalIgnoreCase))
            {
                OpenAccountLedgerRequested?.Invoke(this, accName);
            }
        });

        _ = InitializeAsync();
    }

    public event EventHandler<string>? OpenAccountLedgerRequested;

    public string Title => "Balance Sheet";
    public string HeaderIcon => AppIcons.Reports;
    public string CurrentUsername => _currentUser;

    public ObservableCollection<BalanceSheetSection> LiabilitiesSections { get; } = [];
    public ObservableCollection<BalanceSheetSection> AssetsSections { get; } = [];
    public ObservableCollection<BalanceSheetItem> LiabilitiesItems { get; } = [];
    public ObservableCollection<BalanceSheetItem> AssetsItems { get; } = [];
    public ObservableCollection<BalanceSheetItem> AllItems { get; } = [];

    public ICommand RefreshCommand { get; }
    public ICommand ExportToExcelCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand SetThisFYCommand { get; }
    public ICommand SetLastFYCommand { get; }
    public ICommand SetTodayCommand { get; }
    public ICommand ToggleViewModeCommand { get; }
    public ICommand OpenAccountLedgerCommand { get; }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value) && !_isInitializing)
            {
                _ = LoadBalanceSheetAsync(100);
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
                _ = LoadBalanceSheetAsync(100);
            }
        }
    }

    public decimal? ManualClosingStock
    {
        get => _manualClosingStock;
        set
        {
            if (SetProperty(ref _manualClosingStock, value) && !_isInitializing)
            {
                _ = LoadBalanceSheetAsync(300);
            }
        }
    }

    public decimal? ManualClosingGoldWeight
    {
        get => _manualClosingGoldWeight;
        set
        {
            if (SetProperty(ref _manualClosingGoldWeight, value) && !_isInitializing)
            {
                _ = LoadBalanceSheetAsync(300);
            }
        }
    }

    public decimal? ManualClosingSilverWeight
    {
        get => _manualClosingSilverWeight;
        set
        {
            if (SetProperty(ref _manualClosingSilverWeight, value) && !_isInitializing)
            {
                _ = LoadBalanceSheetAsync(300);
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
                _ = LoadBalanceSheetAsync(50);
            }
        }
    }

    public BalanceSheetViewMode ViewMode
    {
        get => _viewMode;
        set
        {
            if (SetProperty(ref _viewMode, value))
            {
                OnPropertyChanged(nameof(IsHorizontalView));
                OnPropertyChanged(nameof(IsVerticalView));
                OnPropertyChanged(nameof(ViewModeButtonText));
            }
        }
    }

    public bool IsHorizontalView => ViewMode == BalanceSheetViewMode.HorizontalTFormat;
    public bool IsVerticalView => ViewMode == BalanceSheetViewMode.VerticalSchedule;
    public string ViewModeButtonText => IsHorizontalView ? "Switch to Vertical Schedule" : "Switch to Horizontal T-Format";

    public BalanceSheetStatement Statement
    {
        get => _statement;
        private set => SetProperty(ref _statement, value);
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

    private void SetThisFY()
    {
        var now = DateTime.Today;
        var startYear = now.Month < 4 ? now.Year - 1 : now.Year;
        FromDate = new DateTime(startYear, 4, 1);
        ToDate = new DateTime(startYear + 1, 3, 31) > now ? now : new DateTime(startYear + 1, 3, 31);
    }

    private void SetLastFY()
    {
        var now = DateTime.Today;
        var startYear = (now.Month < 4 ? now.Year - 1 : now.Year) - 1;
        FromDate = new DateTime(startYear, 4, 1);
        ToDate = new DateTime(startYear + 1, 3, 31);
    }

    private void SetToday()
    {
        FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate = DateTime.Today;
    }

    private async Task InitializeAsync()
    {
        _isInitializing = true;
        try
        {
            if (!_manualClosingStock.HasValue)
            {
                _manualClosingStock = await _repository.GetEstimatedClosingStockValueAsync(ToDate ?? DateTime.Today);
                OnPropertyChanged(nameof(ManualClosingStock));
            }
            await LoadBalanceSheetAsync(0);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    public async Task LoadBalanceSheetAsync(int debounceMs = 0)
    {
        if (debounceMs > 0)
        {
            await Task.Delay(debounceMs);
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsBusy = true;
        try
        {
            var filter = new BalanceSheetFilter
            {
                FromDate = FromDate,
                ToDate = ToDate,
                ManualClosingStockAmount = ManualClosingStock,
                ManualClosingGoldWeight = ManualClosingGoldWeight,
                ManualClosingSilverWeight = ManualClosingSilverWeight,
                HideZeroBalances = HideZeroBalances,
                ViewMode = ViewMode
            };

            var statement = await _repository.GetBalanceSheetAsync(filter, ct);
            if (ct.IsCancellationRequested) return;

            Statement = statement;

            LiabilitiesSections.Clear();
            LiabilitiesItems.Clear();
            AllItems.Clear();
            foreach (var sec in statement.LiabilitiesSections)
            {
                LiabilitiesSections.Add(sec);
                foreach (var item in sec.Items)
                {
                    LiabilitiesItems.Add(item);
                    AllItems.Add(item);
                }
            }

            AssetsSections.Clear();
            AssetsItems.Clear();
            foreach (var sec in statement.AssetsSections)
            {
                AssetsSections.Add(sec);
                foreach (var item in sec.Items)
                {
                    AssetsItems.Add(item);
                    AllItems.Add(item);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ShowToast($"Failed to load Balance Sheet: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PrintReport()
    {
        try
        {
            var doc = BalanceSheetFlowDocumentBuilder.CreateDocument(this);
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                doc.PageWidth = printDialog.PrintableAreaWidth;
                doc.PageHeight = printDialog.PrintableAreaHeight;
                doc.PagePadding = new System.Windows.Thickness(40);
                doc.ColumnGap = 0;
                doc.ColumnWidth = printDialog.PrintableAreaWidth;

                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, "Balance Sheet");
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
                Filter = "CSV (Comma delimited)|*.csv",
                FileName = $"BalanceSheet_{ToDate:yyyyMMdd}.csv",
                DefaultExt = ".csv"
            };

            if (sfd.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine($"BALANCE SHEET AS ON {ToDate:dd/MM/yyyy}");
            sb.AppendLine($"Period: {FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}");
            sb.AppendLine();

            sb.AppendLine("--- CAPITAL & LIABILITIES ---");
            sb.AppendLine("Schedule,Account Name,Category,Gold Wt (g),Silver Wt (g),Amount (₹)");
            foreach (var sec in Statement.LiabilitiesSections)
            {
                sb.AppendLine($"[{sec.SectionTitle.ToUpperInvariant()}],,,,Total:,{sec.TotalAmount}");
                foreach (var item in sec.Items)
                {
                    sb.AppendLine($"\"{sec.SectionTitle}\",\"{item.AccountName.Replace("\"", "\"\"")}\",\"{item.HeadCategory}\",{item.GoldWeight},{item.SilverWeight},{item.Amount}");
                }
            }
            sb.AppendLine($",,,,TOTAL LIABILITIES:,{Statement.TotalLiabilities}");
            sb.AppendLine();

            sb.AppendLine("--- PROPERTY & ASSETS ---");
            sb.AppendLine("Schedule,Account Name,Category,Gold Wt (g),Silver Wt (g),Amount (₹)");
            foreach (var sec in Statement.AssetsSections)
            {
                sb.AppendLine($"[{sec.SectionTitle.ToUpperInvariant()}],,,,Total:,{sec.TotalAmount}");
                foreach (var item in sec.Items)
                {
                    sb.AppendLine($"\"{sec.SectionTitle}\",\"{item.AccountName.Replace("\"", "\"\"")}\",\"{item.HeadCategory}\",{item.GoldWeight},{item.SilverWeight},{item.Amount}");
                }
            }
            sb.AppendLine($",,,,TOTAL ASSETS:,{Statement.TotalAssets}");
            sb.AppendLine();

            sb.AppendLine("--- FINANCIAL SUMMARY ---");
            sb.AppendLine($"Net Sales Revenue,{Statement.ProfitLoss.SalesAmount}");
            sb.AppendLine($"Net Purchases,{Statement.ProfitLoss.PurchaseAmount}");
            sb.AppendLine($"Gross Profit,{Statement.ProfitLoss.GrossProfit}");
            sb.AppendLine($"Net Profit/(Loss),{Statement.ProfitLoss.NetProfit}");
            sb.AppendLine($"Working Capital,{Statement.WorkingCapital}");
            sb.AppendLine($"Equilibrium Difference,{Statement.Difference}");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            ShowToast("Balance Sheet exported to CSV successfully.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Export failed: {ex.Message}", ToastType.Error);
        }
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
