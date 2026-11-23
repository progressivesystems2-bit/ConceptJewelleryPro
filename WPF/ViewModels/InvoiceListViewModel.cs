using System.Collections.ObjectModel;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Accounts;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Services;
using ConceptJewelleryPro.WPF.Views;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class InvoiceListViewModel : ViewModelBase
{
    private readonly IBillRepository _billRepository;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IWhatsAppBillTrackerRepository? _trackerRepository;
    private readonly IWhatsAppService _whatsAppService;
    private readonly string _currentUser;

    private DateTime _fromDate;
    private DateTime _toDate;
    private string _selectedVouType = "ALL";
    private string _searchText = string.Empty;
    private InvoiceListItem? _selectedInvoice;
    private int _selectedRecordIndex = 1;

    private int _totalQty;
    private decimal _totalNetWeight;
    private decimal _totalGrossWeight;
    private decimal _totalTaxableAmount;
    private decimal _totalTaxAmount;
    private decimal _totalNetAmount;

    private string _selectedPrintOption = "DayWisePrint";

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;

    private readonly List<InvoiceListItem> _allInvoices = new();

    public event EventHandler<InvoiceListItem>? RequestOpenInvoice;
    public event EventHandler<bool?>? RequestClose;

    public InvoiceListViewModel(
        IBillRepository billRepository,
        string currentUser = "admin",
        IWhatsAppService? whatsAppService = null,
        ISettingsRepository? settingsRepository = null,
        IWhatsAppBillTrackerRepository? trackerRepository = null)
    {
        _billRepository = billRepository;
        _currentUser = currentUser;
        _settingsRepository = settingsRepository;
        _trackerRepository = trackerRepository;
        _whatsAppService = whatsAppService ?? new WhatsAppService(_settingsRepository, _trackerRepository);

        // Default to start of financial year (April 1st)
        var today = DateTime.Today;
        var startYear = today.Month >= 4 ? today.Year : today.Year - 1;
        _fromDate = new DateTime(startYear, 4, 1);
        _toDate = today;

        Invoices = new ObservableCollection<InvoiceListItem>();
        AvailableVouTypes = new ObservableCollection<string> { "ALL" };

        RefreshCommand = new AsyncRelayCommand(LoadInvoicesAsync);
        SyncWhatsAppRepliesCommand = new AsyncRelayCommand(SyncWhatsAppRepliesAsync);
        SelectInvoiceCommand = new RelayCommand(SelectInvoice);
        CloseCommand = new RelayCommand(Close);
        SalesBreakupCommand = new RelayCommand(SalesBreakup);
        SalesErrorCommand = new RelayCommand(SalesError);
        PrintReportCommand = new RelayCommand(PrintReport);
        ExportExcelCommand = new RelayCommand(ExportExcel);

        FirstRecordCommand = new RelayCommand(FirstRecord);
        PrevRecordCommand = new RelayCommand(PrevRecord);
        NextRecordCommand = new RelayCommand(NextRecord);
        LastRecordCommand = new RelayCommand(LastRecord);

        _ = InitializeAsync();
    }

    public ObservableCollection<InvoiceListItem> Invoices { get; }
    public ObservableCollection<string> AvailableVouTypes { get; }

    public DateTime FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                _ = LoadInvoicesAsync();
            }
        }
    }

    public DateTime ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                _ = LoadInvoicesAsync();
            }
        }
    }

    public string SelectedVouType
    {
        get => _selectedVouType;
        set
        {
            if (SetProperty(ref _selectedVouType, value))
            {
                _ = LoadInvoicesAsync();
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

    public InvoiceListItem? SelectedInvoice
    {
        get => _selectedInvoice;
        set
        {
            if (SetProperty(ref _selectedInvoice, value))
            {
                UpdateRecordIndex();
            }
        }
    }

    public int SelectedRecordIndex
    {
        get => _selectedRecordIndex;
        set
        {
            if (SetProperty(ref _selectedRecordIndex, value))
            {
                NavigateToIndex(value);
            }
        }
    }

    public int TotalQty
    {
        get => _totalQty;
        private set => SetProperty(ref _totalQty, value);
    }

    public decimal TotalNetWeight
    {
        get => _totalNetWeight;
        private set => SetProperty(ref _totalNetWeight, value);
    }

    public decimal TotalGrossWeight
    {
        get => _totalGrossWeight;
        private set => SetProperty(ref _totalGrossWeight, value);
    }

    public decimal TotalTaxableAmount
    {
        get => _totalTaxableAmount;
        private set => SetProperty(ref _totalTaxableAmount, value);
    }

    public decimal TotalTaxAmount
    {
        get => _totalTaxAmount;
        private set => SetProperty(ref _totalTaxAmount, value);
    }

    public decimal TotalNetAmount
    {
        get => _totalNetAmount;
        private set => SetProperty(ref _totalNetAmount, value);
    }

    public int TotalRecordsCount => Invoices.Count;

    public string SelectedPrintOption
    {
        get => _selectedPrintOption;
        set => SetProperty(ref _selectedPrintOption, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
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

    public ICommand RefreshCommand { get; }
    public ICommand SyncWhatsAppRepliesCommand { get; }
    public ICommand SelectInvoiceCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SalesBreakupCommand { get; }
    public ICommand SalesErrorCommand { get; }
    public ICommand PrintReportCommand { get; }
    public ICommand ExportExcelCommand { get; }

    public ICommand FirstRecordCommand { get; }
    public ICommand PrevRecordCommand { get; }
    public ICommand NextRecordCommand { get; }
    public ICommand LastRecordCommand { get; }

    public DateRangeCompanyInfo? CompanyDetails { get; private set; }
    public string CompanyName => CompanyDetails?.CompanyName ?? string.Empty;
    public string Address => CompanyDetails?.Address ?? string.Empty;
    public string Gstin => CompanyDetails?.Gstin ?? string.Empty;

    public async Task LoadCompanyDetailsAsync()
    {
        try
        {
            CompanyDetails = await _billRepository.GetCompanyDetailsAsync();
            OnPropertyChanged(nameof(CompanyDetails));
            OnPropertyChanged(nameof(CompanyName));
            OnPropertyChanged(nameof(Address));
            OnPropertyChanged(nameof(Gstin));
        }
        catch
        {
        }
    }

    private async Task InitializeAsync()
    {
        await LoadCompanyDetailsAsync();
        await LoadVoucherTypesAsync();
        await LoadInvoicesAsync();
    }

    private async Task LoadVoucherTypesAsync()
    {
        try
        {
            var appSettings = AppSettings.Load();
            var types = await _billRepository.GetSalesVouTypesAsync(appSettings.Counter, "Sales");

            AvailableVouTypes.Clear();
            AvailableVouTypes.Add("ALL");

            var distinctTypes = types.Select(t => t.VouType)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n);

            foreach (var t in distinctTypes)
            {
                AvailableVouTypes.Add(t);
            }

            if (AvailableVouTypes.Count > 0)
            {
                _selectedVouType = AvailableVouTypes[0];
                OnPropertyChanged(nameof(SelectedVouType));
            }
        }
        catch
        {
            // Fallback default list
            AvailableVouTypes.Clear();
            AvailableVouTypes.Add("ALL");
            AvailableVouTypes.Add("TAX INVOICE GOLD");
            AvailableVouTypes.Add("RETAIL INVOICE GOLD");
            AvailableVouTypes.Add("TAX INVOICE ARTICLES");
            AvailableVouTypes.Add("TAX INVOICE BULLION");
            _selectedVouType = "ALL";
            OnPropertyChanged(nameof(SelectedVouType));
        }
    }

    public async Task LoadInvoicesAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading invoices...";

            var list = await _billRepository.GetInvoiceListAsync(_fromDate, _toDate, _selectedVouType);

            if (_trackerRepository != null && list.Count > 0)
            {
                try
                {
                    var invoiceNos = list.Select(i => i.VouNo?.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct();
                    var trackers = await _trackerRepository.GetByInvoiceNosAsync(invoiceNos!);
                    foreach (var item in list)
                    {
                        var key = item.VouNo?.Trim();
                        if (!string.IsNullOrWhiteSpace(key) && trackers.TryGetValue(key, out var tracker))
                        {
                            item.WhatsAppStatus = !string.IsNullOrWhiteSpace(tracker.DeliveryStatus)
                                ? tracker.DeliveryStatus
                                : tracker.Rating;

                            item.WhatsAppReply = tracker.Reply;
                        }
                    }
                }
                catch
                {
                    // Non-fatal if tracker query encounters an issue
                }
            }

            _allInvoices.Clear();
            _allInvoices.AddRange(list);

            ApplyFilter();

            StatusMessage = $"Loaded {Invoices.Count} invoice(s).";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error loading invoice list: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var filter = _searchText?.Trim() ?? string.Empty;

        IEnumerable<InvoiceListItem> query = _allInvoices;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(i =>
                (!string.IsNullOrWhiteSpace(i.VouNo) && i.VouNo.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(i.CustomerName) && i.CustomerName.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(i.Phone) && i.Phone.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(i.VouType) && i.VouType.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(i.City) && i.City.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(i.WhatsAppStatus) && i.WhatsAppStatus.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(i.WhatsAppReply) && i.WhatsAppReply.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (i.Items != null && i.Items.Any(s =>
                    (!string.IsNullOrWhiteSpace(s.ItemName) && s.ItemName.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(s.Barcode) && s.Barcode.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(s.ENote) && s.ENote.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(s.Purity) && s.Purity.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(s.SalesMan) && s.SalesMan.Contains(filter, StringComparison.OrdinalIgnoreCase)))));
        }

        var filteredList = query.ToList();

        Invoices.Clear();
        foreach (var item in filteredList)
        {
            Invoices.Add(item);
        }

        RecalculateTotals();

        if (Invoices.Count > 0)
        {
            SelectedInvoice = Invoices[0];
            SelectedRecordIndex = 1;
        }
        else
        {
            SelectedInvoice = null;
            SelectedRecordIndex = 0;
        }

        OnPropertyChanged(nameof(TotalRecordsCount));
    }

    private void RecalculateTotals()
    {
        TotalQty = Invoices.Sum(i => i.Qty);
        TotalNetWeight = Invoices.Sum(i => i.NetWt);
        TotalGrossWeight = Invoices.Sum(i => i.GrWt);
        TotalTaxableAmount = Invoices.Sum(i => i.TaxableAmt);
        TotalTaxAmount = Invoices.Sum(i => i.TaxAmt);
        TotalNetAmount = Invoices.Sum(i => i.NetAmt);
    }

    private void UpdateRecordIndex()
    {
        if (SelectedInvoice != null)
        {
            var idx = Invoices.IndexOf(SelectedInvoice);
            if (idx >= 0)
            {
                _selectedRecordIndex = idx + 1;
                OnPropertyChanged(nameof(SelectedRecordIndex));
            }
        }
    }

    private void NavigateToIndex(int index)
    {
        if (index >= 1 && index <= Invoices.Count)
        {
            var target = Invoices[index - 1];
            if (!ReferenceEquals(SelectedInvoice, target))
            {
                _selectedInvoice = target;
                OnPropertyChanged(nameof(SelectedInvoice));
            }
        }
    }

    private void FirstRecord()
    {
        if (Invoices.Count > 0)
        {
            SelectedInvoice = Invoices[0];
        }
    }

    private void PrevRecord()
    {
        if (Invoices.Count == 0) return;
        var idx = SelectedInvoice != null ? Invoices.IndexOf(SelectedInvoice) : 0;
        if (idx > 0)
        {
            SelectedInvoice = Invoices[idx - 1];
        }
    }

    private void NextRecord()
    {
        if (Invoices.Count == 0) return;
        var idx = SelectedInvoice != null ? Invoices.IndexOf(SelectedInvoice) : -1;
        if (idx < Invoices.Count - 1)
        {
            SelectedInvoice = Invoices[idx + 1];
        }
    }

    private void LastRecord()
    {
        if (Invoices.Count > 0)
        {
            SelectedInvoice = Invoices[Invoices.Count - 1];
        }
    }

    public async Task EnsureSubformItemsLoadedAsync(InvoiceListItem item)
    {
        if (item.Items == null || item.Items.Count == 0)
        {
            try
            {
                var subItems = await _billRepository.GetSubformItemsAsync(item.VouNo);
                item.Items = subItems.ToList();
            }
            catch
            {
                // Non-blocking
            }
        }
    }

    private void SelectInvoice(object? parameter)
    {
        var target = parameter as InvoiceListItem ?? SelectedInvoice;
        if (target == null)
        {
            ErrorMessage = "Please select an invoice from the list.";
            return;
        }

        RequestOpenInvoice?.Invoke(this, target);
        RequestClose?.Invoke(this, true);
    }

    private void Close()
    {
        RequestClose?.Invoke(this, false);
    }

    private async void SalesBreakup()
    {
        try
        {
            var breakupVm = new SalesPaymentBreakupViewModel(
                _billRepository,
                _fromDate,
                _toDate,
                _selectedVouType,
                Invoices);

            await breakupVm.LoadAsync();

            var win = new SalesPaymentBreakupWindow(breakupVm);
            win.SetOwnerSafely();
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to open Sales Breakup: " + ex.Message;
            System.Windows.MessageBox.Show(
                "Error opening Sales Breakup: " + ex.Message,
                "Sales Breakup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void SalesError()
    {
        var errors = Invoices.Where(i => i.NetAmt <= 0 || i.Qty <= 0 || string.IsNullOrWhiteSpace(i.CustomerName)).ToList();
        if (errors.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "No sales errors detected in the current filtered invoice list.",
                "Sales Error Check",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        else
        {
            System.Windows.MessageBox.Show(
                $"Found {errors.Count} potential issue(s) in invoices (zero amount, zero qty, or missing customer).",
                "Sales Error Check",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private void PrintReport()
    {
        if (Invoices.Count == 0)
        {
            ErrorMessage = "No invoices available in the current filter to print.";
            System.Windows.MessageBox.Show(
                "No invoices found for the selected period and voucher type filter.",
                "Print Report",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            var optionName = SelectedPrintOption switch
            {
                "DayWisePrint" => "Day Wise Sales Register",
                "DayWiseSummaryPrint" => "Day Wise Sales Summary",
                "PurityWiseSales" => "Purity Wise Sales Report",
                "VoucherWisePrint" => "Voucher Wise Sales Register",
                "VoucherWiseSummary" => "Voucher Wise Sales Summary",
                "MonthwiseSummary" => "Month Wise Sales Summary",
                _ => "Sales Register Report"
            };

            if (CompanyDetails == null || string.IsNullOrWhiteSpace(CompanyDetails.CompanyName))
            {
                try { CompanyDetails = _billRepository.GetCompanyDetailsAsync().GetAwaiter().GetResult(); } catch { }
            }

            var doc = SalesRegisterFlowDocumentBuilder.CreateReportDocument(this);

            var preview = new InvoicePrintPreviewWindow(
                document: doc,
                windowTitle: $"{optionName} - Print Preview",
                headerTitle: $"{optionName} ({FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy})",
                docName: optionName);
            preview.SetOwnerSafely();

            preview.ShowDialog();
            StatusMessage = $"Report '{optionName}' generated successfully ({Invoices.Count} records).";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error generating print report: " + ex.Message;
        }
    }

    private void ExportExcel()
    {
        try
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"Invoice_Register_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Bill No,Bill Date,Invoice Type,Customer Name,Phone,Net Amt,Qty,Net Wt,Taxable Amt,Tax Amt,Status,WA Status,WA Reply");

                foreach (var i in Invoices)
                {
                    sb.AppendLine($"\"{i.VouNo}\",\"{i.BillDate:dd/MM/yyyy}\",\"{i.VouType}\",\"{i.CustomerName?.Replace("\"", "\"\"")}\",\"{i.Phone}\",{i.NetAmt},{i.Qty},{i.NetWt},{i.TaxableAmt},{i.TaxAmt},\"{i.Status}\",\"{i.WhatsAppStatus}\",\"{i.WhatsAppReply?.Replace("\"", "\"\"")}\"");
                }

                System.IO.File.WriteAllText(saveDialog.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                StatusMessage = $"Invoices exported to {System.IO.Path.GetFileName(saveDialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Export failed: " + ex.Message;
        }
    }

    public async Task SyncWhatsAppRepliesAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = "Syncing WhatsApp delivery statuses and customer replies...";

        try
        {
            var result = await _whatsAppService.SyncRepliesAndStatusesAsync();
            if (result.TotalCount > 0)
            {
                var parts = new List<string>();
                if (result.SyncedStatusesCount > 0) parts.Add($"{result.SyncedStatusesCount} status update(s)");
                if (result.SyncedRepliesCount > 0) parts.Add($"{result.SyncedRepliesCount} customer reply(s)");
                var summary = string.Join(" and ", parts);

                StatusMessage = $"Successfully synced {summary}.";
                System.Windows.MessageBox.Show($"Successfully synced {summary} from WhatsApp server.", "WhatsApp Sync", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                await LoadInvoicesAsync();
            }
            else
            {
                StatusMessage = "No new WhatsApp status updates or replies found on the server.";
                System.Windows.MessageBox.Show("No new WhatsApp delivery status updates or customer replies found on the server.", "WhatsApp Sync", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                await LoadInvoicesAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
            System.Windows.MessageBox.Show($"Failed to sync WhatsApp status & replies: {ex.Message}", "Sync Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
