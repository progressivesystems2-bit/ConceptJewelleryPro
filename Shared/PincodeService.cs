using System.IO;
using System.Linq;
using System.Text.Json;

namespace ConceptJewelleryPro.Shared;

public static class PincodeService
{
    private static readonly Dictionary<string, (string District, string State)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static bool _isLoaded;
    private static readonly object LockObj = new();

    public static (string City, string State)? Lookup(string pincode)
    {
        if (string.IsNullOrWhiteSpace(pincode))
        {
            return null;
        }

        pincode = pincode.Trim();
        EnsureLoaded();

        if (Cache.TryGetValue(pincode, out var info))
        {
            return (info.District, MapStateName(info.State));
        }

        return null;
    }

    public static string MapStateName(string? stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return string.Empty;
        }

        var trimmed = stateName.Trim().ToUpperInvariant();
        return trimmed switch
        {
            "ODISHA" or "ORISSA" => "21-ODISHA",
            "WEST BENGAL" => "19-WEST BENGAL",
            "BIHAR" => "10-BIHAR",
            "JHARKHAND" => "20-JHARKHAND",
            "ANDHRA PRADESH" => "37-ANDHRA PRADESH",
            "TELANGANA" => "36-TELANGANA",
            "MAHARASHTRA" => "27-MAHARASHTRA",
            "DELHI" => "07-DELHI",
            "KARNATAKA" => "29-KARNATAKA",
            "TAMIL NADU" => "33-TAMIL NADU",
            "GUJARAT" => "24-GUJARAT",
            "UTTAR PRADESH" => "09-UTTAR PRADESH",
            "MADHYA PRADESH" => "23-MADHYA PRADESH",
            "RAJASTHAN" => "08-RAJASTHAN",
            "PUNJAB" => "03-PUNJAB",
            "HARYANA" => "06-HARYANA",
            "CHHATTISGARH" => "22-CHHATTISGARH",
            "ASSAM" => "18-ASSAM",
            "KERALA" => "32-KERALA",
            "GOA" => "30-GOA",
            "HIMACHAL PRADESH" => "02-HIMACHAL PRADESH",
            "JAMMU AND KASHMIR" or "JAMMU & KASHMIR" => "01-JAMMU AND KASHMIR",
            "UTTARAKHAND" => "05-UTTARAKHAND",
            "PUDUCHERRY" or "PONDICHERRY" => "34-PUDUCHERRY",
            _ => stateName
        };
    }

    private static void EnsureLoaded()
    {
        if (_isLoaded)
        {
            return;
        }

        lock (LockObj)
        {
            if (_isLoaded)
            {
                return;
            }

            try
            {
                var baseDir = AppContext.BaseDirectory;
                var candidatePaths = new[]
                {
                    Path.Combine(baseDir, "pincodeJSON.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "pincodeJSON.json"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "pincodeJSON.json"),
                    "E:\\repos\\ConceptJewelleryPro\\pincodeJSON.json"
                };

                string? foundPath = candidatePaths.FirstOrDefault(File.Exists);
                if (foundPath != null)
                {
                    using var stream = File.OpenRead(foundPath);
                    using var doc = JsonDocument.Parse(stream);
                    if (doc.RootElement.TryGetProperty("records", out var recordsElement) &&
                        recordsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in recordsElement.EnumerateArray())
                        {
                            var pin = item.TryGetProperty("pincode", out var p) ? p.GetString()?.Trim() : null;
                            var district = item.TryGetProperty("district", out var d) ? d.GetString()?.Trim() : null;
                            var state = item.TryGetProperty("statename", out var s) ? s.GetString()?.Trim() : null;

                            if (!string.IsNullOrEmpty(pin) && !Cache.ContainsKey(pin))
                            {
                                Cache[pin] = (district ?? string.Empty, state ?? string.Empty);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore load exceptions
            }
            finally
            {
                _isLoaded = true;
            }
        }
    }
}
