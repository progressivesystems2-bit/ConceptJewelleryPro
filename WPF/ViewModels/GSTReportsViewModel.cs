using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities.GST;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Views;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class GSTReportsViewModel : ViewModelBase
{
    private readonly IGstReportRepository _gstReportRepository;
    private readonly string _currentUser;
    private readonly ISettingsRepository? _settingsRepository;

    private int _selectedTabIndex;
    private DateTime _fromDate;
    private DateTime _toDate;
    private string _selectedVouType = "ALL";

    private bool _isBusy;
    private string? _statusMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    // GSTR-1
    private Gstr1Report _gstr1Report = new();
    private ObservableCollection<Gstr1B2BInvoice> _b2bInvoices = new();
    private ObservableCollection<Gstr1B2CLargeInvoice> _b2cLargeInvoices = new();
    private ObservableCollection<Gstr1B2CSmallSummary> _b2cSmallSummaries = new();
    private ObservableCollection<Gstr1CreditDebitNote> _creditDebitNotes = new();
    private ObservableCollection<Gstr1HsnSummary> _hsnSummaries = new();
    private ObservableCollection<Gstr1HsnSummary> _hsnB2BSummaries = new();
    private ObservableCollection<Gstr1HsnSummary> _hsnB2CSummaries = new();
    private ObservableCollection<Gstr1DocumentSummary> _documentSummaries = new();

    // GSTR-3B
    private Gstr3BReport _gstr3BReport = new();

    // GSTR-2 Purchase Register
    private ObservableCollection<GstPurchaseRegisterItem> _purchaseRegisterItems = new();

    // GST Reconciliation
    private List<GstPortalInvoice> _portalInvoices = new();
    private List<GstReconciliationItem> _allReconciliationItems = new();
    private ObservableCollection<GstReconciliationItem> _filteredReconciliationItems = new();
    private GstReconciliationSummary _reconciliationSummary = new();
    private string _selectedMatchFilter = "ALL";
    private string _uploadedFileName = string.Empty;
    private string _reconciliationSearchText = string.Empty;
    private decimal _reconciliationTolerance = 2.00m;
    private string _companyGstin = string.Empty;
    private string _companyName = string.Empty;

    public GSTReportsViewModel(IGstReportRepository gstReportRepository, string currentUser, ISettingsRepository? settingsRepository = null)
    {
        _gstReportRepository = gstReportRepository;
        _currentUser = currentUser;
        _settingsRepository = settingsRepository;

        // Default to current month
        var today = DateTime.Today;
        _fromDate = new DateTime(today.Year, today.Month, 1);
        _toDate = _fromDate.AddMonths(1).AddDays(-1);

        RefreshCommand = new AsyncRelayCommand(LoadActiveReportAsync, () => !IsBusy);
        ExportCommand = new RelayCommand(ExportReport);
        ExportGstr1ExcelCommand = new RelayCommand(ExportGstr1Excel);
        GenerateGstr1JsonCommand = new RelayCommand(GenerateGstr1Json);
        PreviewGstr1JsonCommand = new RelayCommand(PreviewGstr1Json);
        GenerateGstr3BJsonCommand = new RelayCommand(GenerateGstr3BJson);
        PreviewGstr3BJsonCommand = new RelayCommand(PreviewGstr3BJson);
        ImportPortalFileCommand = new RelayCommand(ImportPortalFile);
        RunReconciliationCommand = new RelayCommand(ExecuteReconciliation);

        SetThisMonthCommand = new RelayCommand(SetThisMonth);
        SetLastMonthCommand = new RelayCommand(SetLastMonth);
        SetThisQuarterCommand = new RelayCommand(SetThisQuarter);
        SetThisFYCommand = new RelayCommand(SetThisFY);

        _ = LoadActiveReportAsync();
    }

    // ── Properties ───────────────────────────────────────────────────────────

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                _ = LoadActiveReportAsync();
            }
        }
    }

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public string SelectedVouType
    {
        get => _selectedVouType;
        set => SetProperty(ref _selectedVouType, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ToastMessage
    {
        get => _toastMessage;
        private set => SetProperty(ref _toastMessage, value);
    }

    public ToastType ToastType
    {
        get => _toastType;
        private set => SetProperty(ref _toastType, value);
    }

    // GSTR-1 Collections
    public Gstr1Report Gstr1Report
    {
        get => _gstr1Report;
        private set => SetProperty(ref _gstr1Report, value);
    }

    public ObservableCollection<Gstr1B2BInvoice> B2BInvoices
    {
        get => _b2bInvoices;
        set => SetProperty(ref _b2bInvoices, value);
    }

    public ObservableCollection<Gstr1B2CLargeInvoice> B2CLargeInvoices
    {
        get => _b2cLargeInvoices;
        set => SetProperty(ref _b2cLargeInvoices, value);
    }

    public ObservableCollection<Gstr1B2CSmallSummary> B2CSmallSummaries
    {
        get => _b2cSmallSummaries;
        set => SetProperty(ref _b2cSmallSummaries, value);
    }

    public ObservableCollection<Gstr1CreditDebitNote> CreditDebitNotes
    {
        get => _creditDebitNotes;
        set => SetProperty(ref _creditDebitNotes, value);
    }

    public ObservableCollection<Gstr1HsnSummary> HsnSummaries
    {
        get => _hsnSummaries;
        set => SetProperty(ref _hsnSummaries, value);
    }

    public ObservableCollection<Gstr1HsnSummary> HsnB2BSummaries
    {
        get => _hsnB2BSummaries;
        set => SetProperty(ref _hsnB2BSummaries, value);
    }

    public ObservableCollection<Gstr1HsnSummary> HsnB2CSummaries
    {
        get => _hsnB2CSummaries;
        set => SetProperty(ref _hsnB2CSummaries, value);
    }

    public ObservableCollection<Gstr1DocumentSummary> DocumentSummaries
    {
        get => _documentSummaries;
        set => SetProperty(ref _documentSummaries, value);
    }

    // GSTR-3B
    public Gstr3BReport Gstr3BReport
    {
        get => _gstr3BReport;
        private set => SetProperty(ref _gstr3BReport, value);
    }

    // GSTR-2 Purchase Register
    public ObservableCollection<GstPurchaseRegisterItem> PurchaseRegisterItems
    {
        get => _purchaseRegisterItems;
        set => SetProperty(ref _purchaseRegisterItems, value);
    }

    // Reconciliation
    public ObservableCollection<GstReconciliationItem> FilteredReconciliationItems
    {
        get => _filteredReconciliationItems;
        set => SetProperty(ref _filteredReconciliationItems, value);
    }

    public GstReconciliationSummary ReconciliationSummary
    {
        get => _reconciliationSummary;
        set => SetProperty(ref _reconciliationSummary, value);
    }

    public string SelectedMatchFilter
    {
        get => _selectedMatchFilter;
        set
        {
            if (SetProperty(ref _selectedMatchFilter, value))
            {
                ApplyReconciliationFilter();
            }
        }
    }

    public string ReconciliationSearchText
    {
        get => _reconciliationSearchText;
        set
        {
            if (SetProperty(ref _reconciliationSearchText, value))
            {
                ApplyReconciliationFilter();
            }
        }
    }

    public string UploadedFileName
    {
        get => _uploadedFileName;
        set => SetProperty(ref _uploadedFileName, value);
    }

    public decimal ReconciliationTolerance
    {
        get => _reconciliationTolerance;
        set => SetProperty(ref _reconciliationTolerance, value);
    }

    public string CompanyGstin
    {
        get => _companyGstin;
        private set => SetProperty(ref _companyGstin, value);
    }

    public string CompanyName
    {
        get => _companyName;
        private set => SetProperty(ref _companyName, value);
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ExportGstr1ExcelCommand { get; }
    public RelayCommand GenerateGstr1JsonCommand { get; }
    public RelayCommand PreviewGstr1JsonCommand { get; }
    public RelayCommand GenerateGstr3BJsonCommand { get; }
    public RelayCommand PreviewGstr3BJsonCommand { get; }
    public RelayCommand ImportPortalFileCommand { get; }
    public RelayCommand RunReconciliationCommand { get; }

    public RelayCommand SetThisMonthCommand { get; }
    public RelayCommand SetLastMonthCommand { get; }
    public RelayCommand SetThisQuarterCommand { get; }
    public RelayCommand SetThisFYCommand { get; }

    // ── Data Loading ─────────────────────────────────────────────────────────

    public async Task LoadActiveReportAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading GST report data...";
        try
        {
            if (string.IsNullOrWhiteSpace(CompanyGstin))
            {
                var (dbGstin, dbCompany, _) = await _gstReportRepository.GetCompanyDetailsAsync();
                if (!string.IsNullOrWhiteSpace(dbGstin))
                {
                    CompanyGstin = dbGstin;
                    CompanyName = dbCompany;
                }
            }

            switch (SelectedTabIndex)
            {
                case 0: // GSTR-1
                    var gstr1 = await _gstReportRepository.GetGstr1ReportAsync(FromDate, ToDate, SelectedVouType);
                    Gstr1Report = gstr1;
                    B2BInvoices = new ObservableCollection<Gstr1B2BInvoice>(gstr1.B2BInvoices);
                    B2CLargeInvoices = new ObservableCollection<Gstr1B2CLargeInvoice>(gstr1.B2CLargeInvoices);
                    B2CSmallSummaries = new ObservableCollection<Gstr1B2CSmallSummary>(gstr1.B2CSmallSummaries);
                    CreditDebitNotes = new ObservableCollection<Gstr1CreditDebitNote>(gstr1.CreditDebitNotes);
                    HsnSummaries = new ObservableCollection<Gstr1HsnSummary>(gstr1.HsnSummaries);
                    var b2bHsn = gstr1.HsnB2BSummaries != null && gstr1.HsnB2BSummaries.Count > 0
                        ? gstr1.HsnB2BSummaries
                        : new List<Gstr1HsnSummary>();
                    var b2cHsn = gstr1.HsnB2CSummaries != null && gstr1.HsnB2CSummaries.Count > 0
                        ? gstr1.HsnB2CSummaries
                        : (gstr1.B2BInvoices.Count == 0 ? gstr1.HsnSummaries : new List<Gstr1HsnSummary>());
                    HsnB2BSummaries = new ObservableCollection<Gstr1HsnSummary>(b2bHsn);
                    HsnB2CSummaries = new ObservableCollection<Gstr1HsnSummary>(b2cHsn);
                    DocumentSummaries = new ObservableCollection<Gstr1DocumentSummary>(gstr1.DocumentSummaries);
                    StatusMessage = $"GSTR-1: {gstr1.TotalInvoiceCount} Invoices | Taxable: ₹{gstr1.TotalTaxableAmount:N2} | Total Tax: ₹{gstr1.TotalTaxAmount:N2}";
                    break;

                case 1: // GSTR-3B
                    var gstr3b = await _gstReportRepository.GetGstr3BReportAsync(FromDate, ToDate);
                    Gstr3BReport = gstr3b;
                    StatusMessage = $"GSTR-3B ({gstr3b.Period}): Output Tax: ₹{gstr3b.TotalTaxLiability.Total:N2} | ITC Available: ₹{gstr3b.NetItcAvailable.TotalItc:N2} | Net Cash: ₹{gstr3b.NetTaxPayableInCash.Total:N2}";
                    break;

                case 2: // GSTR-2 Purchase Register
                    var purchases = await _gstReportRepository.GetGstPurchaseRegisterAsync(FromDate, ToDate);
                    PurchaseRegisterItems = new ObservableCollection<GstPurchaseRegisterItem>(purchases);
                    var sumTaxable = purchases.Sum(p => p.TaxableAmount);
                    var sumTax = purchases.Sum(p => p.TotalTax);
                    StatusMessage = $"Purchase Register: {purchases.Count} Vouchers | Taxable: ₹{sumTaxable:N2} | Total ITC: ₹{sumTax:N2}";
                    break;

                case 3: // Reconciliation
                    if (_portalInvoices.Count > 0)
                    {
                        ExecuteReconciliation();
                    }
                    else
                    {
                        StatusMessage = "Upload GSTR-2B / GSTR-2A file (JSON / CSV / Excel) to start reconciliation.";
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowToast("Error loading GST report: " + ex.Message, ToastType.Error);
            StatusMessage = "Error: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ImportPortalFile()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "GST Portal Files (*.json;*.csv;*.txt)|*.json;*.csv;*.txt|JSON Files (*.json)|*.json|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Select GSTR-2B or GSTR-2A File from GST Portal"
            };

            if (dialog.ShowDialog() == true)
            {
                UploadedFileName = Path.GetFileName(dialog.FileName);
                _portalInvoices = GstReconciliationService.ParsePortalFile(dialog.FileName);
                ShowToast($"Successfully imported {_portalInvoices.Count} records from {UploadedFileName}", ToastType.Success);
                ExecuteReconciliation();
            }
        }
        catch (Exception ex)
        {
            ShowToast("Failed to parse GSTR file: " + ex.Message, ToastType.Error);
        }
    }

    private async void ExecuteReconciliation()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Reconciling ERP Purchase Books against Portal GSTR-2B...";

            var books = await _gstReportRepository.GetGstPurchaseRegisterAsync(FromDate, ToDate);
            var (reconciled, summary) = GstReconciliationService.Reconcile(books, _portalInvoices, ReconciliationTolerance);

            _allReconciliationItems = reconciled;
            ReconciliationSummary = summary;

            ApplyReconciliationFilter();

            StatusMessage = $"Reconciliation Complete: {summary.MatchedCount} Matched | {summary.MismatchCount} Mismatches | {summary.MissingIn2BCount} In Books Only | {summary.MissingInBooksCount} In 2B Only";
            ShowToast($"Reconciliation Complete: {summary.MatchedCount} Matched, {summary.MissingIn2BCount} Risk items", ToastType.Info);
        }
        catch (Exception ex)
        {
            ShowToast("Reconciliation failed: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyReconciliationFilter()
    {
        IEnumerable<GstReconciliationItem> q = _allReconciliationItems;

        if (SelectedMatchFilter == "MATCHED")
        {
            q = q.Where(x => x.Status == GstMatchStatus.Matched);
        }
        else if (SelectedMatchFilter == "MISMATCH")
        {
            q = q.Where(x => x.Status is GstMatchStatus.MismatchInTaxable or GstMatchStatus.MismatchInTax or GstMatchStatus.MismatchInDate);
        }
        else if (SelectedMatchFilter == "MISSING_IN_2B")
        {
            q = q.Where(x => x.Status == GstMatchStatus.MissingIn2B);
        }
        else if (SelectedMatchFilter == "MISSING_IN_BOOKS")
        {
            q = q.Where(x => x.Status == GstMatchStatus.MissingInBooks);
        }

        if (!string.IsNullOrWhiteSpace(ReconciliationSearchText))
        {
            var term = ReconciliationSearchText.Trim();
            q = q.Where(x => (x.BooksSupplierName != null && x.BooksSupplierName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                             (x.PortalSupplierName != null && x.PortalSupplierName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                             (x.BooksGstin != null && x.BooksGstin.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                             (x.PortalGstin != null && x.PortalGstin.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                             (x.BooksBillNo != null && x.BooksBillNo.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                             (x.PortalBillNo != null && x.PortalBillNo.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredReconciliationItems = new ObservableCollection<GstReconciliationItem>(q.ToList());
    }

    // ── GSTR-1 JSON Generation ──────────────────────────────────────────────

    public async void GenerateGstr1Json()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Preparing GSTR-1 figures for JSON return...";

            Gstr1Report = await _gstReportRepository.GetGstr1ReportAsync(FromDate, ToDate, SelectedVouType);

            var (gstin, company, state) = await ResolveCompanyGstDetailsAsync();
            var period = Gstr1JsonService.FormatReturnPeriod(ToDate != default ? ToDate : (FromDate != default ? FromDate : DateTime.Today));

            var dialog = new SaveFileDialog
            {
                Title = $"Save GSTR-1 JSON File - {company} ({gstin})",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                FileName = $"GSTR1_{gstin}_{period}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                var json = Gstr1JsonService.GenerateJson(Gstr1Report, gstin, period, state, indented: false);
                File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
                ShowToast($"✓ GSTR-1 JSON saved successfully for {company} ({gstin})", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Failed to generate GSTR-1 JSON: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async void PreviewGstr1Json()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading GSTR-1 figures for JSON preview...";

            Gstr1Report = await _gstReportRepository.GetGstr1ReportAsync(FromDate, ToDate, SelectedVouType);

            var (gstin, company, state) = await ResolveCompanyGstDetailsAsync();
            var period = Gstr1JsonService.FormatReturnPeriod(ToDate != default ? ToDate : (FromDate != default ? FromDate : DateTime.Today));

            var vm = new Gstr1ExportViewModel(Gstr1Report, gstin, period, company, state);
            var win = new Gstr1ExportWindow(vm);

            if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
            {
                win.Owner = System.Windows.Application.Current.MainWindow;
            }

            win.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowToast("Failed to open GSTR-1 preview: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── GSTR-3B JSON Generation ──────────────────────────────────────────────

    public async void GenerateGstr3BJson()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Preparing GSTR-3B figures for JSON return...";

            if (Gstr3BReport.TotalTaxLiability.Total == 0 && Gstr3BReport.OutwardTaxableSupplies.TotalTaxableValue == 0)
            {
                Gstr3BReport = await _gstReportRepository.GetGstr3BReportAsync(FromDate, ToDate);
            }

            var (gstin, company, _) = await ResolveCompanyGstDetailsAsync();
            var period = Gstr3BJsonService.FormatReturnPeriod(FromDate != default ? FromDate : DateTime.Today);

            var dialog = new SaveFileDialog
            {
                Title = $"Save GSTR-3B JSON File - {company} ({gstin})",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                FileName = $"GSTR3B_{gstin}_{period}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                var json = Gstr3BJsonService.GenerateJson(Gstr3BReport, gstin, period, indented: false);
                File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
                ShowToast($"✓ GSTR-3B JSON saved successfully for {company} ({gstin})", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Failed to generate GSTR-3B JSON: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async void PreviewGstr3BJson()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading GSTR-3B figures for JSON preview...";

            if (Gstr3BReport.TotalTaxLiability.Total == 0 && Gstr3BReport.OutwardTaxableSupplies.TotalTaxableValue == 0)
            {
                Gstr3BReport = await _gstReportRepository.GetGstr3BReportAsync(FromDate, ToDate);
            }

            var (gstin, company, _) = await ResolveCompanyGstDetailsAsync();
            var period = Gstr3BJsonService.FormatReturnPeriod(FromDate != default ? FromDate : DateTime.Today);

            var vm = new Gstr3BExportViewModel(Gstr3BReport, gstin, period, company);
            var win = new Gstr3BExportWindow(vm);

            if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
            {
                win.Owner = System.Windows.Application.Current.MainWindow;
            }

            win.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowToast("Failed to open GSTR-3B preview: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<(string Gstin, string CompanyName, string StateCode)> ResolveCompanyGstDetailsAsync()
    {
        try
        {
            var (dbGstin, dbCompany, dbState) = await _gstReportRepository.GetCompanyDetailsAsync();
            if (!string.IsNullOrWhiteSpace(dbGstin))
            {
                CompanyGstin = dbGstin;
                CompanyName = dbCompany;
                return (dbGstin, dbCompany, !string.IsNullOrWhiteSpace(dbState) ? dbState : "21");
            }
        }
        catch
        {
        }

        if (_settingsRepository != null)
        {
            try
            {
                var settings = await _settingsRepository.GetAllAsync();
                if (settings.TryGetValue("GSTIN", out var g) && !string.IsNullOrWhiteSpace(g)) return (g.Trim(), "TATTVA JEWELS", "21");
                if (settings.TryGetValue("CompanyGstin", out var cg) && !string.IsNullOrWhiteSpace(cg)) return (cg.Trim(), "TATTVA JEWELS", "21");
                if (settings.TryGetValue("StoreGstin", out var sg) && !string.IsNullOrWhiteSpace(sg)) return (sg.Trim(), "TATTVA JEWELS", "21");
            }
            catch
            {
            }
        }

        try
        {
            var einvoiceConfig = EInvoiceService.LoadConfig("LIVE");
            if (!string.IsNullOrWhiteSpace(einvoiceConfig.Gstin) && einvoiceConfig.Gstin.Length == 15)
            {
                return (einvoiceConfig.Gstin.Trim(), einvoiceConfig.LegalName, "21");
            }
        }
        catch
        {
        }

        return ("21AGVPV8495M1Z5", "TATTVA JEWELS", "21");
    }

    // ── Exporting ────────────────────────────────────────────────────────────

    public async void ExportGstr1Excel()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Preparing GSTR-1 Excel workbook (official offline format)...";

            Gstr1Report = await _gstReportRepository.GetGstr1ReportAsync(FromDate, ToDate, SelectedVouType);
            var (gstin, company, state) = await ResolveCompanyGstDetailsAsync();
            var period = Gstr1JsonService.FormatReturnPeriod(ToDate != default ? ToDate : (FromDate != default ? FromDate : DateTime.Today));

            var dialog = new SaveFileDialog
            {
                Title = $"Export GSTR-1 Excel Return - {company} ({gstin})",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"GSTR1_{gstin}_{period}.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                if (ext == ".csv")
                {
                    ExportGstr1Csv(dialog.FileName);
                }
                else
                {
                    GstrExcelService.ExportGstr1ToFile(Gstr1Report, dialog.FileName, gstin, period, company, state);
                }
                ShowToast($"✓ GSTR-1 Excel exported successfully for {company}", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Failed to export GSTR-1 Excel: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExportReport()
    {
        try
        {
            var (defaultGstin, company, state) = await ResolveCompanyGstDetailsAsync();
            var returnPeriod = Gstr1JsonService.FormatReturnPeriod(ToDate != default ? ToDate : (FromDate != default ? FromDate : DateTime.Today));

            var filter = SelectedTabIndex switch
            {
                0 => "Excel Workbook (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                1 => "Excel Workbook (*.xlsx)|*.xlsx|JSON Files (*.json)|*.json|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                2 => "Excel Workbook (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                3 => "Excel Workbook (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                _ => "Excel Workbook (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            var defaultName = SelectedTabIndex switch
            {
                0 => $"GSTR1_{defaultGstin}_{returnPeriod}.xlsx",
                1 => $"GSTR3B_{defaultGstin}_{returnPeriod}.xlsx",
                2 => $"GSTR2_PurchaseRegister_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.xlsx",
                3 => $"GST_Reconciliation_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                _ => $"GST_Report_{SelectedTabName()}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultName
            };

            if (dialog.ShowDialog() == true)
            {
                var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();

                // ── EXCEL EXPORTS (.xlsx) ──
                if (ext == ".xlsx")
                {
                    switch (SelectedTabIndex)
                    {
                        case 0: // GSTR-1 (Offline Utility Workbook: b2b, b2cl, b2cs, cdnr, hsn, docs, Summary)
                            Gstr1Report = await _gstReportRepository.GetGstr1ReportAsync(FromDate, ToDate, SelectedVouType);
                            GstrExcelService.ExportGstr1ToFile(Gstr1Report, dialog.FileName, defaultGstin, returnPeriod, company, state);
                            ShowToast($"✓ GSTR-1 Excel exported successfully ({Path.GetFileName(dialog.FileName)})", ToastType.Success);
                            return;

                        case 1: // GSTR-3B
                            if (Gstr3BReport.TotalTaxLiability.Total == 0 && Gstr3BReport.OutwardTaxableSupplies.TotalTaxableValue == 0)
                            {
                                Gstr3BReport = await _gstReportRepository.GetGstr3BReportAsync(FromDate, ToDate);
                            }
                            GstrExcelService.ExportGstr3BToFile(Gstr3BReport, dialog.FileName, defaultGstin, returnPeriod, company);
                            ShowToast($"✓ GSTR-3B Excel exported successfully ({Path.GetFileName(dialog.FileName)})", ToastType.Success);
                            return;

                        case 2: // Purchase Register
                            var purchases = PurchaseRegisterItems.Count > 0 ? PurchaseRegisterItems : await _gstReportRepository.GetGstPurchaseRegisterAsync(FromDate, ToDate);
                            GstrExcelService.ExportPurchaseRegisterToFile(purchases, dialog.FileName, company, FromDate, ToDate);
                            ShowToast($"✓ Purchase Register Excel exported successfully ({Path.GetFileName(dialog.FileName)})", ToastType.Success);
                            return;

                        case 3: // Reconciliation
                            IEnumerable<GstReconciliationItem> reconList = FilteredReconciliationItems.Count > 0 ? FilteredReconciliationItems : _allReconciliationItems;
                            GstrExcelService.ExportReconciliationToFile(reconList, ReconciliationSummary, dialog.FileName, company, FromDate, ToDate);
                            ShowToast($"✓ GST Reconciliation Excel exported successfully ({Path.GetFileName(dialog.FileName)})", ToastType.Success);
                            return;
                    }
                }

                // ── JSON EXPORTS (.json) ──
                if (SelectedTabIndex == 0 && ext == ".json")
                {
                    Gstr1Report = await _gstReportRepository.GetGstr1ReportAsync(FromDate, ToDate, SelectedVouType);
                    var json = Gstr1JsonService.GenerateJson(Gstr1Report, defaultGstin, returnPeriod, state, indented: false);
                    File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
                    ShowToast("GSTR-1 JSON exported successfully", ToastType.Success);
                    return;
                }

                if (SelectedTabIndex == 1 && ext == ".json")
                {
                    var json = Gstr3BJsonService.GenerateJson(Gstr3BReport, defaultGstin, returnPeriod, indented: false);
                    File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
                    ShowToast("GSTR-3B JSON exported successfully", ToastType.Success);
                    return;
                }

                // ── CSV EXPORTS (.csv) ──
                var sb = new StringBuilder();

                switch (SelectedTabIndex)
                {
                    case 0: // GSTR-1
                        ExportGstr1Csv(dialog.FileName);
                        ShowToast("Report exported successfully", ToastType.Success);
                        return;

                    case 1: // GSTR-3B
                        sb.AppendLine("SECTION,DESCRIPTION,TAXABLE VALUE,INTEGRATED TAX,CENTRAL TAX,STATE TAX,TOTAL TAX");
                        sb.AppendLine($"\"3.1\",\"Outward Taxable\",{Gstr3BReport.OutwardTaxableSupplies.TotalTaxableValue:F2},{Gstr3BReport.OutwardTaxableSupplies.IntegratedTax:F2},{Gstr3BReport.OutwardTaxableSupplies.CentralTax:F2},{Gstr3BReport.OutwardTaxableSupplies.StateTax:F2},{Gstr3BReport.OutwardTaxableSupplies.TotalTax:F2}");
                        sb.AppendLine($"\"4\",\"Eligible ITC (Purchases/Expenses)\",0,{Gstr3BReport.NetItcAvailable.IntegratedTax:F2},{Gstr3BReport.NetItcAvailable.CentralTax:F2},{Gstr3BReport.NetItcAvailable.StateTax:F2},{Gstr3BReport.NetItcAvailable.TotalItc:F2}");
                        sb.AppendLine($"\"6\",\"Net Cash Payable\",0,{Gstr3BReport.NetTaxPayableInCash.IntegratedTax:F2},{Gstr3BReport.NetTaxPayableInCash.CentralTax:F2},{Gstr3BReport.NetTaxPayableInCash.StateTax:F2},{Gstr3BReport.NetTaxPayableInCash.Total:F2}");
                        break;

                    case 2: // Purchase Register
                        sb.AppendLine("VOU NO,BILL NO,DATE,SUPPLIER,GSTIN,VOU TYPE,GROSS WT,NET WT,TAXABLE,RATE,IGST,CGST,SGST,TOTAL TAX,NET AMOUNT");
                        foreach (var p in PurchaseRegisterItems)
                            sb.AppendLine($"\"{p.VouNo}\",\"{p.SuppBillNo}\",{p.BillDate:dd/MM/yyyy},\"{p.SupplierName}\",\"{p.SupplierGstin}\",\"{p.VoucherType}\",{p.GrossWt:F3},{p.NetWt:F3},{p.TaxableAmount:F2},{p.GstRate:F2},{p.IgstAmount:F2},{p.CgstAmount:F2},{p.SgstAmount:F2},{p.TotalTax:F2},{p.NetAmount:F2}");
                        break;

                    case 3: // Reconciliation
                        sb.AppendLine("STATUS,BOOKS BILL NO,BOOKS DATE,BOOKS SUPPLIER,BOOKS GSTIN,BOOKS TAXABLE,BOOKS TAX,PORTAL BILL NO,PORTAL DATE,PORTAL SUPPLIER,PORTAL GSTIN,PORTAL TAXABLE,PORTAL TAX,TAX DIFF,REMARKS");
                        foreach (var r in FilteredReconciliationItems)
                            sb.AppendLine($"\"{r.StatusText}\",\"{r.BooksBillNo}\",{(r.BooksDate.HasValue ? r.BooksDate.Value.ToString("dd/MM/yyyy") : "")},\"{r.BooksSupplierName}\",\"{r.BooksGstin}\",{r.BooksTaxableValue:F2},{r.BooksTotalTax:F2},\"{r.PortalBillNo}\",{(r.PortalDate.HasValue ? r.PortalDate.Value.ToString("dd/MM/yyyy") : "")},\"{r.PortalSupplierName}\",\"{r.PortalGstin}\",{r.PortalTaxableValue:F2},{r.PortalTotalTax:F2},{r.DifferenceTax:F2},\"{r.Remarks}\"");
                        break;
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                ShowToast("Report exported successfully", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Export failed: " + ex.Message, ToastType.Error);
        }
    }

    private void ExportGstr1Csv(string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SECTION,GSTIN,RECEIVER NAME,INVOICE NO,DATE,VALUE,POS,RATE,TAXABLE VALUE,IGST,CGST,SGST");
        foreach (var b in B2BInvoices)
            sb.AppendLine($"\"B2B\",\"{b.Gstin}\",\"{b.ReceiverName}\",\"{b.InvoiceNo}\",{b.InvoiceDate:dd/MM/yyyy},{b.InvoiceValue:F2},\"{b.PlaceOfSupply}\",{b.GstRate:F2},{b.TaxableValue:F2},{b.IgstAmount:F2},{b.CgstAmount:F2},{b.SgstAmount:F2}");
        foreach (var l in B2CLargeInvoices)
            sb.AppendLine($"\"B2CL\",\"\",,\"{l.InvoiceNo}\",{l.InvoiceDate:dd/MM/yyyy},{l.InvoiceValue:F2},\"{l.PlaceOfSupply}\",{l.GstRate:F2},{l.TaxableValue:F2},{l.IgstAmount:F2},0,0");
        foreach (var s in B2CSmallSummaries)
            sb.AppendLine($"\"B2CS\",,,,,\"{s.PlaceOfSupply}\",{s.GstRate:F2},{s.TaxableValue:F2},{s.IgstAmount:F2},{s.CgstAmount:F2},{s.SgstAmount:F2}");
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private string SelectedTabName() => SelectedTabIndex switch
    {
        0 => "GSTR1",
        1 => "GSTR3B",
        2 => "GSTR2_PurchaseRegister",
        3 => "GST_Reconciliation",
        _ => "GST"
    };

    // ── Quick Date Range Setters ─────────────────────────────────────────────

    private void SetThisMonth()
    {
        var today = DateTime.Today;
        FromDate = new DateTime(today.Year, today.Month, 1);
        ToDate = FromDate.AddMonths(1).AddDays(-1);
        _ = LoadActiveReportAsync();
    }

    private void SetLastMonth()
    {
        var firstOfCurrent = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate = firstOfCurrent.AddDays(-1);
        FromDate = new DateTime(ToDate.Year, ToDate.Month, 1);
        _ = LoadActiveReportAsync();
    }

    private void SetThisQuarter()
    {
        var today = DateTime.Today;
        int quarter = (today.Month - 1) / 3;
        FromDate = new DateTime(today.Year, quarter * 3 + 1, 1);
        ToDate = FromDate.AddMonths(3).AddDays(-1);
        _ = LoadActiveReportAsync();
    }

    private void SetThisFY()
    {
        var today = DateTime.Today;
        int startYear = today.Month >= 4 ? today.Year : today.Year - 1;
        FromDate = new DateTime(startYear, 4, 1);
        ToDate = new DateTime(startYear + 1, 3, 31);
        _ = LoadActiveReportAsync();
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastType = type;
        ToastMessage = string.Empty;
        ToastMessage = message;
    }
}
