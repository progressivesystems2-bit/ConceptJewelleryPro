using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Infrastructure.Services;
using ConceptJewelleryPro.WPF.Controls;
using ConceptJewelleryPro.WPF.Helpers;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class CouponEngineViewModel : ViewModelBase, IRefreshable
{
    private readonly ICouponRepository _couponRepository;
    private readonly ICouponEngineService _couponEngineService;
    private readonly string _currentUser;

    private int _selectedTabIndex;
    private bool _isBusy;
    private string _statusMessage = "Ready";
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    // Tab 1: Campaigns
    private CouponCampaign? _selectedCampaign;
    private int _editingCampaignId;
    private string _formCampaignName = string.Empty;
    private string _formDescription = string.Empty;
    private CouponType _formCouponType = CouponType.FlatAmount;
    private decimal _formDiscountValue = 1000m;
    private decimal? _formMinBillAmount = 15000m;
    private decimal? _formMaxDiscountAmount;
    private DateTime _formValidFrom = DateTime.Today;
    private DateTime _formValidTo = DateTime.Today.AddDays(30);
    private bool _formIsActive = true;
    private CouponAutoTriggerType _formAutoTriggerType = CouponAutoTriggerType.None;
    private decimal? _formTriggerThresholdAmount;
    private string _formCodePrefix = "GOLD";
    private string _formWhatsAppTemplate = "Dear {CustomerName}, here is your exclusive coupon {CouponCode} for {DiscountText} at {Store}! Valid till {ValidTo}. T&C apply.";

    // Tab 2: Target & Generate
    private CouponCampaign? _targetCampaign;
    private string _filterCity = string.Empty;
    private string _filterGroup = string.Empty;
    private decimal? _filterMinSpend;
    private int? _filterMinVisits;
    private int? _filterInactiveDays;
    private int? _filterBirthMonth;
    private bool _dispatchWhatsAppOnGenerate = true;

    // Tab 3: Issued Coupons
    private CouponCampaign? _couponFilterCampaign;
    private string _couponFilterStatus = "All";
    private string _couponSearchText = string.Empty;
    private string _singleCouponCode = string.Empty;
    private string _singleCustomerMobile = string.Empty;
    private string _singleCustomerName = string.Empty;
    private CouponType _singleCouponType = CouponType.FlatAmount;
    private decimal _singleDiscountValue = 500m;
    private decimal? _singleMinBillAmount = 5000m;
    private DateTime _singleValidTo = DateTime.Today.AddDays(30);

    // Tab 4: Analytics
    private CouponAnalyticsSummary _analytics = new();

    public CouponEngineViewModel(
        ICouponRepository couponRepository,
        ICouponEngineService couponEngineService,
        string currentUser = "admin")
    {
        _couponRepository = couponRepository;
        _couponEngineService = couponEngineService;
        _currentUser = currentUser;

        CouponTypeOptions = [CouponType.FlatAmount, CouponType.Percentage];
        AutoTriggerOptions = Enum.GetValues<CouponAutoTriggerType>();
        CouponStatusFilterOptions = ["All", "Active", "Redeemed", "Expired", "Revoked"];

        LoadAllDataCommand = new AsyncRelayCommand(LoadAllDataAsync);
        RunAutoPilotTriggersCommand = new AsyncRelayCommand(RunAutoPilotTriggersAsync);
        NewCampaignCommand = new RelayCommand(NewCampaign);
        SaveCampaignCommand = new AsyncRelayCommand(SaveCampaignAsync);
        DeleteCampaignCommand = new AsyncRelayCommand(DeleteCampaignAsync);
        SeedDefaultCampaignsCommand = new AsyncRelayCommand(SeedDefaultCampaignsAsync);

        SearchSegmentationCommand = new AsyncRelayCommand(SearchSegmentationAsync);
        SelectAllCustomersCommand = new RelayCommand(() => SetAllCustomerSelection(true));
        DeselectAllCustomersCommand = new RelayCommand(() => SetAllCustomerSelection(false));
        GenerateBulkCouponsCommand = new AsyncRelayCommand(GenerateBulkCouponsAsync);

        LoadCouponsCommand = new AsyncRelayCommand(LoadCouponsAsync);
        GenerateSingleCouponCommand = new AsyncRelayCommand(GenerateSingleCouponAsync);
        DispatchSelectedCouponsCommand = new AsyncRelayCommand(DispatchSelectedCouponsAsync);
        RevokeSelectedCouponsCommand = new AsyncRelayCommand(RevokeSelectedCouponsAsync);
        SyncDeliveryStatusesCommand = new AsyncRelayCommand(SyncDeliveryStatusesAsync);

        _ = LoadAllDataAsync();
    }

    public string Title => "Coupon Engine";
    public CouponType[] CouponTypeOptions { get; }
    public CouponAutoTriggerType[] AutoTriggerOptions { get; }
    public string[] CouponStatusFilterOptions { get; }

    public ObservableCollection<CouponCampaign> Campaigns { get; } = [];
    public ObservableCollection<CustomerSegmentationResult> SegmentedCustomers { get; } = [];
    public ObservableCollection<Coupon> IssuedCoupons { get; } = [];
    public ObservableCollection<CouponRedemption> RecentRedemptions { get; } = [];

    public ICommand LoadAllDataCommand { get; }
    public ICommand RunAutoPilotTriggersCommand { get; }
    public ICommand NewCampaignCommand { get; }
    public ICommand SaveCampaignCommand { get; }
    public ICommand DeleteCampaignCommand { get; }
    public ICommand SeedDefaultCampaignsCommand { get; }
    public ICommand SearchSegmentationCommand { get; }
    public ICommand SelectAllCustomersCommand { get; }
    public ICommand DeselectAllCustomersCommand { get; }
    public ICommand GenerateBulkCouponsCommand { get; }
    public ICommand LoadCouponsCommand { get; }
    public ICommand GenerateSingleCouponCommand { get; }
    public ICommand DispatchSelectedCouponsCommand { get; }
    public ICommand RevokeSelectedCouponsCommand { get; }
    public ICommand SyncDeliveryStatusesCommand { get; }

    public int SelectedTabIndex { get => _selectedTabIndex; set => SetProperty(ref _selectedTabIndex, value); }
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string? ToastMessage { get => _toastMessage; set => SetProperty(ref _toastMessage, value); }
    public ToastType ToastType { get => _toastType; set => SetProperty(ref _toastType, value); }

    public CouponCampaign? SelectedCampaign
    {
        get => _selectedCampaign;
        set
        {
            if (SetProperty(ref _selectedCampaign, value) && value != null)
            {
                LoadCampaignIntoForm(value);
            }
        }
    }

    public int EditingCampaignId { get => _editingCampaignId; set => SetProperty(ref _editingCampaignId, value); }
    public string FormCampaignName { get => _formCampaignName; set => SetProperty(ref _formCampaignName, value); }
    public string FormDescription { get => _formDescription; set => SetProperty(ref _formDescription, value); }
    public CouponType FormCouponType { get => _formCouponType; set => SetProperty(ref _formCouponType, value); }
    public decimal FormDiscountValue { get => _formDiscountValue; set => SetProperty(ref _formDiscountValue, value); }
    public decimal? FormMinBillAmount { get => _formMinBillAmount; set => SetProperty(ref _formMinBillAmount, value); }
    public decimal? FormMaxDiscountAmount { get => _formMaxDiscountAmount; set => SetProperty(ref _formMaxDiscountAmount, value); }
    public DateTime FormValidFrom { get => _formValidFrom; set => SetProperty(ref _formValidFrom, value); }
    public DateTime FormValidTo { get => _formValidTo; set => SetProperty(ref _formValidTo, value); }
    public bool FormIsActive { get => _formIsActive; set => SetProperty(ref _formIsActive, value); }
    public CouponAutoTriggerType FormAutoTriggerType { get => _formAutoTriggerType; set => SetProperty(ref _formAutoTriggerType, value); }
    public decimal? FormTriggerThresholdAmount { get => _formTriggerThresholdAmount; set => SetProperty(ref _formTriggerThresholdAmount, value); }
    public string FormCodePrefix { get => _formCodePrefix; set => SetProperty(ref _formCodePrefix, value); }
    public string FormWhatsAppTemplate { get => _formWhatsAppTemplate; set => SetProperty(ref _formWhatsAppTemplate, value); }

    public CouponCampaign? TargetCampaign { get => _targetCampaign; set => SetProperty(ref _targetCampaign, value); }
    public string FilterCity { get => _filterCity; set => SetProperty(ref _filterCity, value); }
    public string FilterGroup { get => _filterGroup; set => SetProperty(ref _filterGroup, value); }
    public decimal? FilterMinSpend { get => _filterMinSpend; set => SetProperty(ref _filterMinSpend, value); }
    public int? FilterMinVisits { get => _filterMinVisits; set => SetProperty(ref _filterMinVisits, value); }
    public int? FilterInactiveDays { get => _filterInactiveDays; set => SetProperty(ref _filterInactiveDays, value); }
    public int? FilterBirthMonth { get => _filterBirthMonth; set => SetProperty(ref _filterBirthMonth, value); }
    public bool DispatchWhatsAppOnGenerate { get => _dispatchWhatsAppOnGenerate; set => SetProperty(ref _dispatchWhatsAppOnGenerate, value); }

    public CouponCampaign? CouponFilterCampaign { get => _couponFilterCampaign; set => SetProperty(ref _couponFilterCampaign, value); }
    public string CouponFilterStatus { get => _couponFilterStatus; set => SetProperty(ref _couponFilterStatus, value); }
    public string CouponSearchText { get => _couponSearchText; set => SetProperty(ref _couponSearchText, value); }
    public string SingleCouponCode { get => _singleCouponCode; set => SetProperty(ref _singleCouponCode, value); }
    public string SingleCustomerMobile { get => _singleCustomerMobile; set => SetProperty(ref _singleCustomerMobile, value); }
    public string SingleCustomerName { get => _singleCustomerName; set => SetProperty(ref _singleCustomerName, value); }
    public CouponType SingleCouponType { get => _singleCouponType; set => SetProperty(ref _singleCouponType, value); }
    public decimal SingleDiscountValue { get => _singleDiscountValue; set => SetProperty(ref _singleDiscountValue, value); }
    public decimal? SingleMinBillAmount { get => _singleMinBillAmount; set => SetProperty(ref _singleMinBillAmount, value); }
    public DateTime SingleValidTo { get => _singleValidTo; set => SetProperty(ref _singleValidTo, value); }

    public CouponAnalyticsSummary Analytics { get => _analytics; set => SetProperty(ref _analytics, value); }

    public Task RefreshAsync() => LoadAllDataAsync();

    public async Task LoadAllDataAsync()
    {
        IsBusy = true;
        try
        {
            var camps = await _couponRepository.GetCampaignsAsync();
            Campaigns.Clear();
            foreach (var c in camps) Campaigns.Add(c);
            TargetCampaign ??= Campaigns.FirstOrDefault(c => c.IsActive) ?? Campaigns.FirstOrDefault();

            await LoadCouponsAsync();

            Analytics = await _couponRepository.GetAnalyticsSummaryAsync();
            var redemptions = await _couponRepository.GetRecentRedemptionsAsync(100);
            RecentRedemptions.Clear();
            foreach (var r in redemptions) RecentRedemptions.Add(r);

            StatusMessage = $"Loaded {Campaigns.Count} campaigns, {IssuedCoupons.Count} coupons, {RecentRedemptions.Count} redemptions.";
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to load coupon data: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NewCampaign()
    {
        SelectedCampaign = null;
        EditingCampaignId = 0;
        FormCampaignName = string.Empty;
        FormDescription = string.Empty;
        FormCouponType = CouponType.FlatAmount;
        FormDiscountValue = 1000m;
        FormMinBillAmount = 15000m;
        FormMaxDiscountAmount = null;
        FormValidFrom = DateTime.Today;
        FormValidTo = DateTime.Today.AddDays(30);
        FormIsActive = true;
        FormAutoTriggerType = CouponAutoTriggerType.None;
        FormTriggerThresholdAmount = null;
        FormCodePrefix = "GOLD";
        FormWhatsAppTemplate = "Dear {CustomerName}, here is your exclusive coupon {CouponCode} for {DiscountText} at {Store}! Valid till {ValidTo}. T&C apply.";
    }

    private void LoadCampaignIntoForm(CouponCampaign c)
    {
        EditingCampaignId = c.CampaignId;
        FormCampaignName = c.CampaignName;
        FormDescription = c.Description;
        FormCouponType = c.CouponType;
        FormDiscountValue = c.DiscountValue;
        FormMinBillAmount = c.MinBillAmount;
        FormMaxDiscountAmount = c.MaxDiscountAmount;
        FormValidFrom = c.ValidFrom;
        FormValidTo = c.ValidTo;
        FormIsActive = c.IsActive;
        FormAutoTriggerType = c.AutoTriggerType;
        FormTriggerThresholdAmount = c.TriggerThresholdAmount;
        FormCodePrefix = c.CodePrefix;
        FormWhatsAppTemplate = c.WhatsAppTemplate;
    }

    private async Task SaveCampaignAsync()
    {
        if (string.IsNullOrWhiteSpace(FormCampaignName))
        {
            ShowToast("Please enter a campaign name.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var model = new CouponCampaign
            {
                CampaignId = EditingCampaignId,
                CampaignName = FormCampaignName.Trim(),
                Description = FormDescription?.Trim() ?? string.Empty,
                CouponType = FormCouponType,
                DiscountValue = FormDiscountValue,
                MinBillAmount = FormMinBillAmount,
                MaxDiscountAmount = FormMaxDiscountAmount,
                ValidFrom = FormValidFrom,
                ValidTo = FormValidTo,
                IsActive = FormIsActive,
                AutoTriggerType = FormAutoTriggerType,
                TriggerThresholdAmount = FormTriggerThresholdAmount,
                CodePrefix = string.IsNullOrWhiteSpace(FormCodePrefix) ? "CPN" : FormCodePrefix.Trim().ToUpperInvariant(),
                WhatsAppTemplate = FormWhatsAppTemplate?.Trim() ?? string.Empty,
                CreatedBy = _currentUser
            };

            if (EditingCampaignId > 0)
            {
                await _couponRepository.UpdateCampaignAsync(model);
                ShowToast($"Campaign '{model.CampaignName}' updated.", ToastType.Success);
            }
            else
            {
                await _couponRepository.SaveCampaignAsync(model);
                ShowToast($"Campaign '{model.CampaignName}' created.", ToastType.Success);
            }

            NewCampaign();
            await LoadAllDataAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"Error saving campaign: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteCampaignAsync()
    {
        if (SelectedCampaign == null)
        {
            ShowToast("Select a campaign to delete.", ToastType.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Delete campaign '{SelectedCampaign.CampaignName}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _couponRepository.DeleteCampaignAsync(SelectedCampaign.CampaignId);
            NewCampaign();
            await LoadAllDataAsync();
            ShowToast("Campaign deleted.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Delete failed: {ex.Message}", ToastType.Error);
        }
    }

    private async Task SeedDefaultCampaignsAsync()
    {
        IsBusy = true;
        try
        {
            var starters = new[]
            {
                new CouponCampaign
                {
                    CampaignName = "Festive Gold Offer",
                    Description = "Flat ₹1,000 off on gold jewellery above ₹25,000",
                    CouponType = CouponType.FlatAmount,
                    DiscountValue = 1000m,
                    MinBillAmount = 25000m,
                    ValidFrom = DateTime.Today,
                    ValidTo = DateTime.Today.AddDays(45),
                    IsActive = true,
                    CodePrefix = "FEST",
                    CreatedBy = _currentUser
                },
                new CouponCampaign
                {
                    CampaignName = "Birthday Privilege Voucher",
                    Description = "Auto-sent to customers with upcoming birthdays",
                    CouponType = CouponType.FlatAmount,
                    DiscountValue = 1500m,
                    MinBillAmount = 20000m,
                    ValidFrom = DateTime.Today,
                    ValidTo = DateTime.Today.AddDays(365),
                    IsActive = true,
                    AutoTriggerType = CouponAutoTriggerType.Birthday,
                    CodePrefix = "BDAY",
                    CreatedBy = _currentUser
                },
                new CouponCampaign
                {
                    CampaignName = "Bounce-Back Next Visit Voucher",
                    Description = "Auto-issued on bills above ₹15,000 for next visit",
                    CouponType = CouponType.FlatAmount,
                    DiscountValue = 500m,
                    MinBillAmount = 10000m,
                    TriggerThresholdAmount = 15000m,
                    ValidFrom = DateTime.Today,
                    ValidTo = DateTime.Today.AddDays(180),
                    IsActive = true,
                    AutoTriggerType = CouponAutoTriggerType.BounceBackVoucher,
                    CodePrefix = "NEXT",
                    CreatedBy = _currentUser
                }
            };

            foreach (var s in starters)
            {
                if (!Campaigns.Any(c => string.Equals(c.CampaignName, s.CampaignName, StringComparison.OrdinalIgnoreCase)))
                {
                    await _couponRepository.SaveCampaignAsync(s);
                }
            }

            await LoadAllDataAsync();
            ShowToast("Starter jewellery campaigns loaded!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to seed campaigns: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SearchSegmentationAsync()
    {
        IsBusy = true;
        try
        {
            var filter = new CouponCustomerSegmentFilter
            {
                City = string.IsNullOrWhiteSpace(FilterCity) ? null : FilterCity.Trim(),
                Group = string.IsNullOrWhiteSpace(FilterGroup) ? null : FilterGroup.Trim(),
                MinLifetimeSpend = FilterMinSpend,
                MinVisits = FilterMinVisits,
                InactiveDaysMin = FilterInactiveDays,
                BirthMonth = FilterBirthMonth,
                OnlyWithMobile = true
            };

            var results = await _couponRepository.SearchCustomersForSegmentationAsync(filter);
            SegmentedCustomers.Clear();
            foreach (var r in results) SegmentedCustomers.Add(r);

            StatusMessage = $"Found {SegmentedCustomers.Count} matching customers.";
        }
        catch (Exception ex)
        {
            ShowToast($"Segmentation failed: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetAllCustomerSelection(bool selected)
    {
        foreach (var c in SegmentedCustomers) c.IsSelected = selected;
        var snapshot = SegmentedCustomers.ToList();
        SegmentedCustomers.Clear();
        foreach (var c in snapshot) SegmentedCustomers.Add(c);
    }

    private async Task GenerateBulkCouponsAsync()
    {
        if (TargetCampaign == null)
        {
            ShowToast("Please select a Target Campaign first.", ToastType.Warning);
            return;
        }

        var selected = SegmentedCustomers.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowToast("Select at least one customer from the segmented list.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var generated = await _couponEngineService.GenerateBulkCouponsAsync(
                TargetCampaign,
                selected,
                _currentUser,
                DispatchWhatsAppOnGenerate);

            await LoadAllDataAsync();
            SelectedTabIndex = 2;
            ShowToast($"Generated {generated.Count} personalized coupons{(DispatchWhatsAppOnGenerate ? " and queued WhatsApp dispatch" : "")}!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Bulk generation failed: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCouponsAsync()
    {
        var list = await _couponRepository.GetCouponsAsync(
            CouponFilterCampaign?.CampaignId,
            CouponFilterStatus == "All" ? null : CouponFilterStatus,
            CouponSearchText,
            1,
            500);

        IssuedCoupons.Clear();
        foreach (var c in list) IssuedCoupons.Add(c);
    }

    private async Task GenerateSingleCouponAsync()
    {
        if (SingleDiscountValue <= 0)
        {
            ShowToast("Discount value must be greater than 0.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var coupon = new Coupon
            {
                CampaignId = CouponFilterCampaign?.CampaignId,
                CampaignName = CouponFilterCampaign?.CampaignName ?? "Direct Issue",
                CouponCode = SingleCouponCode?.Trim() ?? string.Empty,
                CustomerMobile = string.IsNullOrWhiteSpace(SingleCustomerMobile) ? null : SingleCustomerMobile.Trim(),
                CustomerName = string.IsNullOrWhiteSpace(SingleCustomerName) ? null : SingleCustomerName.Trim(),
                CouponType = SingleCouponType,
                DiscountValue = SingleDiscountValue,
                MinBillAmount = SingleMinBillAmount,
                ValidFrom = DateTime.Today,
                ValidTo = SingleValidTo,
                MaxUsageCount = 1,
                Status = CouponStatus.Active,
                Channel = "Direct",
                CreatedBy = _currentUser
            };

            var created = await _couponEngineService.GenerateSingleCouponAsync(coupon);
            SingleCouponCode = string.Empty;
            SingleCustomerMobile = string.Empty;
            SingleCustomerName = string.Empty;
            await LoadCouponsAsync();
            ShowToast($"Coupon '{created.CouponCode}' created!", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Error creating coupon: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DispatchSelectedCouponsAsync()
    {
        var ids = IssuedCoupons.Where(c => c.IsSelected).Select(c => c.CouponId).ToList();
        if (ids.Count == 0)
        {
            ShowToast("Select one or more coupons to dispatch via WhatsApp.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var (sent, failed) = await _couponEngineService.DispatchCouponsWhatsAppAsync(ids);
            await LoadCouponsAsync();
            ShowToast($"WhatsApp Dispatch: {sent} sent, {failed} failed.", failed == 0 ? ToastType.Success : ToastType.Warning);
        }
        catch (Exception ex)
        {
            ShowToast($"Dispatch error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RevokeSelectedCouponsAsync()
    {
        var selected = IssuedCoupons.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowToast("Select coupons to revoke.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            foreach (var c in selected)
            {
                await _couponRepository.UpdateCouponStatusAsync(c.CouponId, CouponStatus.Revoked);
            }
            await LoadCouponsAsync();
            ShowToast($"Revoked {selected.Count} coupon(s).", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Revoke error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncDeliveryStatusesAsync()
    {
        IsBusy = true;
        try
        {
            var (updated, checkedCount) = await _couponEngineService.SyncCouponStatusesAsync();
            await LoadCouponsAsync();
            ShowToast($"Checked {checkedCount} messages, updated {updated} delivery statuses.", ToastType.Info);
        }
        catch (Exception ex)
        {
            ShowToast($"Sync error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunAutoPilotTriggersAsync()
    {
        IsBusy = true;
        try
        {
            var count = await _couponEngineService.ProcessAutoTriggersAsync();
            await LoadAllDataAsync();
            ShowToast($"Auto-Pilot Triggers complete: {count} coupon(s) generated & dispatched.", ToastType.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"Auto-Pilot error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = message;
        ToastType = type;
        StatusMessage = message;
    }
}
