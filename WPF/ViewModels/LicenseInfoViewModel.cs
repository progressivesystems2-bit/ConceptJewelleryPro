using System.Windows.Input;
using ConceptJewelleryPro.Application.Licensing;
using ConceptJewelleryPro.WPF.Controls;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class LicenseInfoViewModel : ViewModelBase
{
    private readonly ILicenseValidationService _licenseService;

    private string _companyName = "Loading...";
    private string _licenseStatus = "Checking...";
    private string _licenseType = "Subscription";
    private string _editionText = "Single HO (Standalone)";
    private string _maxBranchesText = "1 Store (Head Office)";
    private string _maxNodesText = "1 Computer (Node)";
    private string _registeredMachinesText = "—";
    private string _enabledModulesText = "All Modules Unlocked";
    private string _currentMachineText = DeviceFingerprintHelper.GetMachineName();
    private string _validFromText = "—";
    private string _validToText = "—";
    private string _daysRemainingText = "—";
    private string _lastVerifiedOnlineText = "—";
    private bool _isBusy;
    private string _busyMessage = "Verifying license with cloud server...";
    private string _statusMessage = string.Empty;
    private string? _toastMessage;
    private ToastType _toastType = ToastType.Info;

    public LicenseInfoViewModel(ILicenseValidationService licenseService)
    {
        _licenseService = licenseService;
        RefreshLicenseCommand = new RelayCommand(async () => await LoadLicenseInfoAsync(forceOnline: true), () => !IsBusy);

        if (_licenseService.CurrentStatus is not null)
        {
            ApplyResult(_licenseService.CurrentStatus);
        }
        else
        {
            _ = LoadLicenseInfoAsync(forceOnline: false);
        }
    }

    public ICommand RefreshLicenseCommand { get; }

    public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value); }
    public string LicenseStatus { get => _licenseStatus; set => SetProperty(ref _licenseStatus, value); }
    public string LicenseType { get => _licenseType; set => SetProperty(ref _licenseType, value); }
    public string EditionText { get => _editionText; set => SetProperty(ref _editionText, value); }
    public string MaxBranchesText { get => _maxBranchesText; set => SetProperty(ref _maxBranchesText, value); }
    public string MaxNodesText { get => _maxNodesText; set => SetProperty(ref _maxNodesText, value); }
    public string RegisteredMachinesText { get => _registeredMachinesText; set => SetProperty(ref _registeredMachinesText, value); }
    public string EnabledModulesText { get => _enabledModulesText; set => SetProperty(ref _enabledModulesText, value); }
    public string CurrentMachineText { get => _currentMachineText; set => SetProperty(ref _currentMachineText, value); }
    public string ValidFromText { get => _validFromText; set => SetProperty(ref _validFromText, value); }
    public string ValidToText { get => _validToText; set => SetProperty(ref _validToText, value); }
    public string DaysRemainingText { get => _daysRemainingText; set => SetProperty(ref _daysRemainingText, value); }
    public string LastVerifiedOnlineText { get => _lastVerifiedOnlineText; set => SetProperty(ref _lastVerifiedOnlineText, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string BusyMessage { get => _busyMessage; set => SetProperty(ref _busyMessage, value); }
    public string? ToastMessage { get => _toastMessage; set => SetProperty(ref _toastMessage, value); }
    public ToastType ToastType { get => _toastType; set => SetProperty(ref _toastType, value); }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                (RefreshLicenseCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task LoadLicenseInfoAsync(bool forceOnline = false)
    {
        if (IsBusy) return;
        IsBusy = true;
        BusyMessage = "Syncing license with Concept License Manager...";

        try
        {
            var result = await _licenseService.ValidateLicenseAsync(forceOnline);
            ApplyResult(result);
            ShowToast(result.IsValid ? "License verified successfully." : result.StatusMessage,
                result.IsValid ? ToastType.Success : ToastType.Warning);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to check license: {ex.Message}";
            ShowToast(StatusMessage, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyResult(LicenseValidationResult result)
    {
        CompanyName = string.IsNullOrWhiteSpace(result.CompanyName) ? "Unknown" : result.CompanyName;
        LicenseType = result.LicenseType ?? "Subscription";
        EditionText = result.IsMultiBranchAllowed
            ? "Multi-Branch Edition (HQ & Remote Stores)"
            : "Single HO Edition (Head Office / Standalone)";
        MaxBranchesText = result.IsMultiBranchAllowed
            ? (result.MaxBranches >= 90 ? "Unlimited Stores" : $"{result.MaxBranches} Store(s)")
            : "1 Store (Head Office Only)";
        MaxNodesText = result.MaxNodes >= 99
            ? $"Unlimited Computers ({result.RegisteredNodesCount} Active)"
            : $"{result.RegisteredNodesCount} of {result.MaxNodes} Computer(s) Used";
        RegisteredMachinesText = string.IsNullOrWhiteSpace(result.RegisteredMachines)
            ? DeviceFingerprintHelper.GetMachineName()
            : result.RegisteredMachines;
        EnabledModulesText = result.EnabledModulesDisplay;
        CurrentMachineText = string.IsNullOrWhiteSpace(result.CurrentNodeMachineName)
            ? DeviceFingerprintHelper.GetMachineName()
            : $"{result.CurrentNodeMachineName} (This Computer)";
        ValidFromText = result.ValidFrom.HasValue ? result.ValidFrom.Value.ToString("dd-MMM-yyyy") : "—";
        ValidToText = result.ValidTo.HasValue ? result.ValidTo.Value.ToString("dd-MMM-yyyy") : "Perpetual (Lifetime)";
        DaysRemainingText = result.DaysRemaining.HasValue ? $"{result.DaysRemaining.Value} Day(s)" : "Unlimited";
        LastVerifiedOnlineText = result.LastVerifiedOnline.HasValue
            ? result.LastVerifiedOnline.Value.ToLocalTime().ToString("dd-MMM-yyyy hh:mm:ss tt")
            : "Not Available";

        LicenseStatus = result.Status switch
        {
            LicenseValidationStatus.Valid => "Active & Verified",
            LicenseValidationStatus.OfflineGraceValid => "Offline Grace Mode",
            LicenseValidationStatus.Expired => "Subscription Expired",
            LicenseValidationStatus.Deactivated => "Suspended",
            LicenseValidationStatus.NotRegistered => "Unregistered Company",
            LicenseValidationStatus.NodeLimitExceeded => "Node Limit Exceeded",
            LicenseValidationStatus.NodeRevoked => "Computer Revoked",
            _ => "Attention Required"
        };

        StatusMessage = result.StatusMessage;
    }

    private void ShowToast(string message, ToastType type)
    {
        ToastMessage = null;
        ToastType = type;
        ToastMessage = message;
    }
}
