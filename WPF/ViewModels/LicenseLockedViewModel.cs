using System.Windows.Input;
using ConceptJewelleryPro.Application.Licensing;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class LicenseLockedViewModel : ViewModelBase
{
    private readonly ILicenseValidationService _licenseService;

    private string _companyName = string.Empty;
    private string _machineName = DeviceFingerprintHelper.GetMachineName();
    private string _statusTitle = "License Required";
    private string _statusMessage = string.Empty;
    private string _validToText = string.Empty;
    private bool _isChecking;
    private string _checkResultMessage = string.Empty;

    public event Action? VerificationSucceeded;
    public event Action? ExitRequested;

    public LicenseLockedViewModel(
        ILicenseValidationService licenseService,
        LicenseValidationResult initialResult)
    {
        _licenseService = licenseService;
        RetryVerificationCommand = new RelayCommand(async () => await RetryVerificationAsync(), () => !IsChecking);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke());

        ApplyResult(initialResult);
    }

    public ICommand RetryVerificationCommand { get; }
    public ICommand ExitCommand { get; }

    public string CompanyName
    {
        get => _companyName;
        set => SetProperty(ref _companyName, value);
    }

    public string MachineName
    {
        get => _machineName;
        set => SetProperty(ref _machineName, value);
    }

    public string StatusTitle
    {
        get => _statusTitle;
        set => SetProperty(ref _statusTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ValidToText
    {
        get => _validToText;
        set => SetProperty(ref _validToText, value);
    }

    public bool IsChecking
    {
        get => _isChecking;
        set
        {
            if (SetProperty(ref _isChecking, value))
            {
                (RetryVerificationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string CheckResultMessage
    {
        get => _checkResultMessage;
        set => SetProperty(ref _checkResultMessage, value);
    }

    private void ApplyResult(LicenseValidationResult result)
    {
        CompanyName = string.IsNullOrWhiteSpace(result.CompanyName) ? "Unknown Company" : result.CompanyName;
        MachineName = string.IsNullOrWhiteSpace(result.CurrentNodeMachineName)
            ? DeviceFingerprintHelper.GetMachineName()
            : result.CurrentNodeMachineName;
        StatusMessage = result.StatusMessage;

        StatusTitle = result.Status switch
        {
            LicenseValidationStatus.NotRegistered => "Company Not Registered",
            LicenseValidationStatus.Deactivated => "License Deactivated",
            LicenseValidationStatus.Expired => "Subscription Expired",
            LicenseValidationStatus.OfflineGraceExpired => "Offline Grace Expired",
            LicenseValidationStatus.ClockTampered => "Clock Tampering Detected",
            LicenseValidationStatus.NodeLimitExceeded => "Computer Node Limit Reached",
            LicenseValidationStatus.NodeRevoked => "Computer Deactivated",
            LicenseValidationStatus.SignatureInvalid => "Invalid License Signature",
            LicenseValidationStatus.ConnectionFailed => "Cloud Connection Failed",
            _ => "License Validation Required"
        };

        ValidToText = result.Status switch
        {
            LicenseValidationStatus.NodeLimitExceeded => $"Max Nodes Allowed: {result.MaxNodes} (Registered: {result.RegisteredNodesCount})",
            LicenseValidationStatus.NodeRevoked => "This computer has been revoked by the administrator",
            _ => result.ValidTo.HasValue
                ? $"Expired On: {result.ValidTo.Value:dd-MMM-yyyy}"
                : "No active license found in Concept License Manager"
        };
    }

    private async Task RetryVerificationAsync()
    {
        if (IsChecking) return;

        IsChecking = true;
        CheckResultMessage = "Connecting to Concept License Manager...";

        try
        {
            var result = await _licenseService.ValidateLicenseAsync(forceOnline: true);
            ApplyResult(result);

            if (result.IsValid)
            {
                CheckResultMessage = "License verified successfully! Opening Concept Jewellery Pro...";
                await Task.Delay(700);
                VerificationSucceeded?.Invoke();
            }
            else
            {
                CheckResultMessage = result.StatusMessage;
            }
        }
        catch (Exception ex)
        {
            CheckResultMessage = $"Verification error: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }
}
