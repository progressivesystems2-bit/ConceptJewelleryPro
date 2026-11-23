using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;
using ConceptJewelleryPro.Shared;

namespace ConceptJewelleryPro.Infrastructure.Services;

public sealed class EInvoiceItemDto
{
    public int SlNo { get; set; } = 1;
    public string ItemName { get; set; } = string.Empty;
    public string HsnCode { get; set; } = "71131910";
    public decimal Qty { get; set; } = 1;
    public string Unit { get; set; } = "GMS";
    public decimal Rate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal GstRate { get; set; } = 3.00m;
    public decimal CgstAmt { get; set; }
    public decimal SgstAmt { get; set; }
    public decimal IgstAmt { get; set; }
    public decimal TotalAmt { get; set; }
}

public sealed class EInvoiceRequestDto
{
    public string VouNo { get; set; } = string.Empty;
    public DateTime BillDate { get; set; } = DateTime.Today;
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerGstin { get; set; } = string.Empty;
    public string BuyerAddress { get; set; } = string.Empty;
    public string BuyerCity { get; set; } = "CUTTACK";
    public string BuyerPincode { get; set; } = "753001";
    public string BuyerState { get; set; } = "ODISHA";
    public decimal GrossAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal NetAmount { get; set; }
    public List<EInvoiceItemDto> Items { get; set; } = new();
}

public sealed class EInvoiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Irn { get; set; }
    public string? AckNo { get; set; }
    public string? AckDate { get; set; }
    public string? QrCode { get; set; }
    public string? SignedInvoice { get; set; }
    public string Mode { get; set; } = "DEMO";
}

public interface IEInvoiceService
{
    Task<EInvoiceResult> GenerateEInvoiceAsync(EInvoiceRequestDto request, CancellationToken cancellationToken = default);
}

