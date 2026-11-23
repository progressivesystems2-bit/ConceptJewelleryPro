namespace ConceptJewelleryPro.Application.Licensing;

public enum LicenseValidationStatus
{
    Valid,
    OfflineGraceValid,
    NotRegistered,
    Deactivated,
    Expired,
    OfflineGraceExpired,
    ClockTampered,
    SignatureInvalid,
    ConnectionFailed,
    NodeLimitExceeded,
    NodeRevoked
}

public sealed class LicenseValidationResult
{
    public bool IsValid { get; init; }
    public LicenseValidationStatus Status { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string StatusMessage { get; init; } = string.Empty;
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
    public int? DaysRemaining { get; init; }
    public bool IsOfflineValidation { get; init; }
    public DateTime? LastVerifiedOnline { get; init; }
    public string? LicenseType { get; init; }
    public string Edition { get; init; } = "SingleHO";
    public int MaxBranches { get; init; } = 1;
    public int MaxNodes { get; init; } = 1;
    public int RegisteredNodesCount { get; init; } = 1;
    public string CurrentNodeMachineName { get; init; } = string.Empty;
    public string RegisteredMachines { get; init; } = string.Empty;
    public string? EnabledModules { get; init; }

    public bool IsMultiBranchAllowed => string.Equals(Edition, "MultiBranch", StringComparison.OrdinalIgnoreCase);

    public bool IsModuleEnabled(string moduleCode)
    {
        if (string.Equals(moduleCode, JewelleryModuleCatalog.Core, StringComparison.OrdinalIgnoreCase))
            return true;

        var set = JewelleryModuleCatalog.ParseEnabledModules(EnabledModules);
        return set.Contains(moduleCode);
    }

    public bool IsScreenLicensed(string? screenTitle)
    {
        var moduleCode = JewelleryModuleCatalog.ResolveModuleForScreen(screenTitle);
        return IsModuleEnabled(moduleCode);
    }

    public string EnabledModulesDisplay => JewelleryModuleCatalog.FormatModulesForDisplay(EnabledModules);

    public static LicenseValidationResult SuccessOnline(
        CompanyLicenseRecord record,
        int? daysRemaining,
        int registeredNodesCount = 1,
        string currentNodeMachineName = "",
        string registeredMachines = "") =>
        new()
        {
            IsValid = true,
            Status = LicenseValidationStatus.Valid,
            CompanyName = record.CompanyName,
            StatusMessage = "License active and verified with Concept License Manager cloud database.",
            ValidFrom = record.ValidFrom,
            ValidTo = record.ValidTo,
            DaysRemaining = daysRemaining,
            IsOfflineValidation = false,
            LastVerifiedOnline = DateTime.UtcNow,
            LicenseType = record.LicenseType,
            Edition = string.IsNullOrWhiteSpace(record.Edition) ? "SingleHO" : record.Edition,
            MaxBranches = record.MaxBranches > 0 ? record.MaxBranches : 1,
            MaxNodes = record.MaxNodes > 0 ? record.MaxNodes : 1,
            RegisteredNodesCount = registeredNodesCount,
            CurrentNodeMachineName = currentNodeMachineName,
            RegisteredMachines = registeredMachines,
            EnabledModules = record.EnabledModules
        };

    public static LicenseValidationResult SuccessOffline(
        string companyName,
        DateTime? validFrom,
        DateTime? validTo,
        int? daysRemaining,
        int offlineDaysLeft,
        DateTime lastVerifiedOnline,
        string licenseType,
        string edition = "SingleHO",
        int maxBranches = 1,
        int maxNodes = 1,
        string currentNodeMachineName = "",
        string? enabledModules = null) =>
        new()
        {
            IsValid = true,
            Status = LicenseValidationStatus.OfflineGraceValid,
            CompanyName = companyName,
            StatusMessage = $"Operating in offline grace mode ({offlineDaysLeft} day(s) remaining). Please connect to the internet to sync your license.",
            ValidFrom = validFrom,
            ValidTo = validTo,
            DaysRemaining = daysRemaining,
            IsOfflineValidation = true,
            LastVerifiedOnline = lastVerifiedOnline,
            LicenseType = licenseType,
            Edition = string.IsNullOrWhiteSpace(edition) ? "SingleHO" : edition,
            MaxBranches = maxBranches > 0 ? maxBranches : 1,
            MaxNodes = maxNodes > 0 ? maxNodes : 1,
            RegisteredNodesCount = 1,
            CurrentNodeMachineName = currentNodeMachineName,
            EnabledModules = enabledModules
        };

    public static LicenseValidationResult Failed(
        LicenseValidationStatus status,
        string companyName,
        string message,
        DateTime? validTo = null,
        int maxNodes = 1,
        int registeredNodesCount = 0) =>
        new()
        {
            IsValid = false,
            Status = status,
            CompanyName = companyName,
            StatusMessage = message,
            ValidTo = validTo,
            MaxNodes = maxNodes,
            RegisteredNodesCount = registeredNodesCount,
            CurrentNodeMachineName = DeviceFingerprintHelper.GetMachineName()
        };
}

public interface ILicenseValidationService
{
    LicenseValidationResult? CurrentStatus { get; }
    bool IsLicensingEnforced { get; }
    bool IsScreenLicensed(string? screenTitle);
    Task<string> GetActiveCompanyNameAsync(CancellationToken cancellationToken = default);
    Task<LicenseValidationResult> ValidateLicenseAsync(bool forceOnline = false, CancellationToken cancellationToken = default);
}
