using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Services;
using ConceptJewelleryPro.WPF.Views;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class PurchaseRegisterViewModel : ViewModelBase
{
    private static readonly CultureInfo IndianCulture = new("en-IN");
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly string _currentUser;

    private DateTime _fromDate;
    private DateTime _toDate;
    private string _selectedItemType = "*";
    private string _selectedVouType = "ALL";
    private string _searchText = string.Empty;
    private PurchaseRegisterItem? _selectedPurchase;
    private int _selectedRecordIndex = 1;

    private int _totalPurchases;
    private decimal _totalQty;
    private decimal _totalGrossWeight;
    private decimal _totalNetWeight;
    private decimal _totalTaxableAmount;
    private decimal _totalTaxAmount;
    private decimal _totalIGST;
    private decimal _totalCGST;
    private decimal _totalSGST;
    private decimal _totalRoundOff;
    private decimal _totalNetAmount;

    private string _selectedPrintOption = "ItemwiseDetailedPrint";

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    private readonly List<PurchaseRegisterItem> _allLoadedPurchases = new();

    public event EventHandler<PurchaseRegisterItem>? RequestOpenPurchase;
    public event EventHandler<bool?>? RequestClose;

    public PurchaseRegisterViewModel(IPurchaseRepository purchaseRepository, string currentUser = "admin")
    {
        _purchaseRepository = purchaseRepository;
        _currentUser = currentUser;

        // Default to start of financial year (April 1st)
        var today = DateTime.Today;
        var startYear = today.Month >= 4 ? today.Year : today.Year - 1;
        _fromDate = new DateTime(startYear, 4, 1);
        _toDate = today;

        Purchases = new ObservableCollection<PurchaseRegisterItem>();
        AvailableItemTypes = new ObservableCollection<string> { "*" };
        AvailableVouTypes = new ObservableCollection<string> { "ALL" };

        RefreshCommand = new AsyncRelayCommand(LoadPurchasesAsync);
        SelectPurchaseCommand = new RelayCommand(SelectPurchase);
        CloseCommand = new RelayCommand(Close);
        PrintReportCommand = new RelayCommand(PrintReport);
        ExportExcelCommand = new RelayCommand(ExportExcel);

        FirstRecordCommand = new RelayCommand(FirstRecord);
        PrevRecordCommand = new RelayCommand(PrevRecord);
        NextRecordCommand = new RelayCommand(NextRecord);
        LastRecordCommand = new RelayCommand(LastRecord);
        ToggleExpandAllCommand = new RelayCommand(ToggleExpandAll);

        _ = InitializeAsync();
    }

    public ObservableCollection<PurchaseRegisterItem> Purchases { get; }
    public ObservableCollection<string> AvailableItemTypes { get; }
    public ObservableCollection<string> AvailableVouTypes { get; }

    public ICommand RefreshCommand { get; }
    public ICommand SelectPurchaseCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand PrintReportCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand FirstRecordCommand { get; }
    public ICommand PrevRecordCommand { get; }
    public ICommand NextRecordCommand { get; }
    public ICommand LastRecordCommand { get; }
    public ICommand ToggleExpandAllCommand { get; }

    public DateTime FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                _ = LoadPurchasesAsync();
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
                _ = LoadPurchasesAsync();
            }
        }
    }

    public string SelectedItemType
    {
        get => _selectedItemType;
        set
        {
            if (SetProperty(ref _selectedItemType, value))
            {
                _ = LoadPurchasesAsync();
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
                _ = LoadPurchasesAsync();
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

    public PurchaseRegisterItem? SelectedPurchase
    {
        get => _selectedPurchase;
        set
        {
            if (SetProperty(ref _selectedPurchase, value))
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

    public int TotalPurchases
    {
        get => _totalPurchases;
        set => SetProperty(ref _totalPurchases, value);
    }

    public decimal TotalQty
    {
        get => _totalQty;
        set => SetProperty(ref _totalQty, value);
    }

    public decimal TotalGrossWeight
    {
        get => _totalGrossWeight;
        set => SetProperty(ref _totalGrossWeight, value);
    }

    public decimal TotalNetWeight
    {
        get => _totalNetWeight;
        set => SetProperty(ref _totalNetWeight, value);
    }

    public decimal TotalTaxableAmount
    {
        get => _totalTaxableAmount;
        set => SetProperty(ref _totalTaxableAmount, value);
    }

    public decimal TotalTaxAmount
    {
        get => _totalTaxAmount;
        set => SetProperty(ref _totalTaxAmount, value);
    }

    public decimal TotalIGST
    {
        get => _totalIGST;
        set => SetProperty(ref _totalIGST, value);
    }

    public decimal TotalCGST
    {
        get => _totalCGST;
        set => SetProperty(ref _totalCGST, value);
    }

    public decimal TotalSGST
    {
        get => _totalSGST;
        set => SetProperty(ref _totalSGST, value);
    }

    public decimal TotalRoundOff
    {
        get => _totalRoundOff;
        set => SetProperty(ref _totalRoundOff, value);
    }

    public decimal TotalNetAmount
    {
        get => _totalNetAmount;
        set => SetProperty(ref _totalNetAmount, value);
    }

    public string SelectedPrintOption
    {
        get => _selectedPrintOption;
        set => SetProperty(ref _selectedPrintOption, value);
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

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
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

    public DateRangeCompanyInfo? CompanyDetails { get; private set; }
    public string CompanyName => CompanyDetails?.CompanyName ?? string.Empty;
    public string Address => CompanyDetails?.Address ?? string.Empty;
    public string Gstin => CompanyDetails?.Gstin ?? string.Empty;

    public async Task LoadCompanyDetailsAsync()
    {
        try
        {
            CompanyDetails = await _purchaseRepository.GetCompanyDetailsAsync();
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
        try
        {
            await LoadCompanyDetailsAsync();
            // Load available voucher types
            var types = await _purchaseRepository.GetPurchaseVouTypesAsync();
            AvailableVouTypes.Clear();
            AvailableVouTypes.Add("ALL");
            foreach (var t in types.Select(x => x.VouType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            {
                if (!string.IsNullOrWhiteSpace(t))
                {
                    AvailableVouTypes.Add(t);
                }
            }

            // Load available item types
            var itemTypes = await _purchaseRepository.GetAvailableItemTypesAsync();
            AvailableItemTypes.Clear();
            AvailableItemTypes.Add("*");
            foreach (var it in itemTypes)
            {
                if (!string.IsNullOrWhiteSpace(it))
                {
                    AvailableItemTypes.Add(it);
                }
            }

            await LoadPurchasesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error initializing purchase register: " + ex.Message;
        }
    }

    public async Task LoadPurchasesAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Loading purchase register...";

        try
        {
            var filter = new PurchaseRegisterFilter
            {
                FromDate = _fromDate,
                ToDate = _toDate,
                ItemType = _selectedItemType,
                VouType = _selectedVouType,
                SearchText = _searchText
            };

            var list = await _purchaseRepository.GetPurchaseRegisterAsync(filter);

            _allLoadedPurchases.Clear();
            _allLoadedPurchases.AddRange(list);

            ApplyFilter();

            StatusMessage = $"Loaded {Purchases.Count} purchases ({FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy}).";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error loading purchases: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<PurchaseRegisterItem> query = _allLoadedPurchases;

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var term = _searchText.Trim();
            query = query.Where(p =>
                p.VouNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.SuppBillNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.PartyDisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Phone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.City.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.VatNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.CustomerCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Items.Any(it => it.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase))
            );
        }

        Purchases.Clear();
        foreach (var p in query)
        {
            Purchases.Add(p);
        }

        RecalculateTotals();

        if (Purchases.Count > 0)
        {
            SelectedPurchase = Purchases[0];
        }
        else
        {
            SelectedPurchase = null;
            _selectedRecordIndex = 0;
            OnPropertyChanged(nameof(SelectedRecordIndex));
        }
    }

    private void RecalculateTotals()
    {
        TotalPurchases = Purchases.Count;
        TotalQty = Purchases.Sum(p => p.TotalQty > 0 ? p.TotalQty : p.Items.Sum(x => x.Qty));
        TotalGrossWeight = Purchases.Sum(p => p.GrWt > 0 ? p.GrWt : p.Items.Sum(x => x.GrWt));
        TotalNetWeight = Purchases.Sum(p => p.NtWt > 0 ? p.NtWt : p.Items.Sum(x => x.NtWt));
        TotalTaxableAmount = Purchases.Sum(p => p.TaxableAmount > 0 ? p.TaxableAmount : p.Items.Sum(x => x.TaxableAmount));
        TotalTaxAmount = Purchases.Sum(p => p.TaxAmt > 0 ? p.TaxAmt : p.Items.Sum(x => x.GstAmt));
        TotalIGST = Purchases.Sum(p => p.IGSTAmt);
        TotalCGST = Purchases.Sum(p => p.CGSTAmt);
        TotalSGST = Purchases.Sum(p => p.SGSTAmt);
        TotalRoundOff = Purchases.Sum(p => p.AddLess);
        TotalNetAmount = Purchases.Sum(p => p.NetAmt);
    }

    private void UpdateRecordIndex()
    {
        if (SelectedPurchase != null)
        {
            var idx = Purchases.IndexOf(SelectedPurchase);
            if (idx >= 0)
            {
                _selectedRecordIndex = idx + 1;
                OnPropertyChanged(nameof(SelectedRecordIndex));
            }
        }
    }

    private void NavigateToIndex(int index)
    {
        if (index >= 1 && index <= Purchases.Count)
        {
            var target = Purchases[index - 1];
            if (!ReferenceEquals(SelectedPurchase, target))
            {
                _selectedPurchase = target;
                OnPropertyChanged(nameof(SelectedPurchase));
            }
        }
    }

    private void FirstRecord()
    {
        if (Purchases.Count > 0)
        {
            SelectedPurchase = Purchases[0];
        }
    }

    private void PrevRecord()
    {
        if (Purchases.Count == 0) return;
        var idx = SelectedPurchase != null ? Purchases.IndexOf(SelectedPurchase) : 0;
        if (idx > 0)
        {
            SelectedPurchase = Purchases[idx - 1];
        }
    }

    private void NextRecord()
    {
        if (Purchases.Count == 0) return;
        var idx = SelectedPurchase != null ? Purchases.IndexOf(SelectedPurchase) : -1;
        if (idx < Purchases.Count - 1)
        {
            SelectedPurchase = Purchases[idx + 1];
        }
    }

    private void LastRecord()
    {
        if (Purchases.Count > 0)
        {
            SelectedPurchase = Purchases[Purchases.Count - 1];
        }
    }

    private bool _allExpanded;
    private void ToggleExpandAll()
    {
        _allExpanded = !_allExpanded;
        foreach (var p in Purchases)
        {
            p.IsExpanded = _allExpanded;
        }
    }

    private void SelectPurchase(object? parameter)
    {
        var target = parameter as PurchaseRegisterItem ?? SelectedPurchase;
        if (target == null)
        {
            ErrorMessage = "Please select a purchase from the list.";
            return;
        }

        RequestOpenPurchase?.Invoke(this, target);
        RequestClose?.Invoke(this, true);
    }

    private void Close()
    {
        RequestClose?.Invoke(this, false);
    }

    private void PrintReport()
    {
        if (Purchases.Count == 0)
        {
            ErrorMessage = "No purchase records available in the current filter to print.";
            System.Windows.MessageBox.Show(
                "No purchases found for the selected period and filter parameters.",
                "Print Purchase Register",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            var optionName = SelectedPrintOption switch
            {
                "ItemwiseDetailedPrint" or "Itemwise Print Detailed" => "Purchase Register Detailed (Item Wise)",
                "DayWisePrint" => "Day Wise Purchase Register",
                "PartyWisePrint" => "Party Wise Purchase Register",
                "VoucherWisePrint" => "Voucher Wise Purchase Register",
                "ItemWiseSummary" or "Itemwise Print Summary" or "ItemWise Summary" => "PURCHASE REGISTER SUMMARY",
                _ => "Purchase Register Detailed"
            };

            if (CompanyDetails == null || string.IsNullOrWhiteSpace(CompanyDetails.CompanyName))
            {
                try { CompanyDetails = _purchaseRepository.GetCompanyDetailsAsync().GetAwaiter().GetResult(); } catch { }
            }

            var doc = PurchaseRegisterFlowDocumentBuilder.CreateReportDocument(this);

            var preview = new InvoicePrintPreviewWindow(
                document: doc,
                windowTitle: $"{optionName} - Print Preview",
                headerTitle: $"{optionName} ({FromDate:dd/MM/yyyy} to {ToDate:dd/MM/yyyy})",
                docName: optionName);
            preview.SetOwnerSafely();

            preview.ShowDialog();
            StatusMessage = $"Report '{optionName}' generated successfully ({Purchases.Count} records).";
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
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"Purchase_Register_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Pur No,Supp Bill No,Date,Voucher Type,Supplier / Party,Phone,City,Address,GSTIN/PAN,Gross Wt,Net Wt,Taxable Amt,IGST,CGST,SGST,GST Amt,Net Amount,Status");

                foreach (var p in Purchases)
                {
                    sb.AppendLine($"\"{p.VouNo}\",\"{p.SuppBillNo}\",\"{p.BillDate:dd/MM/yyyy}\",\"{p.VouType}\",\"{p.PartyDisplayName.Replace("\"", "\"\"")}\",\"{p.Phone}\",\"{p.City}\",\"{p.Address.Replace("\"", "\"\"")}\",\"{p.VatNo}\",{p.GrWt},{p.NtWt},{p.TaxableAmount},{p.IGSTAmt},{p.CGSTAmt},{p.SGSTAmt},{p.TaxAmt},{p.NetAmt},\"{p.Status}\"");
                }

                File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                StatusMessage = $"Purchases exported to {Path.GetFileName(saveDialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Export failed: " + ex.Message;
        }
    }
}