public sealed class EInvoiceService : IEInvoiceService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ISettingsRepository? _settingsRepository;

    public sealed class EInvoiceConfig
    {
        public string TokenEndpoint { get; set; } = "https://commonapi.mastersindia.co/oauth/access_token";
        public string GenerateEndpoint { get; set; } = "https://commonapi.mastersindia.co/commonapis/genirn";
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Gstin { get; set; } = "21AAAAA0000A1Z5";
        public string LegalName { get; set; } = "Concept Jewellery";
        public string TradeName { get; set; } = "Concept Jewellery";
        public string Address { get; set; } = "Nayasarak";
        public string City { get; set; } = "CUTTACK";
        public int Pincode { get; set; } = 753002;
        public string StateCode { get; set; } = "21";
    }

    public EInvoiceService(ISettingsRepository? settingsRepository = null)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task<EInvoiceResult> GenerateEInvoiceAsync(EInvoiceRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.VouNo))
        {
            return new EInvoiceResult { Success = false, Message = "Invoice number is required for E-Invoice generation." };
        }

        if (request.Items.Count == 0)
        {
            return new EInvoiceResult { Success = false, Message = "At least one item is required to generate E-Invoice." };
        }

        // 1. Determine Mode (DEMO vs LIVE)
        string mode = "DEMO";
        if (_settingsRepository != null)
        {
            try
            {
                var settings = await _settingsRepository.GetAllAsync(cancellationToken);
                if (settings.TryGetValue(AppSetting.EinvoiceModeKey, out var m) && !string.IsNullOrWhiteSpace(m))
                {
                    mode = m.Trim().ToUpperInvariant();
                }
            }
            catch
            {
                // Fallback default
            }
        }

        // 2. Load Config from appsettings.json
        var config = LoadConfig(mode);

        // 3. If credentials exist, call API; otherwise, if DEMO mode, simulate realistic E-Invoice
        if (!string.IsNullOrWhiteSpace(config.ClientId) && !string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            return await CallEInvoiceApiAsync(request, config, mode, cancellationToken);
        }

        if (mode == "DEMO")
        {
            return GenerateSimulatedDemoEInvoice(request, config);
        }

        return new EInvoiceResult
        {
            Success = false,
            Mode = mode,
            Message = "E-Invoice ClientId / ClientSecret is not configured in appsettings.json for LIVE mode."
        };
    }

    private static async Task<EInvoiceResult> CallEInvoiceApiAsync(
        EInvoiceRequestDto request,
        EInvoiceConfig config,
        string mode,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Get Access Token
            var token = await GetAccessTokenAsync(config, cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                return new EInvoiceResult
                {
                    Success = false,
                    Mode = mode,
                    Message = "Failed to authenticate with E-Invoice API provider (check ClientId/Secret in appsettings.json)."
                };
            }

            // 2. Build standard JSON payload
            var payload = BuildStandardEInvoicePayload(request, config);
            var json = JsonSerializer.Serialize(payload);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.GenerateEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrWhiteSpace(config.ClientId))
            {
                httpRequest.Headers.Add("client_id", config.ClientId);
            }
            if (!string.IsNullOrWhiteSpace(config.Gstin))
            {
                httpRequest.Headers.Add("gstin", config.Gstin);
            }

            var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                JsonElement dataElement = root;
                if (root.TryGetProperty("data", out var data))
                {
                    dataElement = data;
                }

                string? irn = dataElement.TryGetProperty("Irn", out var irnProp) ? irnProp.GetString() : null;
                string? ackNo = dataElement.TryGetProperty("AckNo", out var ackProp) ? ackProp.ToString() : null;
                string? ackDt = dataElement.TryGetProperty("AckDt", out var ackDtProp) ? ackDtProp.GetString() : null;
                string? signedQr = dataElement.TryGetProperty("SignedQRCode", out var qrProp) ? qrProp.GetString() : null;
                string? signedInv = dataElement.TryGetProperty("SignedInvoice", out var invProp) ? invProp.GetString() : null;

                if (!string.IsNullOrWhiteSpace(irn))
                {
                    return new EInvoiceResult
                    {
                        Success = true,
                        Mode = mode,
                        Irn = irn,
                        AckNo = ackNo,
                        AckDate = ackDt,
                        QrCode = signedQr,
                        SignedInvoice = signedInv,
                        Message = $"E-Invoice generated successfully [{mode}]. IRN: {irn}"
                    };
                }
            }

            // Error handling
            string errMessage = "E-Invoice API returned error.";
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var m))
                {
                    errMessage = m.GetString() ?? errMessage;
                }
                else if (doc.RootElement.TryGetProperty("message", out var msgProp))
                {
                    errMessage = msgProp.GetString() ?? errMessage;
                }
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(responseBody) && responseBody.Length < 250)
                {
                    errMessage = responseBody;
                }
            }

            return new EInvoiceResult
            {
                Success = false,
                Mode = mode,
                Message = $"E-Invoice API error ({response.StatusCode}): {errMessage}"
            };
        }
        catch (Exception ex)
        {
            return new EInvoiceResult
            {
                Success = false,
                Mode = mode,
                Message = $"E-Invoice request exception: {ex.Message}"
            };
        }
    }

    private static async Task<string?> GetAccessTokenAsync(EInvoiceConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var dict = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", config.ClientId },
                { "client_secret", config.ClientSecret }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, config.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(dict)
            };

            var response = await HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
            {
                return tokenProp.GetString();
            }
        }
        catch
        {
            // Token request failed
        }
        return null;
    }

    private static object BuildStandardEInvoicePayload(EInvoiceRequestDto request, EInvoiceConfig config)
    {
        var cleanBuyerGstin = Regex.Replace(request.BuyerGstin ?? string.Empty, @"[^a-zA-Z0-9]", "").ToUpperInvariant();
        bool isB2B = cleanBuyerGstin.Length == 15;
        string buyerGstin = isB2B ? cleanBuyerGstin : "URP";
        string buyerStateCode = isB2B ? cleanBuyerGstin.Substring(0, 2) : config.StateCode;

        int pinCode = 753001;
        if (int.TryParse(Regex.Replace(request.BuyerPincode ?? string.Empty, @"\D", ""), out var p) && p > 100000)
        {
            pinCode = p;
        }

        var itemsList = request.Items.Select((it, idx) =>
        {
            var hsn = !string.IsNullOrWhiteSpace(it.HsnCode) ? it.HsnCode.Trim() : "71131910";
            return new
            {
                SlNo = (idx + 1).ToString(),
                PrdDesc = !string.IsNullOrWhiteSpace(it.ItemName) ? it.ItemName : "Jewellery Item",
                IsServc = "N",
                HsnCd = hsn,
                Qty = it.Qty > 0 ? it.Qty : 1,
                Unit = !string.IsNullOrWhiteSpace(it.Unit) ? it.Unit : "GMS",
                UnitPrice = it.Rate,
                TotAmt = it.TaxableAmount,
                Discount = 0.00m,
                PreTaxVal = 0.00m,
                AssAmt = it.TaxableAmount,
                GstRt = it.GstRate,
                CgstAmt = it.CgstAmt,
                SgstAmt = it.SgstAmt,
                IgstAmt = it.IgstAmt,
                CesRt = 0.00m,
                CesAmt = 0.00m,
                CesNonAdvlAmt = 0.00m,
                StateCesRt = 0.00m,
                StateCesAmt = 0.00m,
                StateCesNonAdvlAmt = 0.00m,
                OthChrg = 0.00m,
                TotItemVal = it.TotalAmt
            };
        }).ToList();

        return new
        {
            Version = "1.1",
            TranDtls = new
            {
                TaxSch = "GST",
                SupTyp = isB2B ? "B2B" : "B2C",
                RegRev = "N",
                EcmGstin = (string?)null,
                IgstOnIntra = "N"
            },
            DocDtls = new
            {
                Typ = "INV",
                No = request.VouNo,
                Dt = request.BillDate.ToString("dd/MM/yyyy")
            },
            SellerDtls = new
            {
                Gstin = config.Gstin,
                LglNm = config.LegalName,
                TrdNm = config.TradeName,
                Addr1 = config.Address,
                Loc = config.City,
                Pin = config.Pincode,
                Stcd = config.StateCode
            },
            BuyerDtls = new
            {
                Gstin = buyerGstin,
                LglNm = !string.IsNullOrWhiteSpace(request.BuyerName) ? request.BuyerName : "Customer",
                TrdNm = !string.IsNullOrWhiteSpace(request.BuyerName) ? request.BuyerName : "Customer",
                Pos = buyerStateCode,
                Addr1 = !string.IsNullOrWhiteSpace(request.BuyerAddress) ? request.BuyerAddress : config.Address,
                Loc = !string.IsNullOrWhiteSpace(request.BuyerCity) ? request.BuyerCity : config.City,
                Pin = pinCode,
                Stcd = buyerStateCode
            },
            ItemList = itemsList,
            ValDtls = new
            {
                AssVal = request.TaxableAmount,
                CgstVal = request.CgstAmount,
                SgstVal = request.SgstAmount,
                IgstVal = request.IgstAmount,
                CesVal = 0.00m,
                StCesVal = 0.00m,
                Discount = request.Discount,
                OthChrg = 0.00m,
                RndOffAmt = 0.00m,
                TotInvVal = request.NetAmount
            }
        };
    }

    private static EInvoiceResult GenerateSimulatedDemoEInvoice(EInvoiceRequestDto request, EInvoiceConfig config)
    {
        // Generate simulated 64-character SHA-256 IRN hash for DEMO mode
        var rawData = $"{config.Gstin}|{request.BillDate:yyyy-MM-dd}|INV|{request.VouNo}|{request.NetAmount:F2}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        var irnHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var ackNo = DateTime.Now.ToString("yyyyMMdd") + Random.Shared.Next(100000, 999999);
        var ackDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var qrCode = $"DEMO-QR:{config.Gstin}|{request.BuyerGstin ?? "URP"}|{request.VouNo}|{request.BillDate:dd/MM/yyyy}|{request.NetAmount:F2}|{request.TaxableAmount:F2}|{irnHash}|{ackNo}";

        return new EInvoiceResult
        {
            Success = true,
            Mode = "DEMO",
            Irn = irnHash,
            AckNo = ackNo,
            AckDate = ackDate,
            QrCode = qrCode,
            Message = $"[DEMO MODE] E-Invoice & IRN generated successfully!\n\nIRN: {irnHash}\nAck No: {ackNo}\nAck Date: {ackDate}"
        };
    }

    public static EInvoiceConfig LoadConfig(string mode)
    {
        var cfg = new EInvoiceConfig();
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
                if (doc.RootElement.TryGetProperty("EInvoice", out var einvSec))
                {
                    string targetSec = mode.Equals("LIVE", StringComparison.OrdinalIgnoreCase) ? "Live" : "Demo";
                    if (einvSec.TryGetProperty(targetSec, out var sec))
                    {
                        if (sec.TryGetProperty("TokenEndpoint", out var te) && !string.IsNullOrWhiteSpace(te.GetString())) cfg.TokenEndpoint = te.GetString()!;
                        if (sec.TryGetProperty("GenerateEndpoint", out var ge) && !string.IsNullOrWhiteSpace(ge.GetString())) cfg.GenerateEndpoint = ge.GetString()!;
                        if (sec.TryGetProperty("ClientId", out var ci) && !string.IsNullOrWhiteSpace(ci.GetString())) cfg.ClientId = ci.GetString()!;
                        if (sec.TryGetProperty("ClientSecret", out var cs) && !string.IsNullOrWhiteSpace(cs.GetString())) cfg.ClientSecret = cs.GetString()!;
                        if (sec.TryGetProperty("UserName", out var un) && !string.IsNullOrWhiteSpace(un.GetString())) cfg.UserName = un.GetString()!;
                        if (sec.TryGetProperty("Password", out var pw) && !string.IsNullOrWhiteSpace(pw.GetString())) cfg.Password = pw.GetString()!;
                        if (sec.TryGetProperty("Gstin", out var gs) && !string.IsNullOrWhiteSpace(gs.GetString())) cfg.Gstin = gs.GetString()!;
                        if (sec.TryGetProperty("LegalName", out var ln) && !string.IsNullOrWhiteSpace(ln.GetString())) cfg.LegalName = ln.GetString()!;
                        if (sec.TryGetProperty("TradeName", out var tn) && !string.IsNullOrWhiteSpace(tn.GetString())) cfg.TradeName = tn.GetString()!;
                        if (sec.TryGetProperty("Address", out var ad) && !string.IsNullOrWhiteSpace(ad.GetString())) cfg.Address = ad.GetString()!;
                        if (sec.TryGetProperty("City", out var ct) && !string.IsNullOrWhiteSpace(ct.GetString())) cfg.City = ct.GetString()!;
                        if (sec.TryGetProperty("Pincode", out var pin) && pin.TryGetInt32(out var pVal)) cfg.Pincode = pVal;
                        if (sec.TryGetProperty("StateCode", out var sc) && !string.IsNullOrWhiteSpace(sc.GetString())) cfg.StateCode = sc.GetString()!;
                    }
                }
            }
        }
        catch
        {
            // Fallback defaults
        }
        return cfg;
    }
}
