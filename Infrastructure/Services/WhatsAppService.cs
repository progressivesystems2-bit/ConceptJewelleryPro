using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Services;

public sealed class WhatsAppMessageRequest
{
    public string RecipientPhone { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime BillDate { get; set; } = DateTime.Today;
    public decimal NetAmount { get; set; }
    public decimal Balance { get; set; }
    public string StoreName { get; set; } = "Concept Jewellery";
    public string StorePhone { get; set; } = string.Empty;
    public string? CustomMessage { get; set; }
    public bool AttachPdf { get; set; }
    public string? PdfFilePath { get; set; }
    public string? PdfUrl { get; set; }
    public byte[]? PdfBytes { get; set; }
    public string? PdfFileName { get; set; }
}

public sealed record WhatsAppSendResult(bool Success, string Message, string? ExternalId = null);

public sealed record WhatsAppSyncResult(int SyncedRepliesCount, int SyncedStatusesCount)
{
    public int TotalCount => SyncedRepliesCount + SyncedStatusesCount;
}

public interface IWhatsAppService
{
    Task<WhatsAppSendResult> SendInvoiceMessageAsync(WhatsAppMessageRequest request, CancellationToken cancellationToken = default);
    Task<string> FormatMessageAsync(WhatsAppMessageRequest request, CancellationToken cancellationToken = default);
    Task<int> SyncRepliesAsync(CancellationToken cancellationToken = default);
    Task<string?> FetchMessageStatusAsync(string messageId, CancellationToken cancellationToken = default);
    Task<WhatsAppSyncResult> SyncRepliesAndStatusesAsync(int daysBack = 14, CancellationToken cancellationToken = default);
}

public sealed class WhatsAppService : IWhatsAppService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IWhatsAppBillTrackerRepository? _trackerRepository;

    public WhatsAppService(ISettingsRepository? settingsRepository = null, IWhatsAppBillTrackerRepository? trackerRepository = null)
    {
        _settingsRepository = settingsRepository;
        _trackerRepository = trackerRepository;
    }

