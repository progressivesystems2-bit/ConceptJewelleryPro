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

public sealed class TradingAccountViewModel : ViewModelBase
{
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private readonly ITradingAccountRepository _repository;
    private readonly string _currentUser;

    private bool _isInitializing;
    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    private DateTime? _fromDate = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
    private DateTime? _toDate = DateTime.Today;

    private decimal? _manualOpeningStock;
    private decimal? _manualClosingStock;
    private decimal? _manualOpeningGoldWeight;
    private decimal? _manualClosingGoldWeight;
    private decimal? _manualOpeningSilverWeight;
    private decimal? _manualClosingSilverWeight;

    private bool _hideZeroBalances = true;
    private TradingAccountStatement _statement = new();
    private CancellationTokenSource? _loadCts;

    public TradingAccountViewModel(ITradingAccountRepository repository, string currentUser = "admin")
    {
        _repository = repository;
        _currentUser = currentUser;

        RefreshCommand = new AsyncRelayCommand(async () => await LoadTradingAccountAsync(0));
        ExportToExcelCommand = new RelayCommand(ExportToCsv);
        PrintCommand = new RelayCommand(PrintReport);

        SetThisFYCommand = new RelayCommand(SetThisFY);
        SetLastFYCommand = new RelayCommand(SetLastFY);
        SetTodayCommand = new RelayCommand(SetToday);

        OpenAccountLedgerCommand = new RelayCommand(param =>
        {
            var accName = (param as TradingAccountItem)?.AccountName ?? (param as string);
            if (!string.IsNullOrWhiteSpace(accName) &&
                !accName.Contains("Profit", StringComparison.OrdinalIgnoreCase) &&
                !accName.Contains("Loss", StringComparison.OrdinalIgnoreCase) &&
                !accName.Contains("Stock", StringComparison.OrdinalIgnoreCase))
            {
                OpenAccountLedgerRequested?.Invoke(this, accName);
            }
        });

        _ = InitializeAsync();
    }

    public event EventHandler<string>? OpenAccountLedgerRequested;

    public string Title => "Trading Account";
    public string HeaderIcon => AppIcons.Reports;
    public string CurrentUsername => _currentUser;

    public ObservableCollection<TradingAccountItem> DebitItems { get; } = [];
    public ObservableCollection<TradingAccountItem> CreditItems { get; } = [];
    public ObservableCollection<TradingAccountItem> AllItems { get; } = [];

