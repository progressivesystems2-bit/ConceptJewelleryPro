using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Controls;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public enum NonBarcodeReportTab
{
    ItemTypeWiseStock,
    TransactionWise
}

public sealed class NonBarcodeStockReportViewModel : ViewModelBase
{
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private readonly INonBarcodeReportRepository _repository;
    private bool _isInitializing;

    private NonBarcodeReportTab _selectedTab = NonBarcodeReportTab.ItemTypeWiseStock;
    private string _selectedItemHead = "*";
    private string _selectedSubGroup = "*";
    private string _selectedItemName = "*";
    private DateTime? _fromDate = new DateTime(DateTime.Today.Month < 4 ? DateTime.Today.Year - 1 : DateTime.Today.Year, 4, 1);
    private DateTime? _toDate = DateTime.Today;
    private string _searchText = string.Empty;
    private NonBarcodeGroupSummary? _selectedGroupSummary;
    private NonBarcodeTransactionDetail? _selectedTransaction;

    private bool _isBusy;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    // Totals
    private int _totalItemsCount;
    private decimal _totalOpeningQty;
    private decimal _totalOpeningWt;
    private decimal _totalInwardQty;
    private decimal _totalInwardWt;
    private decimal _totalOutwardQty;
    private decimal _totalOutwardWt;
    private decimal _totalClosingQty;
    private decimal _totalClosingWt;
    private decimal _totalAmount;

    public NonBarcodeStockReportViewModel(INonBarcodeReportRepository repository)
    {
        _repository = repository;

        RefreshCommand = new AsyncRelayCommand(async () => await LoadReportAsync());
        ExportToExcelCommand = new RelayCommand(ExportToCsv);
        PrintCommand = new RelayCommand(PrintReport);
        SelectTabCommand = new RelayCommand(param =>
        {
            if (param is NonBarcodeReportTab tab)
            {
                SelectedTab = tab;
            }
            else if (param is string s && Enum.TryParse<NonBarcodeReportTab>(s, true, out var parsedTab))
            {
                SelectedTab = parsedTab;
            }
        });

        _ = InitializeAsync();
    }

    public string Title => "ItemType Stock Report";

    public ObservableRangeCollection<string> ItemHeads { get; } = ["*"];
    public ObservableRangeCollection<string> SubGroups { get; } = ["*"];
    public ObservableRangeCollection<string> ItemNames { get; } = ["*"];

