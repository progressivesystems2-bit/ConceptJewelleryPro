using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConceptJewelleryPro.Shared;

namespace ConceptJewelleryPro.Infrastructure.Services;

public sealed class GstLookupResult
{
    public string Gstin { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string GstType { get; set; } = string.Empty;
}

public sealed class MastersIndiaGstService
{
    private static readonly HttpClient HttpClient = new();

    public string TokenEndpoint { get; set; } = "https://commonapi.mastersindia.co/oauth/access_token";
    public string GstLookupEndpoint { get; set; } = "https://commonapi.mastersindia.co/commonapis/searchgstin";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    public MastersIndiaGstService()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            var candidatePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
                "E:\\repos\\ConceptJewelleryPro\\WPF\\appsettings.json"
            };

            var found = candidatePaths.FirstOrDefault(File.Exists);
            if (found != null)
            {
                using var stream = File.OpenRead(found);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("MastersIndia", out var sec))
                {
                    if (sec.TryGetProperty("TokenEndpoint", out var te) && !string.IsNullOrWhiteSpace(te.GetString()))
                        TokenEndpoint = te.GetString()!;
                    if (sec.TryGetProperty("GstLookupEndpoint", out var gle) && !string.IsNullOrWhiteSpace(gle.GetString()))
                        GstLookupEndpoint = gle.GetString()!;
                    if (sec.TryGetProperty("ClientId", out var ci)) ClientId = ci.GetString() ?? string.Empty;
                    if (sec.TryGetProperty("ClientSecret", out var cs)) ClientSecret = cs.GetString() ?? string.Empty;
                    if (sec.TryGetProperty("ApiKey", out var ak)) ApiKey = ak.GetString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Fallback to default properties
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var dict = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", ClientId },
                { "client_secret", ClientSecret }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(dict)
            };

            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                request.Headers.Add("client_id", ClientId);
                request.Headers.Add("client_secret", ClientSecret);
            }

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
            {
                return tokenProp.GetString();
            }
        }
        catch
        {
            // Token fetch failed
        }

        return null;
    }

    public async Task<GstLookupResult?> SearchGstinAsync(string gstin)
    {
        if (string.IsNullOrWhiteSpace(gstin)) return null;

        var cleanGstin = gstin.Trim().ToUpperInvariant();
        try
        {
            var token = await GetAccessTokenAsync();
            var url = $"{GstLookupEndpoint}?gstin={cleanGstin}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            if (!string.IsNullOrWhiteSpace(ClientId))
            {
                request.Headers.Add("client_id", ClientId);
            }

            var response = await HttpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(json)) return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement target = root;
            if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object)
            {
                target = dataElement;
            }

            var result = new GstLookupResult
            {
                Gstin = cleanGstin
            };

            if (target.TryGetProperty("tradeNam", out var tn)) result.TradeName = tn.GetString() ?? string.Empty;
            if (target.TryGetProperty("lgnm", out var ln)) result.LegalName = ln.GetString() ?? string.Empty;
            if (target.TryGetProperty("dty", out var dty)) result.GstType = dty.GetString() ?? string.Empty;

            if (target.TryGetProperty("pradr", out var pradr) &&
                pradr.TryGetProperty("addr", out var addr))
            {
                var bno = addr.TryGetProperty("bno", out var b) ? b.GetString() : string.Empty;
                var flno = addr.TryGetProperty("flno", out var f) ? f.GetString() : string.Empty;
                var st = addr.TryGetProperty("st", out var s) ? s.GetString() : string.Empty;
                var loc = addr.TryGetProperty("loc", out var l) ? l.GetString() : string.Empty;
                var pncd = addr.TryGetProperty("pncd", out var p) ? p.GetString() : string.Empty;
                var stcd = addr.TryGetProperty("stcd", out var stc) ? stc.GetString() : string.Empty;
                var dst = addr.TryGetProperty("dst", out var d) ? d.GetString() : string.Empty;

                result.Address = string.Join(" ", new[] { bno, flno, st }.Where(x => !string.IsNullOrWhiteSpace(x)));
                result.Address2 = loc ?? string.Empty;
                result.City = dst ?? loc ?? string.Empty;
                result.PinCode = pncd ?? string.Empty;
                result.State = PincodeService.MapStateName(stcd);
            }

            return result;
        }
        catch
        {
            return null;
        }
    }
}
