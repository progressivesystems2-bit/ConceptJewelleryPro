using System.Security.Cryptography;
using System.Text;

namespace ConceptJewelleryPro.Application.Licensing;

public static class DeviceFingerprintHelper
{
    private static string? _cachedMachineId;

    /// <summary>
    /// Optional provider set by the Windows host (WPF) to read HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid.
    /// </summary>
    public static Func<string?>? WindowsMachineGuidProvider { get; set; }

    public static string GetMachineId()
    {
        if (!string.IsNullOrEmpty(_cachedMachineId))
            return _cachedMachineId;

        var sb = new StringBuilder();

        // 1. Windows MachineGuid from Registry (stable across reboots)
        try
        {
            var machineGuid = WindowsMachineGuidProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(machineGuid))
            {
                sb.Append("GUID:").Append(machineGuid.Trim()).Append(';');
            }
        }
        catch
        {
        }

        // 2. Machine Name
        try
        {
            var computerName = Environment.MachineName;
            if (!string.IsNullOrWhiteSpace(computerName))
            {
                sb.Append("NAME:").Append(computerName.Trim().ToUpperInvariant()).Append(';');
            }
        }
        catch
        {
        }

        // 3. System Drive & Processor Count
        try
        {
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            sb.Append("DRIVE:").Append(systemDrive).Append(';');
            sb.Append("CORES:").Append(Environment.ProcessorCount).Append(';');
        }
        catch
        {
        }

        if (sb.Length < 10)
        {
            var fallbackId = GetOrCreateLocalFallbackId();
            sb.Append("FALLBACK:").Append(fallbackId);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        _cachedMachineId = Convert.ToHexString(hash);
        return _cachedMachineId;
    }

    public static string GetMachineName()
    {
        try { return Environment.MachineName; }
        catch { return "UNKNOWN-PC"; }
    }

    public static string GetOsUser()
    {
        try { return Environment.UserName; }
        catch { return "Unknown"; }
    }

    private static string GetOrCreateLocalFallbackId()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ConceptJewelleryPro");
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, ".node_id");
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(content))
                    return content;
            }

            var newId = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, newId);
            return newId;
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
