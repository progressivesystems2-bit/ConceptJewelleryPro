using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConceptJewelleryPro.Shared;

namespace ConceptJewelleryPro.Application.Licensing;

public sealed class LicenseValidationService : ILicenseValidationService
{
    private readonly Func<CancellationToken, Task<string?>> _resolveCompanyNameAsync;
    private readonly LicensingOptions _options;
    private readonly HttpClient _httpClient;

    private static readonly byte[] EncryptionSalt = [
        0x43, 0x6F, 0x6E, 0x63, 0x65, 0x70, 0x74, 0x4A,
        0x65, 0x77, 0x65, 0x6C, 0x50, 0x72, 0x6F, 0x31
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public const int MaxAllowedGraceDays = 7;

    public LicenseValidationResult? CurrentStatus { get; private set; }

    public LicenseValidationService(
        Func<CancellationToken, Task<string?>> resolveCompanyNameAsync,
        LicensingOptions? options = null,
        HttpClient? httpClient = null)
    {
        _resolveCompanyNameAsync = resolveCompanyNameAsync;
        _options = options ?? new LicensingOptions();
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    /// <summary>
    /// In Release builds distributed to clients, licensing is ALWAYS enforced.
    /// In DEBUG builds only, developers can set Licensing:EnforceLicensing = false in appsettings.json.
    /// </summary>
    public bool IsLicensingEnforced =>
#if DEBUG
        _options.EnforceLicensing;
#else
        true;
#endif

    public bool IsScreenLicensed(string? screenTitle)
    {
        if (!IsLicensingEnforced)
            return true;

        if (CurrentStatus is null)
            return true;

        return CurrentStatus.IsScreenLicensed(screenTitle);
    }

    public async Task<string> GetActiveCompanyNameAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var resolved = await _resolveCompanyNameAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved.Trim();
        }
        catch
        {
        }

        return string.Empty;
    }

    public async Task<LicenseValidationResult> ValidateLicenseAsync(bool forceOnline = false, CancellationToken cancellationToken = default)
    {
        if (!IsLicensingEnforced)
        {
            var devCompany = await GetActiveCompanyNameAsync(cancellationToken);
            CurrentStatus = new LicenseValidationResult
            {
                IsValid = true,
                Status = LicenseValidationStatus.Valid,
                CompanyName = string.IsNullOrWhiteSpace(devCompany) ? "License Enforcement Disabled (Debug)" : devCompany,
                StatusMessage = "Licensing check bypassed in DEBUG mode via appsettings.json.",
                Edition = "MultiBranch",
                MaxBranches = 99,
                MaxNodes = 99,
                CurrentNodeMachineName = DeviceFingerprintHelper.GetMachineName()
            };
            return CurrentStatus;
        }

        var companyName = await GetActiveCompanyNameAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(companyName))
        {
            CurrentStatus = LicenseValidationResult.Failed(
                LicenseValidationStatus.NotRegistered,
                string.Empty,
                "No active company name found in the database DateRange table or General Settings.");
            return CurrentStatus;
        }

        var cached = LoadCache(companyName);

        // Anti-clock rollback check
        if (cached != null && DateTime.UtcNow < cached.HighestClockUtc.AddHours(-24))
        {
            CurrentStatus = LicenseValidationResult.Failed(
                LicenseValidationStatus.ClockTampered,
                companyName,
                "System clock rollback detected. Please restore the correct date and time on this computer.");
            return CurrentStatus;
        }

        // Attempt online verification against ConceptS License Manager Supabase database
        try
        {
            var onlineResult = await QuerySupabaseLicenseAsync(companyName, cancellationToken);
            if (onlineResult != null)
            {
                CurrentStatus = onlineResult;
                return CurrentStatus;
            }
        }
        catch
        {
            // Fall back to offline encrypted grace cache
        }

        if (cached != null)
        {
            var offlineResult = EvaluateCachedLicense(companyName, cached);
            CurrentStatus = offlineResult;
            return CurrentStatus;
        }

        var effectiveUrl = LicensingCredentials.ResolveSupabaseUrl(_options.SupabaseUrl);
        var effectiveKey = LicensingCredentials.ResolveSupabaseKey(_options.SupabaseKey);

        if (string.IsNullOrWhiteSpace(effectiveUrl) || string.IsNullOrWhiteSpace(effectiveKey))
        {
            CurrentStatus = LicenseValidationResult.Failed(
                LicenseValidationStatus.ConnectionFailed,
                companyName,
                "Cloud licensing server is not configured. Please configure your licensing Supabase URL and Key.");
            return CurrentStatus;
        }

