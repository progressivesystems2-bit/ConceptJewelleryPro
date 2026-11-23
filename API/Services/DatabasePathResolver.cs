namespace ConceptJewelleryPro.API.Services;

public static class DatabasePathResolver
{
    public static string ResolveDatabasePath(IConfiguration configuration)
    {
        // 1. Try explicit DatabasePath from appsettings.json or environment
        var configured = configuration["DatabasePath"];
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        // 2. Try AppContext.BaseDirectory paths
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "RGRG2526.mdb"),
            Path.Combine(baseDir, "data", "RGRG2526.mdb"),
            Path.Combine(baseDir, "App_Data", "RGRG2526.mdb"),
            Path.Combine(Directory.GetCurrentDirectory(), "RGRG2526.mdb"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "RGRG2526.mdb"),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "RGRG2526.mdb")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        // 3. Search any .mdb file in base directory or current working directory
        try
        {
            var foundInBase = Directory.GetFiles(baseDir, "*.mdb", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrEmpty(foundInBase) && File.Exists(foundInBase))
            {
                return Path.GetFullPath(foundInBase);
            }

            var currDir = Directory.GetCurrentDirectory();
            if (currDir != baseDir)
            {
                var foundInCurr = Directory.GetFiles(currDir, "*.mdb", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(foundInCurr) && File.Exists(foundInCurr))
                {
                    return Path.GetFullPath(foundInCurr);
                }
            }
        }
        catch { }

        // Fallback to configured or local default
        return !string.IsNullOrWhiteSpace(configured) ? configured : Path.Combine(baseDir, "RGRG2526.mdb");
    }
}
