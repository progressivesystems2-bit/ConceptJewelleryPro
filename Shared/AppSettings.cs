using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ConceptJewelleryPro.Shared;

public sealed class AppSettings
{
    public const string AppName = "Concept Jewellery Pro";
    public const string Version = "1.0.0";

    public string Counter { get; set; } = "ad";
    public string StoreID { get; set; } = "1";
    public MastersIndiaSettings MastersIndia { get; set; } = new();
    public UpdateSettings Updates { get; set; } = new();
    public LicensingOptions Licensing { get; set; } = new();

    public static string GetInstalledVersion()
    {
        try
        {
            var entry = Assembly.GetEntryAssembly();
            var infoVersion = entry?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVersion))
            {
                var plusIdx = infoVersion.IndexOf('+');
                return plusIdx > 0 ? infoVersion[..plusIdx] : infoVersion;
            }

            var asmVersion = entry?.GetName().Version;
            if (asmVersion is not null)
            {
                return $"{asmVersion.Major}.{asmVersion.Minor}.{Math.Max(0, asmVersion.Build)}";
            }
        }
        catch
        {
            // Fallback to constant
        }

        return Version;
    }

    public static AppSettings Load()
    {
        try
        {
            var candidatePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")
            };

            var found = candidatePaths.FirstOrDefault(File.Exists);
            if (found != null)
            {
                var json = File.ReadAllText(found);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
            }
        }
        catch
        {
            // Fallback default
        }

        return new AppSettings();
    }
}

public sealed class MastersIndiaSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = "https://commonapi.mastersindia.co/oauth/access_token";
    public string GstLookupEndpoint { get; set; } = "https://commonapi.mastersindia.co/commonapis/searchgstin";
}

public sealed class UpdateSettings
{
    public string GitHubOwner { get; set; } = "progressivesystems2-bit";
    public string GitHubRepo { get; set; } = "ConceptJewelleryPro";
    public bool IncludePreReleases { get; set; } = false;
    public string AssetNamePattern { get; set; } = "ConceptJewelleryPro-WPF";
    public string GitHubToken { get; set; } = string.Empty;
}

public sealed class LicensingOptions
{
    public string SupabaseUrl { get; set; } = string.Empty;
    public string SupabaseKey { get; set; } = string.Empty;
    public int OfflineGraceDays { get; set; } = 7;
    public bool EnforceLicensing { get; set; } = true;
}