        CurrentStatus = LicenseValidationResult.Failed(
            LicenseValidationStatus.ConnectionFailed,
            companyName,
            $"Unable to verify license online for '{companyName}'. Please ensure this computer is connected to the internet.");
        return CurrentStatus;
    }

    private async Task<LicenseValidationResult?> QuerySupabaseLicenseAsync(string companyName, CancellationToken cancellationToken)
    {
        var effectiveUrl = LicensingCredentials.ResolveSupabaseUrl(_options.SupabaseUrl);
        var effectiveKey = LicensingCredentials.ResolveSupabaseKey(_options.SupabaseKey);

        if (string.IsNullOrWhiteSpace(effectiveUrl) || string.IsNullOrWhiteSpace(effectiveKey))
            return null;

        var url = $"{effectiveUrl.TrimEnd('/')}/rest/v1/app_licenses?company_name=ilike.{Uri.EscapeDataString(companyName)}&select=*";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", effectiveKey);
        request.Headers.Add("Authorization", $"Bearer {effectiveKey}");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var records = JsonSerializer.Deserialize<List<CompanyLicenseRecord>>(json, JsonOptions);

        if (records == null || records.Count == 0)
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.NotRegistered,
                companyName,
                $"Company '{companyName}' is not registered in the Concept License Manager. Please contact your software provider to activate your license.");
        }

        var record = records[0];

        // 1. Verify Digital Signature against Vendor RSA Public Key (includes EnabledModules tamper lock)
        var isSignatureValid = LicenseSigner.VerifySignature(
            record.CompanyName,
            record.ValidFrom,
            record.ValidTo,
            record.LicenseType,
            record.Edition,
            record.EnabledModules,
            record.Signature);

        if (!isSignatureValid)
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.SignatureInvalid,
                companyName,
                $"License digital signature verification failed for '{companyName}'. Counterfeit or tampered license record detected.");
        }

        // 2. Active check
        if (!record.IsActive)
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.Deactivated,
                companyName,
                $"The license for '{companyName}' has been suspended or deactivated. Please contact your software provider.");
        }

        // 3. Expiry check
        if (record.ValidTo.HasValue && DateTime.Today > record.ValidTo.Value.Date)
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.Expired,
                companyName,
                $"The license for '{companyName}' expired on {record.ValidTo.Value:dd-MMM-yyyy}. Please renew your subscription to continue using Concept Jewellery Pro.",
                record.ValidTo.Value);
        }

        // 4. Node lock verification & registration
        var (nodeSuccess, nodeFailure, registeredNodesCount, registeredMachines) =
            await ValidateAndRegisterNodeAsync(record, effectiveUrl, effectiveKey, cancellationToken);

        if (!nodeSuccess && nodeFailure != null)
            return nodeFailure;

        int? daysRemaining = record.ValidTo.HasValue
            ? Math.Max(0, (int)(record.ValidTo.Value.Date - DateTime.Today).TotalDays)
            : null;

        var currentMachineId = DeviceFingerprintHelper.GetMachineId();
        var currentMachineName = DeviceFingerprintHelper.GetMachineName();

        SaveCache(new CachedLicenseState
        {
            CompanyName = record.CompanyName,
            IsActive = record.IsActive,
            ValidFrom = record.ValidFrom,
            ValidTo = record.ValidTo,
            LicenseType = record.LicenseType,
            Edition = record.Edition,
            MaxBranches = record.MaxBranches,
            MaxNodes = record.MaxNodes > 0 ? record.MaxNodes : 1,
            AuthorizedMachineId = currentMachineId,
            AuthorizedMachineName = currentMachineName,
            IsNodeAuthorized = true,
            EnabledModules = record.EnabledModules,
            Signature = record.Signature,
            LastVerifiedOnlineUtc = DateTime.UtcNow,
            HighestClockUtc = DateTime.UtcNow
        });

        return LicenseValidationResult.SuccessOnline(
            record,
            daysRemaining,
            registeredNodesCount,
            currentMachineName,
            registeredMachines);
    }

    private LicenseValidationResult EvaluateCachedLicense(string companyName, CachedLicenseState cached)
    {
        if (!string.Equals(cached.CompanyName.Trim(), companyName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.NotRegistered,
                companyName,
                $"Cached license company '{cached.CompanyName}' does not match active company '{companyName}'. Internet connection required.");
        }

        var isCachedSigValid = LicenseSigner.VerifySignature(
            cached.CompanyName,
            cached.ValidFrom,
            cached.ValidTo,
            cached.LicenseType,
            cached.Edition,
            cached.EnabledModules,
            cached.Signature);

        if (!isCachedSigValid)
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.SignatureInvalid,
                companyName,
                $"Cached license signature is invalid or tampered for '{companyName}'. Please connect to the internet to verify.");
        }

        if (!cached.IsActive)
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.Deactivated,
                companyName,
                $"License for '{companyName}' is suspended. Please contact your software provider.");
        }

        if (cached.ValidTo.HasValue && DateTime.Today > cached.ValidTo.Value.Date)
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.Expired,
                companyName,
                $"License for '{companyName}' expired on {cached.ValidTo.Value:dd-MMM-yyyy}. Please renew your subscription.",
                cached.ValidTo.Value);
        }

        var configuredGrace = _options.OfflineGraceDays <= 0 ? MaxAllowedGraceDays : _options.OfflineGraceDays;
        var offlineGraceDays = Math.Clamp(configuredGrace, 1, MaxAllowedGraceDays);
        var graceExpiryUtc = cached.LastVerifiedOnlineUtc.AddDays(offlineGraceDays);

        if (DateTime.UtcNow > graceExpiryUtc)
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.OfflineGraceExpired,
                companyName,
                $"The {offlineGraceDays}-day offline grace period has expired. Please connect this computer to the internet to verify your license.",
                cached.ValidTo);
        }

        if (DateTime.UtcNow > cached.HighestClockUtc)
        {
            cached.HighestClockUtc = DateTime.UtcNow;
            SaveCache(cached);
        }

        var daysLeftOffline = Math.Max(0, (int)Math.Ceiling((graceExpiryUtc - DateTime.UtcNow).TotalDays));
        int? daysRemaining = cached.ValidTo.HasValue
            ? Math.Max(0, (int)(cached.ValidTo.Value.Date - DateTime.Today).TotalDays)
            : null;

        var currentMachineId = DeviceFingerprintHelper.GetMachineId();
        var currentMachineName = DeviceFingerprintHelper.GetMachineName();

        if (!string.IsNullOrEmpty(cached.AuthorizedMachineId) &&
            !string.Equals(cached.AuthorizedMachineId, currentMachineId, StringComparison.OrdinalIgnoreCase))
        {
            return LicenseValidationResult.Failed(
                LicenseValidationStatus.NodeLimitExceeded,
                companyName,
                $"This computer '{currentMachineName}' has not been authorized for the '{companyName}' license. Please connect to the internet to authorize this machine.");
        }

        return LicenseValidationResult.SuccessOffline(
            companyName,
            cached.ValidFrom,
            cached.ValidTo,
            daysRemaining,
            daysLeftOffline,
            cached.LastVerifiedOnlineUtc,
            cached.LicenseType,
            cached.Edition,
            cached.MaxBranches,
            cached.MaxNodes > 0 ? cached.MaxNodes : 1,
            currentMachineName,
            cached.EnabledModules);
    }

    private async Task<(bool Success, LicenseValidationResult? FailureResult, int RegisteredCount, string RegisteredMachines)> ValidateAndRegisterNodeAsync(
        CompanyLicenseRecord licenseRecord,
        string effectiveUrl,
        string effectiveKey,
        CancellationToken cancellationToken)
    {
        var machineId = DeviceFingerprintHelper.GetMachineId();
        var machineName = DeviceFingerprintHelper.GetMachineName();
        var osUser = DeviceFingerprintHelper.GetOsUser();
        var appVersion = $"CJP-{AppSettings.GetInstalledVersion()}";
        var maxNodes = licenseRecord.MaxNodes > 0 ? licenseRecord.MaxNodes : 1;

        try
        {
            var nodesUrl = $"{effectiveUrl.TrimEnd('/')}/rest/v1/app_license_nodes?company_name=eq.{Uri.EscapeDataString(licenseRecord.CompanyName)}&select=*";
            using var req = new HttpRequestMessage(HttpMethod.Get, nodesUrl);
            req.Headers.Add("apikey", effectiveKey);
            req.Headers.Add("Authorization", $"Bearer {effectiveKey}");

            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                return (true, null, 1, machineName);
            }

            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            var nodes = JsonSerializer.Deserialize<List<LicenseNodeRecord>>(json, JsonOptions) ?? new List<LicenseNodeRecord>();

            var existingNode = nodes.FirstOrDefault(n => string.Equals(n.MachineId, machineId, StringComparison.OrdinalIgnoreCase));
            if (existingNode != null)
            {
                if (!existingNode.IsActive)
                {
                    return (false, LicenseValidationResult.Failed(
                        LicenseValidationStatus.NodeRevoked,
                        licenseRecord.CompanyName,
                        $"This computer '{machineName}' has been deactivated by the administrator for license '{licenseRecord.CompanyName}'. Please contact support."), 0, string.Empty);
                }

                // Async heartbeat update
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var patchUrl = $"{effectiveUrl.TrimEnd('/')}/rest/v1/app_license_nodes?id=eq.{existingNode.Id}";
                        using var patchReq = new HttpRequestMessage(HttpMethod.Patch, patchUrl);
                        patchReq.Headers.Add("apikey", effectiveKey);
                        patchReq.Headers.Add("Authorization", $"Bearer {effectiveKey}");
                        var payload = JsonSerializer.Serialize(new
                        {
                            last_active_at = DateTime.UtcNow,
                            machine_name = machineName,
                            os_user = osUser,
                            app_version = appVersion
                        }, JsonOptions);
                        patchReq.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                        await _httpClient.SendAsync(patchReq);
                    }
                    catch
                    {
                    }
                });

                var activeCount = nodes.Count(n => n.IsActive);
                var machineList = string.Join(", ", nodes.Where(n => n.IsActive).Select(n => n.MachineName));
                return (true, null, activeCount, machineList);
            }

            var currentActiveNodes = nodes.Where(n => n.IsActive).ToList();
            if (currentActiveNodes.Count >= maxNodes)
            {
                var machineList = string.Join(", ", currentActiveNodes.Select(n => n.MachineName));
                return (false, LicenseValidationResult.Failed(
                    LicenseValidationStatus.NodeLimitExceeded,
                    licenseRecord.CompanyName,
                    $"Computer node limit reached. Your license for '{licenseRecord.CompanyName}' allows a maximum of {maxNodes} computer(s), and {currentActiveNodes.Count} computer(s) are already registered: [{machineList}]. This computer is '{machineName}'. Please upgrade your license or ask your administrator to revoke an unused computer in License Manager.",
                    maxNodes: maxNodes,
                    registeredNodesCount: currentActiveNodes.Count), currentActiveNodes.Count, machineList);
            }

            var insertUrl = $"{effectiveUrl.TrimEnd('/')}/rest/v1/app_license_nodes";
            using var insertReq = new HttpRequestMessage(HttpMethod.Post, insertUrl);
            insertReq.Headers.Add("apikey", effectiveKey);
            insertReq.Headers.Add("Authorization", $"Bearer {effectiveKey}");
            insertReq.Headers.Add("Prefer", "return=minimal");

            var newRecord = new
            {
                license_id = licenseRecord.Id,
                company_name = licenseRecord.CompanyName,
                machine_id = machineId,
                machine_name = machineName,
                os_user = osUser,
                app_version = appVersion,
                first_registered_at = DateTime.UtcNow,
                last_active_at = DateTime.UtcNow,
                is_active = true
            };

            insertReq.Content = new StringContent(JsonSerializer.Serialize(newRecord, JsonOptions), Encoding.UTF8, "application/json");
            using var insertResp = await _httpClient.SendAsync(insertReq, cancellationToken);

            currentActiveNodes.Add(new LicenseNodeRecord { MachineName = machineName });
            var finalMachineList = string.Join(", ", currentActiveNodes.Select(n => n.MachineName));
            return (true, null, currentActiveNodes.Count, finalMachineList);
        }
        catch
        {
            return (true, null, 1, machineName);
        }
    }

    private static string GetCacheFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ConceptJewelleryPro");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "license_state.dat");
    }

    private CachedLicenseState? LoadCache(string companyName)
    {
        try
        {
            var path = GetCacheFilePath();
            if (!File.Exists(path))
                return null;

            var cipherBytes = File.ReadAllBytes(path);
            var key = DeriveKey(companyName);
            var json = DecryptAes(cipherBytes, key);
            return JsonSerializer.Deserialize<CachedLicenseState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void SaveCache(CachedLicenseState state)
    {
        try
        {
            var path = GetCacheFilePath();
            var json = JsonSerializer.Serialize(state, JsonOptions);
            var key = DeriveKey(state.CompanyName);
            var cipherBytes = EncryptAes(json, key);
            File.WriteAllBytes(path, cipherBytes);
        }
        catch
        {
        }
    }

    private static byte[] DeriveKey(string companyName)
    {
        var normalized = (companyName ?? string.Empty).Trim().ToUpperInvariant();
        return Rfc2898DeriveBytes.Pbkdf2(normalized, EncryptionSalt, 10000, HashAlgorithmName.SHA256, 32);
    }

    private static byte[] EncryptAes(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plainText);
        }

        return ms.ToArray();
    }

    private static string DecryptAes(byte[] cipherData, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;

        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(cipherData, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipherData, iv.Length, cipherData.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);

        return sr.ReadToEnd();
    }

    private sealed class CachedLicenseState
    {
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public string LicenseType { get; set; } = "Subscription";
        public string Edition { get; set; } = "SingleHO";
        public int MaxBranches { get; set; } = 1;
        public int MaxNodes { get; set; } = 1;
        public string AuthorizedMachineId { get; set; } = string.Empty;
        public string AuthorizedMachineName { get; set; } = string.Empty;
        public bool IsNodeAuthorized { get; set; } = true;
        public string? EnabledModules { get; set; }
        public string Signature { get; set; } = string.Empty;
        public DateTime LastVerifiedOnlineUtc { get; set; }
        public DateTime HighestClockUtc { get; set; }
    }
}
