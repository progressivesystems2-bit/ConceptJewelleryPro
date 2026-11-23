using System.IO;
using System.Text.Json;
using ConceptJewelleryPro.Shared;

namespace ConceptJewelleryPro.Infrastructure.Settings;

public sealed class LocalPrinterSettingsStore
{
    private readonly string _filePath;

    public LocalPrinterSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath;
    }

    public static string DefaultFilePath => Path.Combine(AppContext.BaseDirectory, "printersettings.json");

    public static string SharedFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ConceptJewelleryPro",
        "printersettings.json");

    public static string LocalAppDataFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ConceptJewelleryPro",
        "printersettings.json");

    public LocalPrinterSettings Load()
    {
        var isNetworkBaseDir = _filePath.StartsWith(@"\\", StringComparison.Ordinal);
        var candidatePaths = isNetworkBaseDir
            ? new List<string>
            {
                SharedFilePath,
                LocalAppDataFilePath,
                _filePath,
                Path.Combine(Directory.GetCurrentDirectory(), "printersettings.json")
            }
            : new List<string>
            {
                _filePath,
                SharedFilePath,
                LocalAppDataFilePath,
                Path.Combine(Directory.GetCurrentDirectory(), "printersettings.json"),
                Path.Combine(AppContext.BaseDirectory, "..", "printersettings.json"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "printersettings.json")
            };

        foreach (var path in candidatePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<LocalPrinterSettings>(json);
                    if (settings is not null &&
                        (!string.IsNullOrWhiteSpace(settings.DefaultBarcodePrinter) ||
                         !string.IsNullOrWhiteSpace(settings.DefaultBillPrinter) ||
                         !string.IsNullOrWhiteSpace(settings.SoftwarePath) ||
                         !string.IsNullOrWhiteSpace(settings.BarcodePrintEngine)))
                    {
                        return settings;
                    }
                }
            }
            catch
            {
            }
        }

        return new LocalPrinterSettings();
    }

    public void Save(LocalPrinterSettings settings)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(settings, options);

        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
        }

        try
        {
            var sharedDir = Path.GetDirectoryName(SharedFilePath);
            if (!string.IsNullOrWhiteSpace(sharedDir)) Directory.CreateDirectory(sharedDir);
            File.WriteAllText(SharedFilePath, json);
        }
        catch
        {
        }
    }
}