    public ICommand RefreshCommand { get; }
    public ICommand ExportToExcelCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand SetThisFYCommand { get; }
    public ICommand SetLastFYCommand { get; }
    public ICommand SetTodayCommand { get; }
    public ICommand OpenAccountLedgerCommand { get; }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value) && !_isInitializing)
            {
                _ = LoadTradingAccountAsync(100);
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
                _ = LoadTradingAccountAsync(100);
            }
        }
    }

    public decimal? ManualOpeningStock
    {
        get => _manualOpeningStock;
        set
        {
            if (SetProperty(ref _manualOpeningStock, value) && !_isInitializing)
            {
                _ = LoadTradingAccountAsync(300);
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
                _ = LoadTradingAccountAsync(300);
            }
        }
    }

    public decimal? ManualOpeningGoldWeight
    {
        get => _manualOpeningGoldWeight;
        set
        {
            if (SetProperty(ref _manualOpeningGoldWeight, value) && !_isInitializing)
            {
                _ = LoadTradingAccountAsync(300);
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
                _ = LoadTradingAccountAsync(300);
            }
        }
    }

    public decimal? ManualOpeningSilverWeight
    {
        get => _manualOpeningSilverWeight;
        set
        {
            if (SetProperty(ref _manualOpeningSilverWeight, value) && !_isInitializing)
            {
                _ = LoadTradingAccountAsync(300);
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
                _ = LoadTradingAccountAsync(300);
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
                _ = LoadTradingAccountAsync(50);
            }
        }
    }

    public TradingAccountStatement Statement
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
                var est = await _repository.GetEstimatedStockValuationAsync(ToDate ?? DateTime.Today);
                _manualClosingStock = est.Value > 0 ? est.Value : null;
                _manualClosingGoldWeight = est.GoldWeight > 0 ? est.GoldWeight : null;
                _manualClosingSilverWeight = est.SilverWeight > 0 ? est.SilverWeight : null;
                OnPropertyChanged(nameof(ManualClosingStock));
                OnPropertyChanged(nameof(ManualClosingGoldWeight));
                OnPropertyChanged(nameof(ManualClosingSilverWeight));
            }
            await LoadTradingAccountAsync(0);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    public async Task LoadTradingAccountAsync(int debounceMs = 0)
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
            var filter = new TradingAccountFilter
            {
                FromDate = FromDate,
                ToDate = ToDate,
                ManualOpeningStockAmount = ManualOpeningStock,
                ManualClosingStockAmount = ManualClosingStock,
                ManualOpeningGoldWeight = ManualOpeningGoldWeight,
                ManualClosingGoldWeight = ManualClosingGoldWeight,
                ManualOpeningSilverWeight = ManualOpeningSilverWeight,
                ManualClosingSilverWeight = ManualClosingSilverWeight,
                HideZeroBalances = HideZeroBalances
            };

            var statement = await _repository.GetTradingAccountAsync(filter, ct);
            if (ct.IsCancellationRequested) return;

            Statement = statement;

            DebitItems.Clear();
            foreach (var item in statement.DebitItems)
            {
                DebitItems.Add(item);
            }

            CreditItems.Clear();
            foreach (var item in statement.CreditItems)
            {
                CreditItems.Add(item);
            }

            AllItems.Clear();
            foreach (var item in statement.AllItems)
            {
                AllItems.Add(item);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ShowToast($"Failed to load Trading Account: {ex.Message}", ToastType.Error);
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
            var doc = TradingAccountFlowDocumentBuilder.CreateDocument(this);
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                doc.PageWidth = printDialog.PrintableAreaWidth;
                doc.PageHeight = printDialog.PrintableAreaHeight;
                doc.PagePadding = new System.Windows.Thickness(36);
                doc.ColumnGap = 0;
                doc.ColumnWidth = printDialog.PrintableAreaWidth;

                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, "Trading Account");
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
                FileName = $"TradingAccount_{ToDate:yyyyMMdd}.csv",
                DefaultExt = ".csv"
            };

            if (sfd.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine($"TRADING ACCOUNT STATEMENT ({FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy})");
            sb.AppendLine();

            sb.AppendLine("--- DEBIT SIDE (PURCHASES & DIRECT EXPENSES) ---");
            sb.AppendLine("Particulars,Account,Category,Gold Wt (g),Silver Wt (g),Amount (â‚¹)");
            foreach (var item in DebitItems)
            {
                sb.AppendLine($"\"{item.Particulars.Replace("\"", "\"\"")}\",\"{item.AccountName.Replace("\"", "\"\"")}\",\"{item.HeadCategory}\",{item.GrossWeight},{item.SilverWeight},{item.Amount}");
            }
            sb.AppendLine($",,,,TOTAL DEBITS:,{Statement.TotalDebitsTFormat}");
            sb.AppendLine();

            sb.AppendLine("--- CREDIT SIDE (SALES & CLOSING STOCK) ---");
            sb.AppendLine("Particulars,Account,Category,Gold Wt (g),Silver Wt (g),Amount (â‚¹)");
            foreach (var item in CreditItems)
            {
                sb.AppendLine($"\"{item.Particulars.Replace("\"", "\"\"")}\",\"{item.AccountName.Replace("\"", "\"\"")}\",\"{item.HeadCategory}\",{item.GrossWeight},{item.SilverWeight},{item.Amount}");
            }
            sb.AppendLine($",,,,TOTAL CREDITS:,{Statement.TotalCreditsTFormat}");
            sb.AppendLine();

            sb.AppendLine("--- SUMMARY METRICS ---");
            sb.AppendLine($"Gross Sales Revenue,{Statement.TotalSalesAmount}");
            sb.AppendLine($"Net Purchases,{Statement.TotalPurchasesAmount}");
            sb.AppendLine($"Direct Expenses,{Statement.TotalDirectExpensesAmount}");
            sb.AppendLine($"Opening Stock Value,{Statement.TotalOpeningStockAmount}");
            sb.AppendLine($"Closing Stock Value,{Statement.TotalClosingStockAmount}");
            sb.AppendLine($"Cost of Goods Sold (COGS),{Statement.CostOfGoodsSold}");
            sb.AppendLine($"Gross Profit,{Statement.GrossProfit}");
            sb.AppendLine($"Gross Margin %,{Statement.GrossProfitPercentage:F2}%");
            sb.AppendLine();

            sb.AppendLine("--- METAL RECONCILIATION ---");
            sb.AppendLine("Metal,Opening (g),Inward (g),Outward (g),Closing (g),Difference (g)");
            sb.AppendLine($"Gold,{Statement.GoldReconciliation.OpeningWeight},{Statement.GoldReconciliation.InwardWeight},{Statement.GoldReconciliation.OutwardWeight},{Statement.GoldReconciliation.ClosingWeight},{Statement.GoldReconciliation.Difference}");
            sb.AppendLine($"Silver,{Statement.SilverReconciliation.OpeningWeight},{Statement.SilverReconciliation.InwardWeight},{Statement.SilverReconciliation.OutwardWeight},{Statement.SilverReconciliation.ClosingWeight},{Statement.SilverReconciliation.Difference}");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            ShowToast("Trading Account exported to CSV successfully.", ToastType.Success);
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