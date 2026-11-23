using ConceptJewelleryPro.Data.Access.Repositories;

namespace ConceptJewelleryPro.Infrastructure.Settings;

public sealed class DemoSettingsRepository : ISettingsRepository
{
    private readonly Dictionary<string, string> _settings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SavePath"] = @"C:\ConceptJewellery\Exports",
        ["WaSendMethod"] = "API",
        ["WaAPIKey"] = "",
        ["WaTemplate"] = "Dear {CustomerName}, thank you for your purchase. Invoice #{InvoiceNo} of Rs.{NetAmount} dated {BillDate} has been generated successfully.",
        ["WaApiTemplateName"] = "",
        ["WaApiLanguage"] = "en_US",
        ["WaApiParameters"] = "{CustomerName}, {InvoiceNo}, {BillDate}, {NetAmount}, {Balance}, {StoreName}",
        ["WaAttachPdf"] = "False",
        ["WaApiDocumentUrl"] = "",
        ["WaBusinessNo"] = "",
        ["WaAutoSend"] = "False",
        ["EinvoiceMode"] = "DEMO",
        ["AllowNtWtModUpto"] = "0.000",
        ["AnniversaryMessage"] = "Dear {CustomerName}, happy Wedding Anniversary from all of us at Concept Jewellery! Wishing you and your spouse a lifetime of love, happiness, and togetherness!",
        ["BirthdayMessage"] = "Dear {CustomerName}, warm wishes on your Birthday from the entire team at Concept Jewellery! May your day be as sparkling and precious as gold. Wishing you joy, health, and prosperity!",
        ["WishesMode"] = "WhatsappCloud"
    };

    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, string> copy = new Dictionary<string, string>(_settings, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(copy);
    }

    public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        _settings.TryGetValue(key, out var val);
        return Task.FromResult<string?>(val);
    }

    public Task SaveAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        _settings[key] = value ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task SaveManyAsync(IEnumerable<KeyValuePair<string, string?>> settings, CancellationToken cancellationToken = default)
    {
        foreach (var kvp in settings)
        {
            _settings[kvp.Key] = kvp.Value ?? string.Empty;
        }
        return Task.CompletedTask;
    }
}
