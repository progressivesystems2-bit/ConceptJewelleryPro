using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.WPF.Services;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class VoucherAuditViewModel : ViewModelBase
{
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private readonly ITrialBalanceRepository _repository;

    private DateTime? _fromDate;
    private DateTime? _toDate;
    private bool _showOnlyImbalances = true;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private string? _statusMessage;

    private int _totalScanned;
    private int _balancedCount;
    private int _imbalancedCount;
    private decimal _totalImbalanceAmount;

    public VoucherAuditViewModel(ITrialBalanceRepository repository, DateTime? fromDate = null, DateTime? toDate = null)
    {
        _repository = repository;
        _fromDate = fromDate ?? new DateTime(2025, 4, 1);
        _toDate = toDate ?? DateTime.Today;

        RefreshCommand = new AsyncRelayCommand(async () => await RunAuditAsync());
        ExportCommand = new RelayCommand(ExportToCsv);

        _ = RunAuditAsync();
    }

    public string Title => "Voucher-wise Double Entry Audit";

    public ObservableCollection<VoucherAuditItem> AllItems { get; } = [];
    public ObservableCollection<VoucherAuditItem> DisplayItems { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ExportCommand { get; }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                _ = RunAuditAsync();
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
                _ = RunAuditAsync();
            }
        }
    }

    public bool ShowOnlyImbalances
    {
        get => _showOnlyImbalances;
        set
        {
            if (SetProperty(ref _showOnlyImbalances, value))
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

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int TotalScanned
    {
        get => _totalScanned;
        private set => SetProperty(ref _totalScanned, value);
    }

    public int BalancedCount
    {
        get => _balancedCount;
        private set => SetProperty(ref _balancedCount, value);
    }

    public int ImbalancedCount
    {
        get => _imbalancedCount;
        private set => SetProperty(ref _imbalancedCount, value);
    }

    public decimal TotalImbalanceAmount
    {
        get => _totalImbalanceAmount;
        private set => SetProperty(ref _totalImbalanceAmount, value);
    }

    public string FormattedTotalImbalance => TotalImbalanceAmount.ToString("N2", IndianCulture);

    public async Task RunAuditAsync()
    {
        IsBusy = true;
        StatusMessage = "Auditing vouchers from database...";

        try
        {
            var from = FromDate ?? new DateTime(2025, 4, 1);
            var to = ToDate ?? DateTime.Today;

            var list = await _repository.AuditVouchersAsync(from, to);

            AllItems.Clear();
            foreach (var item in list)
            {
                AllItems.Add(item);
            }

            TotalScanned = list.Count;
            BalancedCount = list.Count(x => x.IsBalanced);
            ImbalancedCount = list.Count(x => !x.IsBalanced);
            TotalImbalanceAmount = list.Where(x => !x.IsBalanced).Sum(x => Math.Abs(x.Difference));

            ApplyFilter();

            if (ImbalancedCount == 0)
            {
                StatusMessage = $"✓ Perfect Balance! All {TotalScanned} vouchers match Dr = Cr exactly.";
            }
            else
            {
                StatusMessage = $"Found {ImbalancedCount} imbalanced vouchers totaling ₹{FormattedTotalImbalance}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Audit error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        DisplayItems.Clear();
        var q = AllItems.AsEnumerable();

        if (ShowOnlyImbalances)
        {
            q = q.Where(x => !x.IsBalanced);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            q = q.Where(x =>
                x.VouNo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.VouType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.DebitAccounts.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.CreditAccounts.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.SourceTable.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Remarks.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in q)
        {
            DisplayItems.Add(item);
        }
    }

    private void ExportToCsv()
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"VoucherAudit_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("VOUCHER-WISE DOUBLE ENTRY AUDIT REPORT");
            sb.AppendLine($"Period: {FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}");
            sb.AppendLine($"Total Scanned: {TotalScanned}, Balanced: {BalancedCount}, Imbalanced: {ImbalancedCount}");
            sb.AppendLine();
            sb.AppendLine("VouNo,Date,VouType,SourceTable,DebitAccounts,CreditAccounts,TotalDebit,TotalCredit,Difference,Status,Remarks");

            foreach (var item in DisplayItems)
            {
                sb.AppendLine($"\"{EscapeCsv(item.VouNo)}\",\"{item.FormattedVouDate}\",\"{EscapeCsv(item.VouType)}\",\"{EscapeCsv(item.SourceTable)}\",\"{EscapeCsv(item.DebitAccounts)}\",\"{EscapeCsv(item.CreditAccounts)}\",{item.TotalDebit},{item.TotalCredit},{item.Difference},\"{EscapeCsv(item.StatusText)}\",\"{EscapeCsv(item.Remarks)}\"");
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            StatusMessage = "Audit report exported to CSV successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private static string EscapeCsv(string? value) => (value ?? string.Empty).Replace("\"", "\"\"");
}