    public async Task<string> FormatMessageAsync(WhatsAppMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomMessage))
        {
            return request.CustomMessage;
        }

        string template = "Dear {CustomerName},\nThank you for choosing us! Your invoice #{InvoiceNo} of Rs. {NetAmount} dated {BillDate} has been generated successfully.\nWe appreciate your business!";

        if (_settingsRepository != null)
        {
            try
            {
                var settings = await _settingsRepository.GetAllAsync(cancellationToken);
                if (settings.TryGetValue(AppSetting.WaTemplateKey, out var customTemplate) && !string.IsNullOrWhiteSpace(customTemplate))
                {
                    template = customTemplate;
                }
            }
            catch
            {
                // Fallback to default template
            }
        }

        return template
            .Replace("{CustomerName}", request.CustomerName ?? string.Empty)
            .Replace("{InvoiceNo}", request.InvoiceNo ?? string.Empty)
            .Replace("{BillDate}", request.BillDate.ToString("dd/MM/yyyy"))
            .Replace("{NetAmount}", request.NetAmount.ToString("N2"))
            .Replace("{Balance}", request.Balance.ToString("N2"))
            .Replace("{StoreName}", request.StoreName ?? "Concept Jewellery")
            .Replace("{Phone}", request.RecipientPhone ?? string.Empty);
    }

    public async Task<WhatsAppSendResult> SendInvoiceMessageAsync(WhatsAppMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientPhone))
        {
            return new WhatsAppSendResult(false, "Recipient phone number is missing.");
        }

        var cleanPhone = NormalizePhoneNumber(request.RecipientPhone);
        if (string.IsNullOrWhiteSpace(cleanPhone))
        {
            return new WhatsAppSendResult(false, "Invalid recipient phone number.");
        }

        var messageText = await FormatMessageAsync(request, cancellationToken);

        // Read settings
        string sendMethod = "API";
        string apiKey = string.Empty;
        string businessNo = string.Empty;
        string apiTemplateName = string.Empty;
        string apiLanguage = "en_US";
        string apiParameters = string.Empty;
        bool attachPdf = false;
        string docUrlTemplate = string.Empty;

        if (_settingsRepository != null)
        {
            try
            {
                var settings = await _settingsRepository.GetAllAsync(cancellationToken);
                if (settings.TryGetValue(AppSetting.WaSendMethodKey, out var method) && !string.IsNullOrWhiteSpace(method))
                {
                    sendMethod = method.Trim();
                }
                if (settings.TryGetValue(AppSetting.WaAPIKeyKey, out var key) && !string.IsNullOrWhiteSpace(key))
                {
                    apiKey = key.Trim();
                }
                if (settings.TryGetValue(AppSetting.WaBusinessNoKey, out var bNo) && !string.IsNullOrWhiteSpace(bNo))
                {
                    businessNo = bNo.Trim();
                }
                if (settings.TryGetValue(AppSetting.WaApiTemplateNameKey, out var tName) && !string.IsNullOrWhiteSpace(tName))
                {
                    apiTemplateName = tName.Trim();
                }
                if (settings.TryGetValue(AppSetting.WaApiLanguageKey, out var lang) && !string.IsNullOrWhiteSpace(lang))
                {
                    apiLanguage = lang.Trim();
                }
                if (settings.TryGetValue(AppSetting.WaApiParametersKey, out var tParams) && !string.IsNullOrWhiteSpace(tParams))
                {
                    apiParameters = tParams.Trim();
                }
                if (settings.TryGetValue(AppSetting.WaAttachPdfKey, out var attachPdfStr) && !string.IsNullOrWhiteSpace(attachPdfStr))
                {
                    attachPdf = bool.TryParse(attachPdfStr, out var ap) && ap;
                }
                if (settings.TryGetValue(AppSetting.WaApiDocumentUrlKey, out var docUrl) && !string.IsNullOrWhiteSpace(docUrl))
                {
                    docUrlTemplate = docUrl.Trim();
                }
            }
            catch (Exception ex)
            {
                return new WhatsAppSendResult(false, $"Error reading WhatsApp settings: {ex.Message}");
            }
        }

        // Only attach PDF if WaAttachPdf is enabled in settings OR explicitly requested
        bool shouldAttachPdf = attachPdf || request.AttachPdf;

        if (sendMethod.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return new WhatsAppSendResult(false, "WhatsApp messaging is set to Disabled in General Settings.");
        }

        if (sendMethod.Equals("WhatsApp Web", StringComparison.OrdinalIgnoreCase))
        {
            return LaunchWhatsAppWeb(cleanPhone, messageText);
        }

        if (sendMethod.Equals("WhatsApp Desktop", StringComparison.OrdinalIgnoreCase))
        {
            return LaunchWhatsAppDesktop(cleanPhone, messageText);
        }

        // Default / SendMethod == "API"
        return await SendViaApiAsync(cleanPhone, messageText, apiKey, businessNo, apiTemplateName, apiLanguage, apiParameters, shouldAttachPdf, docUrlTemplate, request, _trackerRepository, cancellationToken);
    }

    private static async Task<WhatsAppSendResult> SendViaApiAsync(
        string phone,
        string messageText,
        string apiKey,
        string businessNo,
        string apiTemplateName,
        string apiLanguage,
        string apiParameters,
        bool attachPdf,
        string docUrlTemplate,
        WhatsAppMessageRequest request,
        IWhatsAppBillTrackerRepository? trackerRepository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new WhatsAppSendResult(false, "WhatsApp API Key is not configured. Please set it in General Settings.");
        }

        try
        {
            HttpRequestMessage httpRequest;

            // Resolve body parameter values for template
            var paramTokens = !string.IsNullOrWhiteSpace(apiParameters)
                ? apiParameters.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();

            var bodyParameters = new List<object>();
            foreach (var token in paramTokens)
            {
                var textVal = ResolveToken(token, request);
                bodyParameters.Add(new { type = "text", text = textVal });
            }

            if (apiKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || apiKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Custom Gateway URL endpoint
                string? pdfUrl = !string.IsNullOrWhiteSpace(request.PdfUrl) ? request.PdfUrl : (!string.IsNullOrWhiteSpace(docUrlTemplate) ? ResolveToken(docUrlTemplate, request) : null);
                var payload = new
                {
                    phone,
                    templateName = apiTemplateName,
                    language = !string.IsNullOrWhiteSpace(apiLanguage) ? apiLanguage : "en_US",
                    message = messageText,
                    customerName = request.CustomerName,
                    invoiceNo = request.InvoiceNo,
                    billDate = request.BillDate.ToString("dd/MM/yyyy"),
                    netAmount = request.NetAmount,
                    balance = request.Balance,
                    storeName = request.StoreName,
                    businessNo,
                    attachPdf,
                    pdfFileName = !string.IsNullOrWhiteSpace(request.PdfFileName) ? request.PdfFileName : $"Invoice_{request.InvoiceNo}.pdf",
                    pdfFilePath = request.PdfFilePath,
                    pdfUrl,
                    parameters = bodyParameters
                };

                var json = JsonSerializer.Serialize(payload);
                httpRequest = new HttpRequestMessage(HttpMethod.Post, apiKey)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            else
            {
                // Standard WhatsApp Cloud API Official Template Endpoint / Gateway
                if (string.IsNullOrWhiteSpace(apiTemplateName))
                {
                    return new WhatsAppSendResult(false, "WhatsApp API Template Name is not configured. Official WhatsApp Cloud API requires a pre-approved template name. Please configure it in General Settings.");
                }

                var cleanBusinessId = Regex.Replace(businessNo ?? string.Empty, @"\D", "");
                var endpoint = !string.IsNullOrWhiteSpace(cleanBusinessId)
                    ? $"https://graph.facebook.com/v18.0/{cleanBusinessId}/messages"
                    : "https://graph.facebook.com/v18.0/me/messages";

                var componentsList = new List<object>();

                // Handle Document Header ONLY if attachPdf is explicitly enabled
                if (attachPdf)
                {
                    string filename = !string.IsNullOrWhiteSpace(request.PdfFileName) ? request.PdfFileName : $"{request.InvoiceNo}.pdf";

                    // 1. If explicit URL provided or URL pattern configured
                    string? resolvedDocUrl = !string.IsNullOrWhiteSpace(request.PdfUrl) ? request.PdfUrl : null;
                    if (string.IsNullOrWhiteSpace(resolvedDocUrl) && !string.IsNullOrWhiteSpace(docUrlTemplate))
                    {
                        resolvedDocUrl = ResolveToken(docUrlTemplate, request);
                    }

                    if (!string.IsNullOrWhiteSpace(resolvedDocUrl))
                    {
                        componentsList.Add(new
                        {
                            type = "header",
                            parameters = new object[]
                            {
                                new
                                {
                                    type = "document",
                                    document = new
                                    {
                                        link = resolvedDocUrl,
                                        filename
                                    }
                                }
                            }
                        });
                    }
                    else if ((request.PdfBytes != null && request.PdfBytes.Length > 0) || (!string.IsNullOrWhiteSpace(request.PdfFilePath) && File.Exists(request.PdfFilePath)))
                    {
                        // 2. Upload file via Meta Media API
                        var mediaId = await UploadMediaAsync(cleanBusinessId, apiKey, request.PdfFilePath, request.PdfBytes, filename, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(mediaId))
                        {
                            componentsList.Add(new
                            {
                                type = "header",
                                parameters = new object[]
                                {
                                    new
                                    {
                                        type = "document",
                                        document = new
                                        {
                                            id = mediaId,
                                            filename
                                        }
                                    }
                                }
                            });
                        }
                        else
                        {
                            return new WhatsAppSendResult(false, "Failed to upload the invoice PDF to WhatsApp Media API. Please check your WhatsApp Business Phone ID and API Token permissions.");
                        }
                    }
                    else
                    {
                        return new WhatsAppSendResult(false, "Template requires a Document Header (WaAttachPdf is enabled), but no Document Link Pattern (WaApiDocumentUrl) is set and no invoice PDF was found locally to upload. Please configure the Document Link Pattern in General Settings (e.g. http://taurastechnology.com/files/{InvoiceNo}.pdf).");
                    }
                }

                if (bodyParameters.Count > 0)
                {
                    componentsList.Add(new
                    {
                        type = "body",
                        parameters = bodyParameters
                    });
                }

                object templateObj;
                if (componentsList.Count > 0)
                {
                    templateObj = new
                    {
                        name = apiTemplateName.Trim(),
                        language = new
                        {
                            code = !string.IsNullOrWhiteSpace(apiLanguage) ? apiLanguage.Trim() : "en_US"
                        },
                        components = componentsList.ToArray()
                    };
                }
                else
                {
                    templateObj = new
                    {
                        name = apiTemplateName.Trim(),
                        language = new
                        {
                            code = !string.IsNullOrWhiteSpace(apiLanguage) ? apiLanguage.Trim() : "en_US"
                        }
                    };
                }

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = phone,
                    type = "template",
                    template = templateObj
                };

                var json = JsonSerializer.Serialize(payload);
                httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                string? messageId = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
                    {
                        var first = messages[0];
                        if (first.TryGetProperty("id", out var idProp))
                        {
                            messageId = idProp.GetString();
                        }
                    }
                }
                catch
                {
                    // Response body wasn't json with id
                }

                if (trackerRepository != null && !string.IsNullOrWhiteSpace(messageId) && !string.IsNullOrWhiteSpace(request.InvoiceNo))
                {
                    try
                    {
                        await trackerRepository.AddAsync(new WhatsAppBillTracker
                        {
                            Wamid = messageId,
                            InvoiceNo = request.InvoiceNo.Trim(),
                            MobileNumber = phone,
                            DeliveryStatus = "Sent",
                            Rating = "Sent",
                            SentAt = DateTime.Now
                        }, cancellationToken);
                    }
                    catch
                    {
                        // Non-fatal if tracker save fails
                    }
                }

                return new WhatsAppSendResult(true, "WhatsApp message sent successfully via API.", messageId);
            }
            else
            {
                string errDetail = response.ReasonPhrase ?? "HTTP Error";
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("error", out var err))
                    {
                        var msg = err.TryGetProperty("message", out var m) ? m.GetString() : null;
                        string? details = null;
                        if (err.TryGetProperty("error_data", out var ed) && ed.TryGetProperty("details", out var d))
                        {
                            details = d.GetString();
                        }

                        if (!string.IsNullOrWhiteSpace(msg) && !string.IsNullOrWhiteSpace(details))
                        {
                            errDetail = $"{msg} - {details}";
                        }
                        else if (!string.IsNullOrWhiteSpace(msg))
                        {
                            errDetail = msg;
                        }
                    }
                }
                catch
                {
                    if (!string.IsNullOrWhiteSpace(responseBody) && responseBody.Length < 200)
                    {
                        errDetail = responseBody;
                    }
                }

                return new WhatsAppSendResult(false, $"WhatsApp API failed ({response.StatusCode}): {errDetail}");
            }
        }
        catch (Exception ex)
        {
            return new WhatsAppSendResult(false, $"WhatsApp API connection error: {ex.Message}");
        }
    }

    private static WhatsAppSendResult LaunchWhatsAppWeb(string phone, string messageText)
    {
        try
        {
            var url = $"https://web.whatsapp.com/send?phone={phone}&text={Uri.EscapeDataString(messageText)}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return new WhatsAppSendResult(true, "WhatsApp Web opened in browser.");
        }
        catch (Exception ex)
        {
            return new WhatsAppSendResult(false, $"Failed to open WhatsApp Web: {ex.Message}");
        }
    }

    private static WhatsAppSendResult LaunchWhatsAppDesktop(string phone, string messageText)
    {
        try
        {
            var url = $"whatsapp://send?phone={phone}&text={Uri.EscapeDataString(messageText)}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return new WhatsAppSendResult(true, "WhatsApp Desktop application launched.");
        }
        catch (Exception ex)
        {
            return new WhatsAppSendResult(false, $"Failed to launch WhatsApp Desktop: {ex.Message}");
        }
    }

    private static string NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        // Remove spaces, hyphens, parentheses, plus
        var digitsOnly = Regex.Replace(phone, @"\D", "");

        // If 10-digit Indian number, prepend 91
        if (digitsOnly.Length == 10)
        {
            return "91" + digitsOnly;
        }

        // If 11-digit starting with 0, replace 0 with 91
        if (digitsOnly.Length == 11 && digitsOnly.StartsWith("0"))
        {
            return "91" + digitsOnly.Substring(1);
        }

        return digitsOnly;
    }

    private static string ResolveToken(string token, WhatsAppMessageRequest request)
    {
        var trimmed = token.Trim();
        return trimmed switch
        {
            "{CustomerName}" or "CustomerName" => request.CustomerName ?? string.Empty,
            "{InvoiceNo}" or "InvoiceNo" => request.InvoiceNo ?? string.Empty,
            "{BillDate}" or "BillDate" => request.BillDate.ToString("dd/MM/yyyy"),
            "{NetAmount}" or "NetAmount" => request.NetAmount.ToString("N2"),
            "{Balance}" or "Balance" => request.Balance.ToString("N2"),
            "{StoreName}" or "StoreName" => request.StoreName ?? "Concept Jewellery",
            "{Phone}" or "Phone" or "{RecipientPhone}" or "RecipientPhone" => request.RecipientPhone ?? string.Empty,
            "{PdfFileName}" or "PdfFileName" => !string.IsNullOrWhiteSpace(request.PdfFileName) ? request.PdfFileName : $"{request.InvoiceNo}.pdf",
            _ => trimmed.Replace("{CustomerName}", request.CustomerName ?? string.Empty)
                        .Replace("{InvoiceNo}", request.InvoiceNo ?? string.Empty)
                        .Replace("{BillDate}", request.BillDate.ToString("dd/MM/yyyy"))
                        .Replace("{NetAmount}", request.NetAmount.ToString("N2"))
                        .Replace("{Balance}", request.Balance.ToString("N2"))
                        .Replace("{StoreName}", request.StoreName ?? "Concept Jewellery")
                        .Replace("{Phone}", request.RecipientPhone ?? string.Empty)
                        .Replace("{PdfFileName}", !string.IsNullOrWhiteSpace(request.PdfFileName) ? request.PdfFileName : $"{request.InvoiceNo}.pdf")
        };
    }

    private static async Task<string?> UploadMediaAsync(
        string businessId,
        string apiKey,
        string? filePath,
        byte[]? fileBytes,
        string filename,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[] bytes;
            if (fileBytes != null && fileBytes.Length > 0)
            {
                bytes = fileBytes;
            }
            else if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            }
            else
            {
                return null;
            }

            var endpoint = !string.IsNullOrWhiteSpace(businessId)
                ? $"https://graph.facebook.com/v18.0/{businessId}/media"
                : "https://graph.facebook.com/v18.0/me/media";

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("whatsapp"), "messaging_product");
            form.Add(new StringContent("application/pdf"), "type");

            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fileContent, "file", filename);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = form
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await HttpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    return idProp.GetString();
                }
            }
        }
        catch
        {
            // Non-fatal, fallback
        }
        return null;
    }

    public async Task<string?> FetchMessageStatusAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        try
        {
            var cleanId = messageId.Trim();
            var url = $"https://taurastechnology.com/wa/wagetStatusnew.asp?messageID={Uri.EscapeDataString(cleanId)}";

            using var response = await HttpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var trimmed = text.Trim();

            // Check if response is an error or HTML page or Not Found
            if (trimmed.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Server Error", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Not Found", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Length > 50)
            {
                return null;
            }

            // Normalize status text (e.g. "sent" -> "Sent", "delivered" -> "Delivered", "read" -> "Read", "failed" -> "Failed")
            if (string.Equals(trimmed, "read", StringComparison.OrdinalIgnoreCase)) return "Read";
            if (string.Equals(trimmed, "delivered", StringComparison.OrdinalIgnoreCase)) return "Delivered";
            if (string.Equals(trimmed, "sent", StringComparison.OrdinalIgnoreCase)) return "Sent";
            if (string.Equals(trimmed, "failed", StringComparison.OrdinalIgnoreCase)) return "Failed";

            return char.ToUpper(trimmed[0]) + trimmed.Substring(1);
        }
        catch
        {
            return null;
        }
    }

    public async Task<WhatsAppSyncResult> SyncRepliesAndStatusesAsync(int daysBack = 14, CancellationToken cancellationToken = default)
    {
        if (_trackerRepository == null)
        {
            throw new InvalidOperationException("WhatsApp tracker repository is not configured.");
        }

        int syncedReplies = 0;
        int syncedStatuses = 0;

        // 1. Sync Customer Replies from GetERPReply.asp
        string? phoneNumberId = null;
        if (_settingsRepository != null)
        {
            try
            {
                var settings = await _settingsRepository.GetAllAsync(cancellationToken);
                if (settings.TryGetValue(AppSetting.WaBusinessNoKey, out var bNo) && !string.IsNullOrWhiteSpace(bNo))
                {
                    phoneNumberId = Regex.Replace(bNo, @"\D", "");
                }
            }
            catch
            {
                // Non-fatal if settings read fails
            }
        }

        if (!string.IsNullOrWhiteSpace(phoneNumberId))
        {
            try
            {
                var url = $"https://taurastechnology.com/wa/GetERPReply.asp?PhoneId={Uri.EscapeDataString(phoneNumberId)}";
                using var response = await HttpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(json) && json.Trim() != "[]" && json.Trim() != "null")
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in doc.RootElement.EnumerateArray())
                            {
                                string? wamid = null;
                                if (item.TryGetProperty("MessageId", out var idProp) || item.TryGetProperty("messageId", out idProp))
                                {
                                    wamid = idProp.GetString()?.Trim();
                                }

                                if (string.IsNullOrWhiteSpace(wamid)) continue;

                                string replyText = string.Empty;
                                if (item.TryGetProperty("MessageText", out var textProp) || item.TryGetProperty("messageText", out textProp))
                                {
                                    replyText = textProp.GetString()?.Trim() ?? string.Empty;
                                }

                                await _trackerRepository.UpdateFeedbackAsync(wamid, replyText, replyText, cancellationToken);
                                syncedReplies++;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Non-fatal if reply endpoint encounters an issue
            }
        }

        // 2. Sync Send / Delivery Statuses from wagetStatusnew.asp
        try
        {
            var pendingTrackers = await _trackerRepository.GetTrackersForStatusSyncAsync(daysBack, cancellationToken);
            foreach (var tracker in pendingTrackers)
            {
                if (string.IsNullOrWhiteSpace(tracker.Wamid)) continue;

                var currentStatus = tracker.DeliveryStatus ?? tracker.Rating;
                var newStatus = await FetchMessageStatusAsync(tracker.Wamid, cancellationToken);

                if (!string.IsNullOrWhiteSpace(newStatus) && !string.Equals(newStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
                {
                    await _trackerRepository.UpdateDeliveryStatusAsync(tracker.Wamid, newStatus, cancellationToken);
                    syncedStatuses++;
                }
            }
        }
        catch
        {
            // Non-fatal if tracker status sync fails
        }

        return new WhatsAppSyncResult(syncedReplies, syncedStatuses);
    }

    public async Task<int> SyncRepliesAsync(CancellationToken cancellationToken = default)
    {
        var result = await SyncRepliesAndStatusesAsync(14, cancellationToken);
        return result.TotalCount;
    }
}