    public ObservableRangeCollection<NonBarcodeGroupSummary> GroupSummaries { get; } = [];
    public ObservableRangeCollection<NonBarcodeTransactionDetail> Transactions { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ExportToExcelCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand SelectTabCommand { get; }

    public NonBarcodeReportTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsItemTypeWiseActive));
                OnPropertyChanged(nameof(IsTransactionWiseActive));
                OnPropertyChanged(nameof(IsGroupGridVisible));
                OnPropertyChanged(nameof(IsTransactionGridVisible));
                OnPropertyChanged(nameof(IsStockSummaryFooterVisible));
                OnPropertyChanged(nameof(IsTransactionFooterVisible));

                if (!_isInitializing)
                {
                    _ = LoadReportAsync();
                }
            }
        }
    }

    public bool IsItemTypeWiseActive => SelectedTab == NonBarcodeReportTab.ItemTypeWiseStock;
    public bool IsTransactionWiseActive => SelectedTab == NonBarcodeReportTab.TransactionWise;

    public bool IsGroupGridVisible => SelectedTab == NonBarcodeReportTab.ItemTypeWiseStock;
    public bool IsTransactionGridVisible => SelectedTab == NonBarcodeReportTab.TransactionWise;

    public bool IsStockSummaryFooterVisible => SelectedTab == NonBarcodeReportTab.ItemTypeWiseStock;
    public bool IsTransactionFooterVisible => SelectedTab == NonBarcodeReportTab.TransactionWise;

    public string SelectedItemHead
    {
        get => _selectedItemHead;
        set
        {
            if (SetProperty(ref _selectedItemHead, value))
            {
                if (!_isInitializing) _ = LoadReportAsync();
            }
        }
    }

    public string SelectedSubGroup
    {
        get => _selectedSubGroup;
        set
        {
            if (SetProperty(ref _selectedSubGroup, value))
            {
                if (!_isInitializing) _ = LoadReportAsync();
            }
        }
    }

    public string SelectedItemName
    {
        get => _selectedItemName;
        set
        {
            if (SetProperty(ref _selectedItemName, value))
            {
                if (!_isInitializing) _ = LoadReportAsync();
            }
        }
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                if (!_isInitializing) _ = LoadReportAsync();
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
                if (!_isInitializing) _ = LoadReportAsync();
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
                if (!_isInitializing) _ = LoadReportAsync();
            }
        }
    }

    public NonBarcodeGroupSummary? SelectedGroupSummary
    {
        get => _selectedGroupSummary;
        set => SetProperty(ref _selectedGroupSummary, value);
    }

    public NonBarcodeTransactionDetail? SelectedTransaction
    {
        get => _selectedTransaction;
        set => SetProperty(ref _selectedTransaction, value);
    }

    public int TotalItemsCount
    {
        get => _totalItemsCount;
        private set
        {
            if (SetProperty(ref _totalItemsCount, value))
            {
                OnPropertyChanged(nameof(RecordCountDisplay));
            }
        }
    }

    public string RecordCountDisplay => $"Total Records: {TotalItemsCount}";

    public decimal TotalOpeningQty
    {
        get => _totalOpeningQty;
        private set => SetProperty(ref _totalOpeningQty, value);
    }

    public decimal TotalOpeningWt
    {
        get => _totalOpeningWt;
        private set => SetProperty(ref _totalOpeningWt, value);
    }

    public decimal TotalInwardQty
    {
        get => _totalInwardQty;
        private set => SetProperty(ref _totalInwardQty, value);
    }

    public decimal TotalInwardWt
    {
        get => _totalInwardWt;
        private set => SetProperty(ref _totalInwardWt, value);
    }

    public decimal TotalOutwardQty
    {
        get => _totalOutwardQty;
        private set => SetProperty(ref _totalOutwardQty, value);
    }

    public decimal TotalOutwardWt
    {
        get => _totalOutwardWt;
        private set => SetProperty(ref _totalOutwardWt, value);
    }

    public decimal TotalClosingQty
    {
        get => _totalClosingQty;
        private set => SetProperty(ref _totalClosingQty, value);
    }

    public decimal TotalClosingWt
    {
        get => _totalClosingWt;
        private set => SetProperty(ref _totalClosingWt, value);
    }

    public decimal TotalAmount
    {
        get => _totalAmount;
        private set => SetProperty(ref _totalAmount, value);
    }

    public decimal TotalNetQty => TotalInwardQty - TotalOutwardQty;
    public decimal TotalNetWt => TotalInwardWt - TotalOutwardWt;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
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

    public async Task InitializeAsync()
    {
        IsBusy = true;
        _isInitializing = true;
        try
        {
            var (heads, subs, items) = await Task.Run(() => _repository.GetFilterMastersAsync());
            ItemHeads.ReplaceRange(heads);
            SubGroups.ReplaceRange(subs);
            ItemNames.ReplaceRange(items);
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load filters: {ex.Message}", ToastType.Error);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
        }

        await LoadReportAsync();
    }

    public async Task LoadReportAsync()
    {
        IsBusy = true;
        try
        {
            var filter = new NonBarcodeStockReportFilter
            {
                ItemHead = SelectedItemHead,
                SubGroup = SelectedSubGroup,
                ItemName = SelectedItemName,
                FromDate = FromDate,
                ToDate = ToDate,
                SearchText = SearchText
            };

            if (SelectedTab == NonBarcodeReportTab.ItemTypeWiseStock)
            {
                var list = await Task.Run(() => _repository.GetItemTypeWiseStockAsync(filter));
                GroupSummaries.ReplaceRange(list);
                RecalculateGroupTotals(list);

                if (GroupSummaries.Count > 0 && SelectedGroupSummary is null)
                {
                    SelectedGroupSummary = GroupSummaries[0];
                }
            }
            else
            {
                var list = await Task.Run(() => _repository.GetTransactionDetailsAsync(filter));
                Transactions.ReplaceRange(list);
                RecalculateTransactionTotals(list);

                if (Transactions.Count > 0 && SelectedTransaction is null)
                {
                    SelectedTransaction = Transactions[0];
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load ItemType stock report: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RecalculateGroupTotals(IReadOnlyList<NonBarcodeGroupSummary> groups)
    {
        TotalItemsCount = groups.Count;
        TotalOpeningQty = groups.Sum(x => x.OpeningQty);
        TotalOpeningWt = groups.Sum(x => x.OpeningWt);
        TotalInwardQty = groups.Sum(x => x.InwardQty);
        TotalInwardWt = groups.Sum(x => x.InwardWt);
        TotalOutwardQty = groups.Sum(x => x.OutwardQty);
        TotalOutwardWt = groups.Sum(x => x.OutwardWt);
        TotalClosingQty = groups.Sum(x => x.ClosingQty);
        TotalClosingWt = groups.Sum(x => x.ClosingWt);
        TotalAmount = 0m;
    }

    private void RecalculateTransactionTotals(IReadOnlyList<NonBarcodeTransactionDetail> txs)
    {
        TotalItemsCount = txs.Count;
        TotalOpeningQty = 0;
        TotalOpeningWt = 0;
        TotalInwardQty = txs.Sum(x => x.InQty);
        TotalInwardWt = txs.Sum(x => x.InWt);
        TotalOutwardQty = txs.Sum(x => x.OutQty);
        TotalOutwardWt = txs.Sum(x => x.OutWt);
        TotalClosingQty = TotalNetQty;
        TotalClosingWt = TotalNetWt;
        TotalAmount = txs.Sum(x => x.Amount);

        OnPropertyChanged(nameof(TotalNetQty));
        OnPropertyChanged(nameof(TotalNetWt));
    }

    private void ExportToCsv()
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"ItemType_StockReport_{SelectedTab}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (saveDialog.ShowDialog() != true) return;

            var sb = new StringBuilder();

            if (SelectedTab == NonBarcodeReportTab.ItemTypeWiseStock)
            {
                sb.AppendLine("Item Type,Items Count,Opening Qty,Opening Wt,Inward Qty,Inward Wt,Outward Qty,Outward Wt,Closing Qty,Closing Wt");
                foreach (var g in GroupSummaries)
                {
                    sb.AppendLine($"\"{EscapeCsv(g.ItemType)}\",{g.ItemCount},{g.OpeningQty},{g.OpeningWt:F3},{g.InwardQty},{g.InwardWt:F3},{g.OutwardQty},{g.OutwardWt:F3},{g.ClosingQty},{g.ClosingWt:F3}");
                }
                sb.AppendLine($"\"TOTALS\",{TotalItemsCount},{TotalOpeningQty},{TotalOpeningWt:F3},{TotalInwardQty},{TotalInwardWt:F3},{TotalOutwardQty},{TotalOutwardWt:F3},{TotalClosingQty},{TotalClosingWt:F3}");
            }
            else
            {
                sb.AppendLine("Date,Voucher No,Type,Party Name,Item Name,Head Group,Sub Group,In Qty,In Wt,Out Qty,Out Wt,Rate,Amount,Narration,User");
                foreach (var t in Transactions)
                {
                    sb.AppendLine($"{t.Date:yyyy-MM-dd},\"{EscapeCsv(t.VouNo)}\",\"{EscapeCsv(t.TransactionType)}\",\"{EscapeCsv(t.PartyName)}\",\"{EscapeCsv(t.ItemName)}\",\"{EscapeCsv(t.HeadGroup)}\",\"{EscapeCsv(t.SubGroup)}\",{t.InQty},{t.InWt:F3},{t.OutQty},{t.OutWt:F3},{t.Rate:F2},{t.Amount:F2},\"{EscapeCsv(t.Narration)}\",\"{EscapeCsv(t.EntryUser)}\"");
                }
                sb.AppendLine($"\"TOTALS\",,,,,,,\"{TotalInwardQty}\",{TotalInwardWt:F3},\"{TotalOutwardQty}\",{TotalOutwardWt:F3},,{TotalAmount:F2},,");
            }

            File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
            ShowToast("Exported to CSV successfully!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Export failed: {ex.Message}", ToastType.Error);
        }
    }

    private static string EscapeCsv(string val) => (val ?? string.Empty).Replace("\"", "\"\"");

    private void PrintReport()
    {
        ShowToast($"Printing ItemType Stock Report ({SelectedTab})...", ToastType.Info);
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
