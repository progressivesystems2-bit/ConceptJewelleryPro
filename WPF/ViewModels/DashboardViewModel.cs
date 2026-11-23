using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using ConceptJewelleryPro.Application.Services;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.Infrastructure.Settings;
using ConceptJewelleryPro.Shared;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly string _currentUser;
    private readonly string _userRole;
    private readonly IDatabaseBackupService? _backupService;
    private readonly DatabaseSettingsStore? _settingsStore;
    private readonly Func<string>? _databasePathProvider;
    private readonly IOrderRepository? _orderRepository;
    private readonly ICustomerRepository? _customerRepository;
    private readonly IItemRepository? _itemRepository;
    private readonly IPurityRepository? _purityRepository;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IAuditRepository? _auditRepository;

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _autoRefreshTimer;

    private bool _isLoading;
    private string _currentDateTimeString = DateTime.Now.ToString("dddd, dd MMMM yyyy  |  hh:mm:ss tt");
    private string _lastUpdatedString = "Not synced yet";
    private string _statusNotification = string.Empty;
    private bool _hasStatusNotification;

    private string _selectedOrderFilter = "All";
    private string _selectedCelebrationFilter = "Today Only";
    private string _selectedSchemeFilter = "All";
    private string _selectedAutoRefreshInterval = "5 min";

    private TodayGlanceMetrics _glanceMetrics = new();
    private SystemStatusInfo _systemStatus = new();

    public event EventHandler<string>? RequestNavigateModule;
    public event EventHandler<string>? RequestOpenOrder;
    public event EventHandler<string>? RequestOpenSchemeReceipt;
    public event EventHandler? RequestOpenRateUpdate;

    public DashboardViewModel(
        IDashboardRepository dashboardRepository,
        string currentUser = "admin",
        string userRole = "Administrator",
        IDatabaseBackupService? backupService = null,
        DatabaseSettingsStore? settingsStore = null,
        Func<string>? databasePathProvider = null,
        IOrderRepository? orderRepository = null,
        ICustomerRepository? customerRepository = null,
        IItemRepository? itemRepository = null,
        IPurityRepository? purityRepository = null,
        ISettingsRepository? settingsRepository = null,
        IAuditRepository? auditRepository = null)
    {
        _dashboardRepository = dashboardRepository;
        _currentUser = currentUser;
        _userRole = userRole;
        _backupService = backupService;
        _settingsStore = settingsStore;
        _databasePathProvider = databasePathProvider;
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _purityRepository = purityRepository;
        _settingsRepository = settingsRepository;
        _auditRepository = auditRepository;

        AllPendingOrders = new ObservableCollection<PendingCustomerOrderItem>();
        FilteredPendingOrders = new ObservableCollection<PendingCustomerOrderItem>();

        AllCelebrations = new ObservableCollection<CustomerCelebrationItem>();
        FilteredCelebrations = new ObservableCollection<CustomerCelebrationItem>();

        AllSchemeDues = new ObservableCollection<SchemeDueSummaryItem>();
        FilteredSchemeDues = new ObservableCollection<SchemeDueSummaryItem>();

        MetalRates = new ObservableCollection<DashboardMetalRateItem>();

        RefreshCommand = new RelayCommand(async () => await LoadDashboardDataAsync());
        NavigateModuleCommand = new RelayCommand(param => NavigateToModule(param?.ToString()));
        OpenOrderCommand = new RelayCommand(param => OpenOrder(param as PendingCustomerOrderItem));
        OpenSchemeReceiptCommand = new RelayCommand(param => OpenSchemeReceipt(param as SchemeDueSummaryItem));
        OpenRateUpdateCommand = new RelayCommand(() => RequestOpenRateUpdate?.Invoke(this, EventArgs.Empty));
        SendWhatsAppGreetingCommand = new RelayCommand(param => SendWhatsAppGreeting(param as CustomerCelebrationItem));
        BackupNowCommand = new RelayCommand(async () => await ExecuteBackupNowAsync(), () => !IsLoading);

        // 1-second clock timer
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) =>
        {
            CurrentDateTimeString = DateTime.Now.ToString("dddd, dd MMMM yyyy  |  hh:mm:ss tt");
        };
        _clockTimer.Start();

        // Auto-refresh timer (default 5 min)
        _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _autoRefreshTimer.Tick += async (_, _) =>
        {
            await LoadDashboardDataAsync();
        };
        _autoRefreshTimer.Start();

        _ = LoadDashboardDataAsync();
    }

    public string Title => "Showroom Overview Dashboard";

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string CurrentDateTimeString
    {
        get => _currentDateTimeString;
        set => SetProperty(ref _currentDateTimeString, value);
    }

    public string LastUpdatedString
    {
        get => _lastUpdatedString;
        set => SetProperty(ref _lastUpdatedString, value);
    }

    public string StatusNotification
    {
        get => _statusNotification;
        set
        {
            if (SetProperty(ref _statusNotification, value))
            {
                HasStatusNotification = !string.IsNullOrWhiteSpace(value);
            }
        }
    }

    public bool HasStatusNotification
    {
        get => _hasStatusNotification;
        set => SetProperty(ref _hasStatusNotification, value);
    }

    public string SelectedAutoRefreshInterval
    {
        get => _selectedAutoRefreshInterval;
        set
        {
            if (SetProperty(ref _selectedAutoRefreshInterval, value))
            {
                ApplyAutoRefreshInterval(value);
            }
        }
    }

    public ObservableCollection<string> AutoRefreshOptions { get; } = new()
    {
        "Off",
        "1 min",
        "5 min",
        "15 min"
    };

    public TodayGlanceMetrics GlanceMetrics
    {
        get => _glanceMetrics;
        set => SetProperty(ref _glanceMetrics, value);
    }

    public SystemStatusInfo SystemStatus
    {
        get => _systemStatus;
        set => SetProperty(ref _systemStatus, value);
    }

    public ObservableCollection<PendingCustomerOrderItem> AllPendingOrders { get; }
    public ObservableCollection<PendingCustomerOrderItem> FilteredPendingOrders { get; }

    public ObservableCollection<CustomerCelebrationItem> AllCelebrations { get; }
    public ObservableCollection<CustomerCelebrationItem> FilteredCelebrations { get; }

    public ObservableCollection<SchemeDueSummaryItem> AllSchemeDues { get; }
    public ObservableCollection<SchemeDueSummaryItem> FilteredSchemeDues { get; }

    public ObservableCollection<DashboardMetalRateItem> MetalRates { get; }

    public string SelectedOrderFilter
    {
        get => _selectedOrderFilter;
        set
        {
            if (SetProperty(ref _selectedOrderFilter, value))
            {
                ApplyOrderFilter();
            }
        }
    }

    public string SelectedCelebrationFilter
    {
        get => _selectedCelebrationFilter;
        set
        {
            if (SetProperty(ref _selectedCelebrationFilter, value))
            {
                ApplyCelebrationFilter();
            }
        }
    }

    public string SelectedSchemeFilter
    {
        get => _selectedSchemeFilter;
        set
        {
            if (SetProperty(ref _selectedSchemeFilter, value))
            {
                ApplySchemeFilter();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand NavigateModuleCommand { get; }
    public ICommand OpenOrderCommand { get; }
    public ICommand OpenSchemeReceiptCommand { get; }
    public ICommand OpenRateUpdateCommand { get; }
    public ICommand SendWhatsAppGreetingCommand { get; }
    public ICommand BackupNowCommand { get; }

    public async Task LoadDashboardDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var summary = await _dashboardRepository.GetDashboardSummaryAsync(
                DateTime.Today,
                _currentUser,
                _userRole);

            GlanceMetrics = summary.GlanceMetrics;
            SystemStatus = summary.SystemStatus;

            // Update Orders
            AllPendingOrders.Clear();
            foreach (var order in summary.PendingOrders)
            {
                AllPendingOrders.Add(order);
            }
            ApplyOrderFilter();

            // Update Celebrations
            AllCelebrations.Clear();
            foreach (var cel in summary.Celebrations)
            {
                AllCelebrations.Add(cel);
            }
            ApplyCelebrationFilter();

            // Update Scheme Dues
            AllSchemeDues.Clear();
            foreach (var sch in summary.SchemeDues)
            {
                AllSchemeDues.Add(sch);
            }
            ApplySchemeFilter();

            // Update Metal Rates
            MetalRates.Clear();
            foreach (var rate in summary.MetalRates)
            {
                MetalRates.Add(rate);
            }

            LastUpdatedString = $"Updated {DateTime.Now:hh:mm:ss tt}";
        }
        catch (Exception ex)
        {
            StatusNotification = $"Error loading dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyOrderFilter()
    {
        FilteredPendingOrders.Clear();
        var items = AllPendingOrders.AsEnumerable();

        if (_selectedOrderFilter == "Due Today")
        {
            items = items.Where(o => o.IsDueToday);
        }
        else if (_selectedOrderFilter == "Overdue")
        {
            items = items.Where(o => o.IsOverdue);
        }

        foreach (var item in items)
        {
            FilteredPendingOrders.Add(item);
        }
    }

    private void ApplyCelebrationFilter()
    {
        FilteredCelebrations.Clear();
        var items = AllCelebrations.AsEnumerable();

        if (_selectedCelebrationFilter == "Today Only")
        {
            items = items.Where(c => c.IsToday);
        }
        else if (_selectedCelebrationFilter == "Birthdays")
        {
            items = items.Where(c => c.EventType == "Birthday");
        }
        else if (_selectedCelebrationFilter == "Anniversaries")
        {
            items = items.Where(c => c.EventType == "Anniversary");
        }

        foreach (var item in items)
        {
            FilteredCelebrations.Add(item);
        }
    }

    private void ApplySchemeFilter()
    {
        FilteredSchemeDues.Clear();
        var items = AllSchemeDues.AsEnumerable();

        if (_selectedSchemeFilter == "Overdue")
        {
            items = items.Where(s => s.DaysOffset < 0);
        }
        else if (_selectedSchemeFilter == "Due Today")
        {
            items = items.Where(s => s.DaysOffset == 0);
        }
        else if (_selectedSchemeFilter == "Next 7 Days")
        {
            items = items.Where(s => s.DaysOffset > 0);
        }

        foreach (var item in items)
        {
            FilteredSchemeDues.Add(item);
        }
    }

    private void ApplyAutoRefreshInterval(string interval)
    {
        _autoRefreshTimer.Stop();
        switch (interval)
        {
            case "1 min":
                _autoRefreshTimer.Interval = TimeSpan.FromMinutes(1);
                _autoRefreshTimer.Start();
                break;
            case "5 min":
                _autoRefreshTimer.Interval = TimeSpan.FromMinutes(5);
                _autoRefreshTimer.Start();
                break;
            case "15 min":
                _autoRefreshTimer.Interval = TimeSpan.FromMinutes(15);
                _autoRefreshTimer.Start();
                break;
            default: // Off
                break;
        }
    }

    private void NavigateToModule(string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName)) return;

        if (moduleName is "Daily Rates" or "Rate Update" or "Rate Master")
        {
            RequestOpenRateUpdate?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (moduleName is "Order" or "Orders" or "Customer Orders" or "New Order" or "Order Requirement" or "OrderRequirement" or "Customer Requirement" or "New Order Requirement" or "Order/Customer Requirements")
        {
            OpenOrder(null);
            return;
        }

        RequestNavigateModule?.Invoke(this, moduleName);
    }

    public async void OpenOrder(PendingCustomerOrderItem? order)
    {
        if (_orderRepository != null)
        {
            try
            {
                CustomerOrder? fullOrder = null;
                if (order != null && !string.IsNullOrWhiteSpace(order.OrderNo))
                {
                    fullOrder = await _orderRepository.GetByOrderNoAsync(order.OrderNo);
                }

                var vm = new OrderEditViewModel(
                    _orderRepository,
                    _customerRepository,
                    _itemRepository,
                    _purityRepository,
                    _settingsRepository,
                    _currentUser,
                    fullOrder,
                    _auditRepository);

                var win = new Views.OrderEditWindow(vm);
                win.SetOwnerSafely();

                if (win.ShowDialog() == true)
                {
                    StatusNotification = fullOrder != null
                        ? $"Order #{fullOrder.OrderNo} updated successfully."
                        : "New Order created successfully.";
                    await LoadDashboardDataAsync();
                }
                return;
            }
            catch (Exception ex)
            {
                StatusNotification = $"Could not open Order window: {ex.Message}";
            }
        }

        if (order != null && !string.IsNullOrWhiteSpace(order.OrderNo))
        {
            RequestOpenOrder?.Invoke(this, order.OrderNo);
        }
    }

    private void OpenSchemeReceipt(SchemeDueSummaryItem? scheme)
    {
        if (scheme == null || string.IsNullOrWhiteSpace(scheme.Scode)) return;
        RequestOpenSchemeReceipt?.Invoke(this, scheme.Scode);
    }

    private async void SendWhatsAppGreeting(CustomerCelebrationItem? celebration)
    {
        if (celebration == null) return;

        var phone = celebration.BestContactNumber.Trim();
        var cleanPhone = new string(phone.Where(char.IsDigit).ToArray());

        if (cleanPhone.Length < 10)
        {
            StatusNotification = $"Invalid phone number for {celebration.CustomerName}";
            return;
        }

        if (cleanPhone.Length == 10)
        {
            cleanPhone = "91" + cleanPhone; // India country code default
        }

        string rawMessage = celebration.GreetingMessage;
        string wishesMode = "WhatsappCloud";

        if (_settingsRepository != null)
        {
            try
            {
                var settings = await _settingsRepository.GetAllAsync();

                if (settings.TryGetValue(AppSetting.WishesModeKey, out var wMode) && !string.IsNullOrWhiteSpace(wMode))
                {
                    wishesMode = wMode.Trim();
                }

                string? customTemplate = null;
                if (celebration.EventType == "Birthday")
                {
                    if (settings.TryGetValue(AppSetting.BirthdayMessageKey, out var bMsg) && !string.IsNullOrWhiteSpace(bMsg))
                    {
                        customTemplate = bMsg;
                    }
                }
                else
                {
                    if (settings.TryGetValue(AppSetting.AnniversaryMessageKey, out var aMsg) && !string.IsNullOrWhiteSpace(aMsg))
                    {
                        customTemplate = aMsg;
                    }
                    else if (settings.TryGetValue("AnniversarryMessage", out var aMsgAlt) && !string.IsNullOrWhiteSpace(aMsgAlt))
                    {
                        customTemplate = aMsgAlt;
                    }
                }

                if (!string.IsNullOrWhiteSpace(customTemplate))
                {
                    rawMessage = customTemplate
                        .Replace("{CustomerName}", celebration.CustomerName ?? string.Empty)
                        .Replace("{StoreName}", "Concept Jewellery");
                }
            }
            catch
            {
                // Fallback to default celebration greeting message
            }
        }

        var encodedMsg = WebUtility.UrlEncode(rawMessage);
        string url;

        if (wishesMode.Equals("WhatsAppWeb", StringComparison.OrdinalIgnoreCase))
        {
            url = $"https://web.whatsapp.com/send?phone={cleanPhone}&text={encodedMsg}";
        }
        else if (wishesMode.Equals("WhatsappDesktop", StringComparison.OrdinalIgnoreCase))
        {
            url = $"whatsapp://send?phone={cleanPhone}&text={encodedMsg}";
        }
        else
        {
            // Default: WhatsappCloud / wa.me link
            url = $"https://wa.me/{cleanPhone}?text={encodedMsg}";
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            StatusNotification = $"Opening WhatsApp greeting ({wishesMode}) for {celebration.CustomerName}...";
        }
        catch (Exception ex)
        {
            StatusNotification = $"Could not launch WhatsApp: {ex.Message}";
        }
    }

    private async Task ExecuteBackupNowAsync()
    {
        if (_backupService == null || _databasePathProvider == null)
        {
            StatusNotification = "Backup service is not initialized.";
            return;
        }

        var dbPath = _databasePathProvider();
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
        {
            StatusNotification = "Primary database file not found.";
            return;
        }

        var targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        IsLoading = true;
        StatusNotification = "Creating full database backup...";

        try
        {
            var result = await _backupService.BackupAccessDatabaseAsync(dbPath, targetFolder, compressZip: true);
            if (result.Success)
            {
                StatusNotification = $"Backup created successfully: {Path.GetFileName(result.BackupFilePath)}";
                await LoadDashboardDataAsync();
            }
            else
            {
                StatusNotification = $"Backup failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusNotification = $"Backup error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
