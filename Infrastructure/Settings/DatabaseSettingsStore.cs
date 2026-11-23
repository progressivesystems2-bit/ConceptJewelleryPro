using System.Text.Json;
using ConceptJewelleryPro.Shared;

namespace ConceptJewelleryPro.Infrastructure.Settings;

public sealed class DatabaseSettingsStore
{
    private readonly string _filePath;

    public DatabaseSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultFilePath => Path.Combine(AppContext.BaseDirectory, "dbsettings.json");

    public DatabaseSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                return JsonSerializer.Deserialize<DatabaseSettings>(File.ReadAllText(_filePath)) ?? new DatabaseSettings();
            }
        }
        catch
        {
        }

        return new DatabaseSettings();
    }

    public void Save(DatabaseSettings settings)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, options));
    }
}
