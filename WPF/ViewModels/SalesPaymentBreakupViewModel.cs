using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class SalesPaymentBreakupViewModel : ViewModelBase
{
    private readonly IBillRepository _billRepository;
    private readonly IEnumerable<InvoiceListItem>? _existingInvoices;

    private DateTime _fromDate;
    private DateTime _toDate;
    private string _vouType;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedCategoryFilter = "ALL";

    private decimal _totalSalesAmount;
    private decimal _totalCashAmount;
    private decimal _totalPaytmAmount;
    private decimal _totalOldSilverAmount;
    private decimal _totalOldSilverWeight;
    private decimal _totalCreditAmount;
    private decimal _totalOtherAmount;
    private int _totalInvoicesCount;
    private int _totalTransactionsCount;

    public SalesPaymentBreakupViewModel(
        IBillRepository billRepository,
        DateTime fromDate,
        DateTime toDate,
        string vouType = "ALL",
        IEnumerable<InvoiceListItem>? existingInvoices = null)
    {
        _billRepository = billRepository;
        _fromDate = fromDate;
        _toDate = toDate;
        _vouType = string.IsNullOrWhiteSpace(vouType) ? "ALL" : vouType;
        _existingInvoices = existingInvoices;

        SummaryItems = new ObservableCollection<PaymentBreakupSummaryItem>();
        InvoiceItems = new List<InvoicePaymentBreakupItem>();
        FilteredInvoiceItems = new ObservableCollection<InvoicePaymentBreakupItem>();

        CategoryFilterOptions = new ObservableCollection<string>
        {
            "ALL",
            "Cash",
            "Paytm / Digital",
            "Old Silver",
            "Credits / Debtors",
            "Old Gold",
            "Other / Advance"
        };

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        ExportCsvCommand = new RelayCommand(ExportCsv);
        CloseCommand = new RelayCommand(Close);
    }

    public DateTime FromDate => _fromDate;
    public DateTime ToDate => _toDate;
    public string VouType => _vouType;

    public string FormattedPeriod =>
        $"Period: {_fromDate:dd/MM/yyyy} to {_toDate:dd/MM/yyyy} | Voucher Type: {_vouType}";

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

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
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

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public ObservableCollection<string> CategoryFilterOptions { get; }

    public decimal TotalSalesAmount
    {
        get => _totalSalesAmount;
        private set => SetProperty(ref _totalSalesAmount, value);
    }

    public decimal TotalCashAmount
    {
        get => _totalCashAmount;
        private set => SetProperty(ref _totalCashAmount, value);
    }

    public decimal TotalPaytmAmount
    {
        get => _totalPaytmAmount;
        private set => SetProperty(ref _totalPaytmAmount, value);
    }

    public decimal TotalOldSilverAmount
    {
        get => _totalOldSilverAmount;
        private set => SetProperty(ref _totalOldSilverAmount, value);
    }

    public decimal TotalOldSilverWeight
    {
        get => _totalOldSilverWeight;
        private set => SetProperty(ref _totalOldSilverWeight, value);
    }

    public decimal TotalCreditAmount
    {
        get => _totalCreditAmount;
        private set => SetProperty(ref _totalCreditAmount, value);
    }

    public decimal TotalOtherAmount
    {
        get => _totalOtherAmount;
        private set => SetProperty(ref _totalOtherAmount, value);
    }

    public int TotalInvoicesCount
    {
        get => _totalInvoicesCount;
        private set => SetProperty(ref _totalInvoicesCount, value);
    }

    public int TotalTransactionsCount
    {
        get => _totalTransactionsCount;
        private set => SetProperty(ref _totalTransactionsCount, value);
    }

    public ObservableCollection<PaymentBreakupSummaryItem> SummaryItems { get; }

    public List<InvoicePaymentBreakupItem> InvoiceItems { get; }

    public ObservableCollection<InvoicePaymentBreakupItem> FilteredInvoiceItems { get; }

    public ICommand RefreshCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand CloseCommand { get; }

    public event EventHandler? RequestClose;

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading sales payment breakup...";

            IReadOnlyList<InvoiceListItem> invoices;
            if (_existingInvoices != null && _existingInvoices.Any())
            {
                invoices = _existingInvoices.ToList();
            }
            else
            {
                invoices = await _billRepository.GetInvoiceListAsync(_fromDate, _toDate, _vouType);
            }

            var payments = await _billRepository.GetSalesPaymentsAsync(_fromDate, _toDate, _vouType);

            // Group payments by VouNo
            var paymentsByVouNo = payments
                .Where(p => !string.IsNullOrWhiteSpace(p.VouNo))
                .GroupBy(p => p.VouNo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            InvoiceItems.Clear();

            decimal sumCash = 0;
            decimal sumPaytm = 0;
            decimal sumOldSilver = 0;
            decimal sumOldSilverWt = 0;
            decimal sumCredit = 0;
            decimal sumOther = 0;
            decimal sumSales = 0;

            // Tracking for summary grid: Key = (Category, AccountName)
            var summaryMap = new Dictionary<(string Category, string AccountName), (int Count, decimal Amount, decimal Weight, string Color)>();

            foreach (var inv in invoices)
            {
                sumSales += inv.NetAmt;
                var invVouNo = inv.VouNo?.Trim() ?? string.Empty;

                decimal invCash = 0;
                decimal invPaytm = 0;
                decimal invOldSilver = 0;
                decimal invOldSilverWt = 0;
                decimal invCredit = 0;
                decimal invOther = 0;

                var summaryParts = new List<string>();

                if (paymentsByVouNo.TryGetValue(invVouNo, out var invPayments))
                {
                    foreach (var pay in invPayments)
                    {
                        var (category, color) = ClassifyPayment(pay);
                        var accName = !string.IsNullOrWhiteSpace(pay.AccountName) ? pay.AccountName.Trim() : "UNNAMED";
                        var payAmt = pay.Amount;
                        var payWt = pay.NtWt > 0 ? pay.NtWt : pay.GrWt;

                        switch (category)
                        {
                            case "Cash":
                                invCash += payAmt;
                                break;
                            case "Paytm / Digital":
                                invPaytm += payAmt;
                                break;
                            case "Old Silver":
                                invOldSilver += payAmt;
                                invOldSilverWt += payWt;
                                break;
                            case "Credits / Debtors":
                                invCredit += payAmt;
                                break;
                            default:
                                invOther += payAmt;
                                break;
                        }

                        if (payAmt != 0 || payWt != 0)
                        {
                            var key = (category, accName);
                            if (!summaryMap.ContainsKey(key))
                            {
                                summaryMap[key] = (1, payAmt, payWt, color);
                            }
                            else
                            {
                                var existing = summaryMap[key];
                                summaryMap[key] = (existing.Count + 1, existing.Amount + payAmt, existing.Weight + payWt, color);
                            }

                            summaryParts.Add($"{accName}: ₹{payAmt:N0}");
                        }
                    }
                }

                // If invoice amount exceeds recorded payments, remaining balance is outstanding credit/due
                var recordedTotal = invCash + invPaytm + invOldSilver + invCredit + invOther;
                var unallocatedBalance = inv.NetAmt - recordedTotal;
                if (unallocatedBalance > 0.01m)
                {
                    invCredit += unallocatedBalance;
                    var key = ("Credits / Debtors", "Customer Credit / Outstanding Due");
                    var color = "#F59E0B";
                    if (!summaryMap.ContainsKey(key))
                    {
                        summaryMap[key] = (1, unallocatedBalance, 0m, color);
                    }
                    else
                    {
                        var existing = summaryMap[key];
                        summaryMap[key] = (existing.Count + 1, existing.Amount + unallocatedBalance, existing.Weight, color);
                    }
                    summaryParts.Add($"Credit Due: ₹{unallocatedBalance:N0}");
                }

                sumCash += invCash;
                sumPaytm += invPaytm;
                sumOldSilver += invOldSilver;
                sumOldSilverWt += invOldSilverWt;
                sumCredit += invCredit;
                sumOther += invOther;

                InvoiceItems.Add(new InvoicePaymentBreakupItem
                {
                    VouNo = invVouNo,
                    BillDate = inv.BillDate,
                    VouType = inv.VouType,
                    CustomerName = inv.CustomerName,
                    Phone = inv.Phone,
                    NetAmt = inv.NetAmt,
                    CashAmt = invCash,
                    PaytmAmt = invPaytm,
                    OldSilverAmt = invOldSilver,
                    OldSilverWt = invOldSilverWt,
                    CreditAmt = invCredit,
                    OtherAmt = invOther,
                    TotalSettled = invCash + invPaytm + invOldSilver + invCredit + invOther,
                    PaymentSummary = string.Join(" | ", summaryParts)
                });
            }

            // Populate SummaryItems
            SummaryItems.Clear();
            var totalAllPayments = sumSales > 0 ? sumSales : (sumCash + sumPaytm + sumOldSilver + sumCredit + sumOther);
            int totalTxCount = 0;

            // Order categories logically: Cash, Paytm / Digital, Old Silver, Credits / Debtors, Old Gold, Other
            var categoryOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Cash", 1 },
                { "Paytm / Digital", 2 },
                { "Old Silver", 3 },
                { "Old Gold", 4 },
                { "Credits / Debtors", 5 },
                { "Other / Advance", 6 }
            };

            var sortedSummary = summaryMap
                .Select(kvp => new PaymentBreakupSummaryItem
                {
                    PaymentCategory = kvp.Key.Category,
                    AccountName = kvp.Key.AccountName,
                    TransactionCount = kvp.Value.Count,
                    Amount = kvp.Value.Amount,
                    Weight = kvp.Value.Weight,
                    CategoryColor = kvp.Value.Color,
                    Percentage = totalAllPayments > 0 ? Math.Round((kvp.Value.Amount / totalAllPayments) * 100m, 2) : 0m
                })
                .OrderBy(s => categoryOrder.TryGetValue(s.PaymentCategory, out var o) ? o : 99)
                .ThenByDescending(s => s.Amount)
                .ToList();

            foreach (var item in sortedSummary)
            {
                SummaryItems.Add(item);
                totalTxCount += item.TransactionCount;
            }

            // Update KPI metrics
            TotalSalesAmount = sumSales;
            TotalCashAmount = sumCash;
            TotalPaytmAmount = sumPaytm;
            TotalOldSilverAmount = sumOldSilver;
            TotalOldSilverWeight = sumOldSilverWt;
            TotalCreditAmount = sumCredit;
            TotalOtherAmount = sumOther;
            TotalInvoicesCount = invoices.Count;
            TotalTransactionsCount = totalTxCount;

            ApplyFilter();

            StatusMessage = $"Loaded {invoices.Count} invoices with {sortedSummary.Count} payment heads.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load payment breakup: " + ex.Message;
            StatusMessage = "Error occurred.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static (string Category, string Color) ClassifyPayment(PaymentSubform pay)
    {
        var acc = pay.AccountName?.Trim() ?? string.Empty;
        var head = pay.HeadCategory?.Trim() ?? string.Empty;
        var item = pay.ItemName?.Trim() ?? string.Empty;
        var itemType = pay.ItemType?.Trim() ?? string.Empty;

        // 1. CASH
        if (acc.Contains("cash", StringComparison.OrdinalIgnoreCase) ||
            head.Contains("cash", StringComparison.OrdinalIgnoreCase))
        {
            return ("Cash", "#10B981");
        }

        // 2. OLD SILVER
        if (acc.Contains("silver", StringComparison.OrdinalIgnoreCase) ||
            item.Contains("silver", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("silver", StringComparison.OrdinalIgnoreCase))
        {
            return ("Old Silver", "#8B5CF6");
        }

        // 3. OLD GOLD
        if (acc.Contains("gold", StringComparison.OrdinalIgnoreCase) ||
            item.Contains("gold", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("gold", StringComparison.OrdinalIgnoreCase))
        {
            return ("Old Gold", "#EAB308");
        }

        // 4. PAYTM / DIGITAL / BANK / CARD
        if (acc.Contains("paytm", StringComparison.OrdinalIgnoreCase) ||
            acc.Contains("upi", StringComparison.OrdinalIgnoreCase) ||
            acc.Contains("gpay", StringComparison.OrdinalIgnoreCase) ||
            acc.Contains("phonepe", StringComparison.OrdinalIgnoreCase) ||
            acc.Contains("bank", StringComparison.OrdinalIgnoreCase) ||
            acc.Contains("card", StringComparison.OrdinalIgnoreCase) ||
            acc.Contains("pos", StringComparison.OrdinalIgnoreCase) ||
            acc.Contains("hdfc", StringComparison.OrdinalIgnoreCase) ||
            acc.Contains("sbi", StringComparison.OrdinalIgnoreCase) ||
            acc.Contains("icici", StringComparison.OrdinalIgnoreCase) ||
            head.Contains("bank", StringComparison.OrdinalIgnoreCase) ||
            head.Contains("card", StringComparison.OrdinalIgnoreCase))
        {
            return ("Paytm / Digital", "#3B82F6");
        }

        // 5. CREDITS / DEBTORS / DUE
        if (acc.Equals("due", StringComparison.OrdinalIgnoreCase) ||
            head.Contains("debtor", StringComparison.OrdinalIgnoreCase) ||
            head.Contains("customer", StringComparison.OrdinalIgnoreCase) ||
            head.Contains("creditor", StringComparison.OrdinalIgnoreCase))
        {
            return ("Credits / Debtors", "#F59E0B");
        }

        // 6. OTHER
        return ("Other / Advance", "#6B7280");
    }

    private void ApplyFilter()
    {
        var search = _searchText?.Trim().ToLowerInvariant() ?? string.Empty;
        var catFilter = _selectedCategoryFilter ?? "ALL";

        var filtered = InvoiceItems.Where(item =>
        {
            // Category filter
            if (!catFilter.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                bool matchesCat = catFilter switch
                {
                    "Cash" => item.CashAmt > 0,
                    "Paytm / Digital" => item.PaytmAmt > 0,
                    "Old Silver" => item.OldSilverAmt > 0,
                    "Credits / Debtors" => item.CreditAmt > 0,
                    "Old Gold" => item.OtherAmt > 0,
                    "Other / Advance" => item.OtherAmt > 0,
                    _ => true
                };
                if (!matchesCat) return false;
            }

            // Search text filter
            if (!string.IsNullOrEmpty(search))
            {
                return item.VouNo.ToLowerInvariant().Contains(search) ||
                       item.CustomerName.ToLowerInvariant().Contains(search) ||
                       item.Phone.ToLowerInvariant().Contains(search) ||
                       item.VouType.ToLowerInvariant().Contains(search) ||
                       item.PaymentSummary.ToLowerInvariant().Contains(search);
            }

            return true;
        }).ToList();

        FilteredInvoiceItems.Clear();
        foreach (var f in filtered)
        {
            FilteredInvoiceItems.Add(f);
        }
    }

    private void ExportCsv()
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"Sales_Payment_Breakup_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();

                // 1. Report Header
                sb.AppendLine("=== SALES PAYMENT BREAKUP REPORT ===");
                sb.AppendLine($"From Date: {_fromDate:dd/MM/yyyy},To Date: {_toDate:dd/MM/yyyy},Voucher Type: {_vouType}");
                sb.AppendLine($"Generated On: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine();

                // 2. Summary KPI
                sb.AppendLine("=== PAYMENT MODE SUMMARY ===");
                sb.AppendLine("Category,Account Name,Transaction Count,Weight (g),Amount (Rs),Share (%)");

                foreach (var s in SummaryItems)
                {
                    sb.AppendLine($"\"{s.PaymentCategory}\",\"{s.AccountName}\",{s.TransactionCount},{s.Weight:F3},{s.Amount:F2},{s.Percentage:F2}%");
                }

                sb.AppendLine($"\"TOTAL SALES\",\"ALL MODES\",{TotalTransactionsCount},{TotalOldSilverWeight:F3},{TotalSalesAmount:F2},100.00%");
                sb.AppendLine();

                // 3. Invoice-wise Details
                sb.AppendLine("=== INVOICE-WISE PAYMENT BREAKDOWN ===");
                sb.AppendLine("Bill No,Date,Voucher Type,Customer Name,Phone,Net Amount,Cash,Paytm/Digital,Old Silver,Old Silver Wt (g),Credits/Due,Other,Details");

                foreach (var inv in FilteredInvoiceItems)
                {
                    var cleanCust = inv.CustomerName?.Replace("\"", "\"\"") ?? string.Empty;
                    var cleanDetails = inv.PaymentSummary?.Replace("\"", "\"\"") ?? string.Empty;
                    sb.AppendLine($"\"{inv.VouNo}\",\"{inv.BillDate:dd/MM/yyyy}\",\"{inv.VouType}\",\"{cleanCust}\",\"{inv.Phone}\",{inv.NetAmt:F2},{inv.CashAmt:F2},{inv.PaytmAmt:F2},{inv.OldSilverAmt:F2},{inv.OldSilverWt:F3},{inv.CreditAmt:F2},{inv.OtherAmt:F2},\"{cleanDetails}\"");
                }

                File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(
                    $"Payment breakup successfully exported to:\n{saveDialog.FileName}",
                    "Export CSV",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Export failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
