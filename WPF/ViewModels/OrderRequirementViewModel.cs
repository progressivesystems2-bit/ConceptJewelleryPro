using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;
using ConceptJewelleryPro.WPF.Views;
using Microsoft.Win32;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class OrderRequirementViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository? _customerRepository;
    private readonly IItemRepository? _itemRepository;
    private readonly IPurityRepository? _purityRepository;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly string _currentUser;

    private string _searchText = string.Empty;
    private string _selectedStatusFilter = "All";
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private bool _showOnlyOpen = true;

    private bool _isBusy;
    private string? _statusMessage;
    private string? _errorMessage;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;
    private CustomerOrder? _selectedOrder;

    // KPI Metrics
    private int _totalOrdersCount;
    private int _pendingOrdersCount;
    private int _completedOrdersCount;
    private decimal _totalApproxValue;
    private decimal _totalAdvanceCollected;
    private decimal _totalBalanceDue;

    public OrderRequirementViewModel(
        IOrderRepository orderRepository,
        ICustomerRepository? customerRepository,
        IItemRepository? itemRepository,
        IPurityRepository? purityRepository,
        ISettingsRepository? settingsRepository,
        string currentUser,
        IAuditRepository? auditRepository = null)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _settingsRepository = settingsRepository;
        _currentUser = currentUser;
        _auditRepository = auditRepository;

        AddNewCommand = new RelayCommand(AddNew);
        ModifyCommand = new RelayCommand(Modify, () => SelectedOrder != null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedOrder != null && !IsBusy);
        RefreshCommand = new AsyncRelayCommand(LoadOrdersAsync, () => !IsBusy);
        ExportToExcelCommand = new RelayCommand(ExportToExcel);
        PrintReceiptCommand = new RelayCommand(PrintReceipt, () => SelectedOrder != null);
        ViewImageCommand = new RelayCommand(ViewImage, () => SelectedOrder != null && SelectedOrder.HasPhoto);

        _ = LoadOrdersAsync();
    }

    public ObservableCollection<CustomerOrder> Orders { get; } = new();
    public ObservableCollection<CustomerOrder> FilteredOrders { get; } = new();

    public ObservableCollection<string> StatusFilterOptions { get; } = new()
    {
        "All", "Pending", "In Progress", "Completed", "Delivered", "Cancelled"
    };

    public CustomerOrder? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (SetProperty(ref _selectedOrder, value))
            {
                ModifyCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                PrintReceiptCommand.RaiseCanExecuteChanged();
                ViewImageCommand.RaiseCanExecuteChanged();
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

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                ApplyFilter();
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
                ApplyFilter();
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
                ApplyFilter();
            }
        }
    }

    public bool ShowOnlyOpen
    {
        get => _showOnlyOpen;
        set
        {
            if (SetProperty(ref _showOnlyOpen, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
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

    // KPI Properties
    public int TotalOrdersCount
    {
        get => _totalOrdersCount;
        private set => SetProperty(ref _totalOrdersCount, value);
    }

    public int PendingOrdersCount
    {
        get => _pendingOrdersCount;
        private set => SetProperty(ref _pendingOrdersCount, value);
    }

    public int CompletedOrdersCount
    {
        get => _completedOrdersCount;
        private set => SetProperty(ref _completedOrdersCount, value);
    }

    public decimal TotalApproxValue
    {
        get => _totalApproxValue;
        private set => SetProperty(ref _totalApproxValue, value);
    }

    public decimal TotalAdvanceCollected
    {
        get => _totalAdvanceCollected;
        private set => SetProperty(ref _totalAdvanceCollected, value);
    }

    public decimal TotalBalanceDue
    {
        get => _totalBalanceDue;
        private set => SetProperty(ref _totalBalanceDue, value);
    }

    public RelayCommand AddNewCommand { get; }
    public RelayCommand ModifyCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ExportToExcelCommand { get; }
    public RelayCommand PrintReceiptCommand { get; }
    public RelayCommand ViewImageCommand { get; }

    public async Task LoadOrdersAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            string? savePath = null;
            if (_settingsRepository != null)
            {
                savePath = await _settingsRepository.GetValueAsync(AppSetting.SavePathKey);
            }

            var list = await _orderRepository.GetAllAsync(savePath);

            Orders.Clear();
            foreach (var order in list)
            {
                Orders.Add(order);
            }

            ApplyFilter();
            ShowToast($"Loaded {Orders.Count} orders.", ToastType.Info);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load orders: " + ex.Message;
            ShowToast("Error loading orders: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredOrders.Clear();
        var query = SearchText?.Trim() ?? string.Empty;

        var matching = Orders.AsEnumerable();

        // 1. Status Filter
        if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && !string.Equals(SelectedStatusFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            matching = matching.Where(o => string.Equals(o.Status, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Open / Closed Filter
        if (ShowOnlyOpen)
        {
            matching = matching.Where(o => !o.Closed);
        }

        // 3. Date Range Filter
        if (FromDate.HasValue)
        {
            matching = matching.Where(o => o.OrderDate.Date >= FromDate.Value.Date);
        }

        if (ToDate.HasValue)
        {
            matching = matching.Where(o => o.OrderDate.Date <= ToDate.Value.Date);
        }

        // 4. Free Text Search
        if (!string.IsNullOrWhiteSpace(query))
        {
            matching = matching.Where(o =>
                (o.OrderNo?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (o.CustomerName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (o.Phone?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (o.ItemName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (o.City?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (o.KarigarName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (o.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (o.Purity?.Contains(query, StringComparison.OrdinalIgnoreCase) == true));
        }

        foreach (var item in matching)
        {
            FilteredOrders.Add(item);
        }

        CalculateKpis();
    }

    private void CalculateKpis()
    {
        TotalOrdersCount = FilteredOrders.Count;
        PendingOrdersCount = FilteredOrders.Count(o => string.Equals(o.Status, "Pending", StringComparison.OrdinalIgnoreCase) || string.Equals(o.Status, "In Progress", StringComparison.OrdinalIgnoreCase));
        CompletedOrdersCount = FilteredOrders.Count(o => string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase) || string.Equals(o.Status, "Delivered", StringComparison.OrdinalIgnoreCase));
        TotalApproxValue = FilteredOrders.Sum(o => o.AppValue);
        TotalAdvanceCollected = FilteredOrders.Sum(o => o.AdvancePaid);
        TotalBalanceDue = FilteredOrders.Sum(o => o.BalanceDue);
    }

    private void AddNew()
    {
        var vm = new OrderEditViewModel(
            _orderRepository,
            _customerRepository,
            _itemRepository,
            _purityRepository,
            _settingsRepository,
            _currentUser,
            null,
            _auditRepository);

        var win = new OrderEditWindow(vm);
        win.SetOwnerSafely();

        if (win.ShowDialog() == true)
        {
            ShowToast("Order created successfully!", ToastType.Success);
            _ = LoadOrdersAsync();
        }
    }

    private void Modify()
    {
        if (SelectedOrder == null) return;

        var vm = new OrderEditViewModel(
            _orderRepository,
            _customerRepository,
            _itemRepository,
            _purityRepository,
            _settingsRepository,
            _currentUser,
            SelectedOrder,
            _auditRepository);

        var win = new OrderEditWindow(vm);
        win.SetOwnerSafely();

        if (win.ShowDialog() == true)
        {
            ShowToast($"Order #{SelectedOrder.OrderNo} updated successfully!", ToastType.Success);
            _ = LoadOrdersAsync();
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedOrder == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete Order #{SelectedOrder.OrderNo} for {SelectedOrder.CustomerName}?",
            "Confirm Delete Order",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var success = await _orderRepository.DeleteAsync(SelectedOrder.OrderNo, _currentUser);
            if (success)
            {
                if (_auditRepository != null)
                {
                    _ = _auditRepository.LogAuditAsync(new AuditLogEntry
                    {
                        Date = DateTime.Today,
                        Time = DateTime.Now,
                        User = _currentUser,
                        EntryType = "Customer Order",
                        VouNo = SelectedOrder.OrderNo,
                        FieldName = "Delete",
                        OldValue = $"Order for {SelectedOrder.CustomerName} - {SelectedOrder.ItemName}",
                        NewValue = "#Deleted#"
                    });
                }

                ShowToast($"Order #{SelectedOrder.OrderNo} deleted.", ToastType.Success);
                await LoadOrdersAsync();
            }
            else
            {
                ShowToast("Failed to delete order.", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Error deleting order: " + ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ViewImage()
    {
        if (SelectedOrder == null || !SelectedOrder.HasPhoto)
        {
            ShowToast("No image attached to this order.", ToastType.Warning);
            return;
        }

        try
        {
            var win = new Window
            {
                Title = $"Order #{SelectedOrder.OrderNo} - Design Preview",
                Width = 700,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = System.Windows.Media.Brushes.Black
            };
            win.SetOwnerSafely();

            var img = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri(SelectedOrder.ResolvedPhotoPath!)),
                Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(16)
            };

            win.Content = img;
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowToast("Could not display image: " + ex.Message, ToastType.Error);
        }
    }

    private void PrintReceipt()
    {
        if (SelectedOrder == null) return;

        try
        {
            var o = SelectedOrder;
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("           CUSTOMER ORDER / REQUIREMENT           ");
            sb.AppendLine("==================================================");
            sb.AppendLine($"Order No      : {o.OrderNo}");
            sb.AppendLine($"Order Date    : {o.OrderDate:dd/MM/yyyy}");
            sb.AppendLine($"Delivery Date : {(o.DeliveryDate.HasValue ? o.DeliveryDate.Value.ToString("dd/MM/yyyy") : "N/A")}");
            sb.AppendLine($"Customer Name : {o.CustomerName}");
            sb.AppendLine($"Phone         : {o.Phone}");
            sb.AppendLine($"City          : {o.City}");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine($"Item Name     : {o.ItemName}");
            sb.AppendLine($"Purity        : {o.Purity}");
            sb.AppendLine($"Quantity      : {o.Qty}");
            sb.AppendLine($"Size          : {o.Size}");
            sb.AppendLine($"Approx Wt     : {o.AppWeight:N3} g");
            sb.AppendLine($"Metal Rate    : ₹{o.MetalRate:N2} / g");
            sb.AppendLine($"Making Chgs   : ₹{o.Making:N2}");
            if (o.StoneValue > 0)
            {
                sb.AppendLine($"Stone Detail  : {o.StoneName} ({o.StoneWt:N3}g) - ₹{o.StoneValue:N2}");
            }
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine($"Approx Value  : ₹{o.AppValue:N2}");
            sb.AppendLine($"Advance Paid  : ₹{o.AdvancePaid:N2}");
            sb.AppendLine($"Balance Due   : ₹{o.BalanceDue:N2}");
            sb.AppendLine("--------------------------------------------------");
            if (!string.IsNullOrWhiteSpace(o.Description))
            {
                sb.AppendLine($"Notes/Desc    : {o.Description}");
            }
            sb.AppendLine($"Status        : {o.Status}");
            sb.AppendLine($"Karigar       : {o.KarigarName}");
            sb.AppendLine("==================================================");

            Clipboard.SetText(sb.ToString());
            ShowToast("Order receipt copied to clipboard!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast("Error generating receipt: " + ex.Message, ToastType.Error);
        }
    }

    private void ExportToExcel()
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"Customer_Orders_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("OrderNo,OrderDate,DeliveryDate,CustomerName,Phone,City,ItemName,Purity,Qty,Size,AppWeight,MetalRate,Making,StoneValue,AppValue,AdvancePaid,BalanceDue,Status,Closed,Karigar,Description");

            foreach (var o in FilteredOrders)
            {
                sb.AppendLine($"\"{o.OrderNo}\",\"{o.OrderDate:yyyy-MM-dd}\",\"{(o.DeliveryDate.HasValue ? o.DeliveryDate.Value.ToString("yyyy-MM-dd") : "")}\",\"{o.CustomerName}\",\"{o.Phone}\",\"{o.City}\",\"{o.ItemName}\",\"{o.Purity}\",{o.Qty},\"{o.Size}\",{o.AppWeight},{o.MetalRate},{o.Making},{o.StoneValue},{o.AppValue},{o.AdvancePaid},{o.BalanceDue},\"{o.Status}\",{o.Closed},\"{o.KarigarName}\",\"{o.Description?.Replace("\"", "\"\"")}\"");
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            ShowToast("Orders exported successfully to CSV!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast("Export failed: " + ex.Message, ToastType.Error);
        }
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
    }
}
