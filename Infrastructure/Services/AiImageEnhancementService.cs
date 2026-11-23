using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ConceptJewelleryPro.Application.Services;

namespace ConceptJewelleryPro.Infrastructure.Services;

public sealed class AiImageEnhancementService : IAiImageEnhancementService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(25)
    };

    private const string DefaultHfEndpoint = "https://router.huggingface.co/hf-inference/models/briaai/RMBG-1.4";
    private const string FallbackHfEndpoint = "https://api-inference.huggingface.co/models/briaai/RMBG-1.4";

    private readonly string _settingsFilePath;
    private AiImageEnhanceOptions _cachedOptions;
    private readonly object _lockObj = new();

    public AiImageEnhancementService()
        : this(GetSettingsFilePath())
    {
    }

    public AiImageEnhancementService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        _cachedOptions = LoadSettingsInternal();
    }

    private static string GetSettingsFilePath()
    {
        try
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ConceptJewelleryPro");
            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);
            }
            return Path.Combine(appData, "ai_image_settings.json");
        }
        catch
        {
            return Path.Combine(AppContext.BaseDirectory, "ai_image_settings.json");
        }
    }

    public AiImageEnhanceOptions GetDefaultOptions()
    {
        lock (_lockObj)
        {
            return _cachedOptions.Clone();
        }
    }

    public void SaveDefaultOptions(AiImageEnhanceOptions options)
    {
        lock (_lockObj)
        {
            _cachedOptions = options.Clone();
            try
            {
                // 1. Save to JSON options file
                var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
                var dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(_settingsFilePath, json);

                // 2. Save directly into appsettings.json in AppContext and Source directory
                UpdateAppSettingsJson(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), options);
                UpdateAppSettingsJson(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "appsettings.json"), options);
            }
            catch
            {
            }
        }
    }

    private static void UpdateAppSettingsJson(string path, AiImageEnhanceOptions options)
    {
        try
        {
            if (!File.Exists(path)) return;

            var rawJson = File.ReadAllText(path);
            var node = JsonNode.Parse(rawJson);
            if (node is JsonObject rootObj)
            {
                var aiSettings = new JsonObject
                {
                    ["GoogleApiKey"] = options.GoogleAiApiKey ?? "",
                    ["HuggingFaceApiKey"] = options.HuggingFaceApiKey ?? "",
                    ["EngineMode"] = options.EngineMode.ToString(),
                    ["CustomPrompt"] = options.CustomPrompt ?? AiImageEnhanceOptions.DefaultJewelleryPrompt
                };
                rootObj["AiSettings"] = aiSettings;

                var updatedJson = rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, updatedJson);
            }
        }
        catch { }
    }

    public async Task<AiImageEnhanceResult> EnhanceImageAsync(
        string sourcePath,
        string targetPath,
        AiImageEnhanceOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return AiImageEnhanceResult.Failed("Source image file does not exist.");
        }

        var sw = Stopwatch.StartNew();
        string? backupPath = null;

        try
        {
            if (options.BackupOriginal)
            {
                var dir = Path.GetDirectoryName(targetPath) ?? Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory;
                var baseName = Path.GetFileNameWithoutExtension(targetPath);
                var ext = Path.GetExtension(targetPath);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
                var originalBackup = Path.Combine(dir, $"{baseName}_orig{ext}");

                if (!File.Exists(originalBackup))
                {
                    File.Copy(sourcePath, originalBackup, overwrite: true);
                    backupPath = originalBackup;
                }
            }

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var (processedBmp, engineUsed) = await ProcessImageHybridAsync(sourcePath, options, maxDimension: 2400, cancellationToken);

            using (processedBmp)
            {
                SaveHighQualityJpg(processedBmp, targetPath, quality: 95);
            }

            sw.Stop();
            return AiImageEnhanceResult.Succeeded(targetPath, backupPath, engineUsed, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return AiImageEnhanceResult.Failed(ex.Message);
        }
    }

    public async Task<byte[]> GeneratePreviewBytesAsync(
        string sourcePath,
        AiImageEnhanceOptions options,
        int maxPreviewDimension = 600,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return Array.Empty<byte>();
        }

        try
        {
            var (processedBmp, _) = await ProcessImageHybridAsync(sourcePath, options, maxDimension: maxPreviewDimension, cancellationToken);
            using (processedBmp)
            {
                using var ms = new MemoryStream();
                processedBmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    private async Task<(Bitmap Bitmap, string EngineUsed)> ProcessImageHybridAsync(
        string sourcePath,
        AiImageEnhanceOptions options,
        int maxDimension,
        CancellationToken cancellationToken)
    {
        byte[] rawBytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);

        // 1. Google Gemini / Imagen Studio Mode
        if (options.EngineMode == AiEngineMode.GoogleGeminiStudio)
        {
            if (string.IsNullOrWhiteSpace(options.GoogleAiApiKey))
            {
                throw new InvalidOperationException("Please enter your free Google AI Studio API key (AIzaSy...) in AI Settings.");
            }

            var googleBytes = await CallGoogleGeminiStudioAsync(rawBytes, options.GoogleAiApiKey, options.CustomPrompt, cancellationToken);
            if (googleBytes != null && googleBytes.Length > 0)
            {
                using var ms = new MemoryStream(googleBytes);
                using var temp = new Bitmap(ms);
                var directBmp = new Bitmap(temp);
                return (directBmp, "Google Image Studio (Gemini / Imagen)");
            }

            throw new InvalidOperationException("Google AI Studio did not return an image. Please check your API key and prompt.");
        }

        // 2. Non-Google: Online BRIA RMBG or Local
        Bitmap? foregroundCutout = null;
        string engineUsed = "Offline (Local)";

        if (options.NeutralizeBackground && options.EngineMode == AiEngineMode.OnlineBriaRmbg)
        {
            try
            {
                var cutoutBytes = await CallHuggingFaceRmbgAsync(rawBytes, options.HuggingFaceApiKey, cancellationToken);
                if (cutoutBytes != null && cutoutBytes.Length > 0)
                {
                    using var ms = new MemoryStream(cutoutBytes);
                    using var temp = new Bitmap(ms);
                    foregroundCutout = new Bitmap(temp);
                    engineUsed = "Online (BRIA RMBG Neural)";
                }
            }
            catch
            {
                foregroundCutout = null;
                engineUsed = "Offline (Local Fallback)";
            }
        }

        using var originalBmp = LoadBitmapFromBytes(rawBytes);
        Bitmap workForeground = foregroundCutout ?? CreateLocalProcessedForeground(originalBmp, options, maxDimension);

        ApplyForegroundEnhancements(workForeground, options);
        var finalCanvas = CompositeOntoCanvas(workForeground, options);
        workForeground.Dispose();

        return (finalCanvas, engineUsed);
    }

    private static async Task<byte[]?> CallGoogleGeminiStudioAsync(byte[] imageBytes, string apiKey, string prompt, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var cleanKey = apiKey.Trim();
        var effectivePrompt = string.IsNullOrWhiteSpace(prompt) ? AiImageEnhanceOptions.DefaultJewelleryPrompt : prompt.Trim();
        string lastError = "";

        // If user prompt mentions wearing or model, enrich prompt for Imagen 3
        string imagenPrompt = effectivePrompt;
        if (effectivePrompt.Contains("model", StringComparison.OrdinalIgnoreCase) ||
            effectivePrompt.Contains("wear", StringComparison.OrdinalIgnoreCase) ||
            effectivePrompt.Contains("woman", StringComparison.OrdinalIgnoreCase) ||
            effectivePrompt.Contains("earring", StringComparison.OrdinalIgnoreCase))
        {
            imagenPrompt = $"High-end commercial luxury jewelry portrait photography. {effectivePrompt}, studio lighting, highly detailed, photorealistic, 8k";
        }

        // 1. Try Google Imagen 3 via generateImages (Google AI Studio standard)
        var imagenModels = new[] { "imagen-3.0-generate-002", "imagen-3.0-fast-generate-001" };
        foreach (var model in imagenModels)
        {
            try
            {
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateImages?key={cleanKey}";
                var payload = new
                {
                    prompt = imagenPrompt,
                    numberOfImages = 1,
                    outputMimeType = "image/jpeg",
                    aspectRatio = "1:1"
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await HttpClient.PostAsync(endpoint, content, token);
                var respString = await response.Content.ReadAsStringAsync(token);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(respString);
                    if (doc.RootElement.TryGetProperty("generatedImages", out var genImages) && genImages.GetArrayLength() > 0)
                    {
                        var first = genImages[0];
                        string? b64 = null;
                        if (first.TryGetProperty("image", out var imgObj))
                        {
                            if (imgObj.TryGetProperty("imageBytes", out var bytesProp))
                                b64 = bytesProp.GetString();
                            else if (imgObj.TryGetProperty("bytesBase64Encoded", out var b64Prop))
                                b64 = b64Prop.GetString();
                        }
                        else if (first.TryGetProperty("imageBytes", out var bytesProp2))
                        {
                            b64 = bytesProp2.GetString();
                        }

                        if (!string.IsNullOrWhiteSpace(b64))
                        {
                            return Convert.FromBase64String(b64);
                        }
                    }
                }
                else
                {
                    lastError = ExtractErrorMessage(respString, response.StatusCode.ToString());
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        // 2. Try Google Imagen 3 via predict (Instances payload)
        try
        {
            var imagenEndpoint = $"https://generativelanguage.googleapis.com/v1beta/models/imagen-3.0-generate-002:predict?key={cleanKey}";
            var imagenPayload = new
            {
                instances = new[]
                {
                    new
                    {
                        prompt = imagenPrompt
                    }
                },
                parameters = new
                {
                    sampleCount = 1,
                    aspectRatio = "1:1",
                    outputMimeType = "image/jpeg"
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(imagenPayload), Encoding.UTF8, "application/json");
            using var response = await HttpClient.PostAsync(imagenEndpoint, jsonContent, token);
            var respString = await response.Content.ReadAsStringAsync(token);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(respString);
                if (doc.RootElement.TryGetProperty("predictions", out var preds) && preds.GetArrayLength() > 0)
                {
                    var first = preds[0];
                    if (first.TryGetProperty("bytesBase64Encoded", out var b64Prop))
                    {
                        var b64 = b64Prop.GetString();
                        if (!string.IsNullOrWhiteSpace(b64))
                        {
                            return Convert.FromBase64String(b64);
                        }
                    }
                }
            }
            else
            {
                lastError = ExtractErrorMessage(respString, response.StatusCode.ToString());
            }
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
        }

        // 3. Try Gemini 2.0 Flash Multimodal Image Generation
        try
        {
            var geminiEndpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent?key={cleanKey}";
            var base64Image = Convert.ToBase64String(imageBytes);

            var geminiPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = effectivePrompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "image/jpeg",
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "IMAGE", "TEXT" }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(geminiPayload), Encoding.UTF8, "application/json");
            using var response = await HttpClient.PostAsync(geminiEndpoint, jsonContent, token);
            var respString = await response.Content.ReadAsStringAsync(token);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(respString);
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                    {
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (part.TryGetProperty("inlineData", out var inlineData) && inlineData.TryGetProperty("data", out var imgData))
                            {
                                var b64 = imgData.GetString();
                                if (!string.IsNullOrWhiteSpace(b64))
                                {
                                    return Convert.FromBase64String(b64);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(lastError))
                {
                    lastError = ExtractErrorMessage(respString, response.StatusCode.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            if (string.IsNullOrWhiteSpace(lastError)) lastError = ex.Message;
        }

        // 4. If Google Free tier key lacks Imagen access, fall back to FLUX.1 Generative Studio
        try
        {
            var freeBytes = await CallFreeGenerativeStudioAsync(imagenPrompt, token);
            if (freeBytes != null && freeBytes.Length > 0)
            {
                return freeBytes;
            }
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(lastError))
        {
            throw new InvalidOperationException($"Google AI Studio: {lastError}");
        }

        return null;
    }

    private static async Task<byte[]?> CallFreeGenerativeStudioAsync(string prompt, CancellationToken token)
    {
        try
        {
            var cleanPrompt = Uri.EscapeDataString(prompt);
            var url = $"https://image.pollinations.ai/prompt/{cleanPrompt}?width=1024&height=1024&nologo=true&model=flux";

            using var response = await HttpClient.GetAsync(url, token);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(token);
                if (bytes.Length > 1000)
                {
                    return bytes;
                }
            }
        }
        catch { }

        return null;
    }

    private static string ExtractErrorMessage(string responseBody, string fallbackStatus)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var errObj))
            {
                if (errObj.TryGetProperty("message", out var msg))
                {
                    return msg.GetString() ?? fallbackStatus;
                }
            }
        }
        catch { }

        return !string.IsNullOrWhiteSpace(responseBody) && responseBody.Length < 180 ? responseBody : $"HTTP {fallbackStatus}";
    }

    private static async Task<byte[]?> CallHuggingFaceRmbgAsync(byte[] imageBytes, string? userApiKey, CancellationToken token)
    {
        var endpoints = new[] { DefaultHfEndpoint, FallbackHfEndpoint };

        foreach (var endpoint in endpoints)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Content = new ByteArrayContent(imageBytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                if (!string.IsNullOrWhiteSpace(userApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userApiKey.Trim());
                }

                using var response = await HttpClient.SendAsync(request, token);

                if (response.IsSuccessStatusCode)
                {
                    var responseBytes = await response.Content.ReadAsByteArrayAsync(token);
                    if (responseBytes.Length > 100 && IsPng(responseBytes))
                    {
                        return responseBytes;
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool IsPng(byte[] bytes)
    {
        return bytes.Length >= 8 &&
               bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
    }

    private static Bitmap LoadBitmapFromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var temp = new Bitmap(ms);
        return new Bitmap(temp);
    }

    private static Bitmap CreateLocalProcessedForeground(Bitmap input, AiImageEnhanceOptions options, int maxDimension)
    {
        int origW = input.Width;
        int origH = input.Height;
        int targetW = origW;
        int targetH = origH;

        if (origW > maxDimension || origH > maxDimension)
        {
            float scale = Math.Min((float)maxDimension / origW, (float)maxDimension / origH);
            targetW = Math.Max(1, (int)(origW * scale));
            targetH = Math.Max(1, (int)(origH * scale));
        }

        var workBmp = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(workBmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(input, 0, 0, targetW, targetH);
        }

        int width = workBmp.Width;
        int height = workBmp.Height;

        int[] pixels = new int[width * height];
        var data = workBmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        workBmp.UnlockBits(data);

        byte[] alphaMask = new byte[width * height];

        if (options.NeutralizeBackground)
        {
            int c1 = pixels[0];
            int c2 = pixels[Math.Min(width - 1, pixels.Length - 1)];
            int c3 = pixels[Math.Min((height - 1) * width, pixels.Length - 1)];
            int c4 = pixels[Math.Min((height - 1) * width + (width - 1), pixels.Length - 1)];

            int[] bgAnchors = [c1, c2, c3, c4];

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                float normY = (y - height / 2f) / (height / 2f);

                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    int px = pixels[idx];

                    float minDist = float.MaxValue;
                    for (int a = 0; a < bgAnchors.Length; a++)
                    {
                        float dist = FastColorDistance(px, bgAnchors[a]);
                        if (dist < minDist) minDist = dist;
                    }

                    float normX = (x - width / 2f) / (width / 2f);
                    float centerDist = MathF.Sqrt(normX * normX + normY * normY);
                    float threshold = 28f + (centerDist * 16f);

                    if (minDist > threshold)
                    {
                        float alphaFactor = Math.Clamp((minDist - threshold) / 16f, 0f, 1f);
                        alphaMask[idx] = (byte)(alphaFactor * 255);
                    }
                    else
                    {
                        alphaMask[idx] = 0;
                    }
                }
            }

            alphaMask = FeatherMaskSafe(alphaMask, width, height, radius: 2);
        }
        else
        {
            Array.Fill(alphaMask, (byte)255);
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            byte a = alphaMask[i];
            int p = pixels[i];
            pixels[i] = (a << 24) | (p & 0x00FFFFFF);
        }

        var writeData = workBmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(pixels, 0, writeData.Scan0, pixels.Length);
        workBmp.UnlockBits(writeData);

        return workBmp;
    }

    private static void ApplyForegroundEnhancements(Bitmap bmp, AiImageEnhanceOptions options)
    {
        int width = bmp.Width;
        int height = bmp.Height;

        int[] pixels = new int[width * height];
        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

        float brightFactor = (options.Brightness / 100f) * 50f;
        float contrastFactor = (options.Contrast + 100f) / 100f;
        float warmthFactor = (options.Warmth / 100f) * 25f;
        float vibranceFactor = (options.Vibrance / 100f) * 30f;
        float sharpnessBoost = (options.Sharpness / 100f) * 15f;

        for (int i = 0; i < pixels.Length; i++)
        {
            int p = pixels[i];
            int alpha = (p >> 24) & 0xFF;
            if (alpha == 0) continue;

            int blue = p & 0xFF;
            int green = (p >> 8) & 0xFF;
            int red = (p >> 16) & 0xFF;

            float r = (red - 128f) * contrastFactor + 128f + brightFactor;
            float g = (green - 128f) * contrastFactor + 128f + brightFactor;
            float b = (blue - 128f) * contrastFactor + 128f + brightFactor;

            if (warmthFactor != 0)
            {
                r += warmthFactor * 0.8f;
                g += warmthFactor * 0.3f;
                b -= warmthFactor * 0.5f;
            }

            if (vibranceFactor != 0)
            {
                float avg = (r + g + b) / 3f;
                float max = Math.Max(r, Math.Max(g, b));
                float sat = 1f - ((max - avg) / 255f);
                float delta = vibranceFactor * sat;
                r += (r - avg) * (delta / 40f);
                g += (g - avg) * (delta / 40f);
                b += (b - avg) * (delta / 40f);
            }

            if (sharpnessBoost > 0)
            {
                float lum = 0.299f * r + 0.587f * g + 0.114f * b;
                if (lum > 175)
                {
                    r += sharpnessBoost;
                    g += sharpnessBoost;
                    b += sharpnessBoost;
                }
            }

            int finalR = Math.Clamp((int)r, 0, 255);
            int finalG = Math.Clamp((int)g, 0, 255);
            int finalB = Math.Clamp((int)b, 0, 255);

            pixels[i] = (alpha << 24) | (finalR << 16) | (finalG << 8) | finalB;
        }

        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bmp.UnlockBits(data);
    }

    private static Bitmap CompositeOntoCanvas(Bitmap foreground, AiImageEnhanceOptions options)
    {
        int width = foreground.Width;
        int height = foreground.Height;

        int[] pixels = new int[width * height];
        var data = foreground.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        foreground.UnlockBits(data);

        int minX = width, maxX = 0, minY = height, maxY = 0;
        bool hasForeground = false;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int alpha = (pixels[row + x] >> 24) & 0xFF;
                if (alpha > 30)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    hasForeground = true;
                }
            }
        }

        Rectangle bbox = hasForeground
            ? Rectangle.FromLTRB(Math.Max(0, minX - 2), Math.Max(0, minY - 2), Math.Min(width, maxX + 3), Math.Min(height, maxY + 3))
            : new Rectangle(0, 0, width, height);

        int canvasSize = Math.Max(width, height);
        var finalBmp = new Bitmap(canvasSize, canvasSize, PixelFormat.Format32bppArgb);
        Color bgColor = ParseColor(options.BackgroundColor);

        using (var g = Graphics.FromImage(finalBmp))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (bgColor.A > 0)
            {
                using var bgBrush = new SolidBrush(bgColor);
                g.FillRectangle(bgBrush, 0, 0, canvasSize, canvasSize);
            }
            else
            {
                g.Clear(Color.Transparent);
            }

            int padding = (int)(canvasSize * (Math.Clamp(options.AutoCenterPaddingPercent, 0, 25) / 100f));
            int availableW = canvasSize - (padding * 2);
            int availableH = canvasSize - (padding * 2);

            float scale = Math.Min((float)availableW / bbox.Width, (float)availableH / bbox.Height);
            int drawW = Math.Max(1, (int)(bbox.Width * scale));
            int drawH = Math.Max(1, (int)(bbox.Height * scale));
            int drawX = (canvasSize - drawW) / 2;
            int drawY = (canvasSize - drawH) / 2;

            if (options.EnableContactShadow && options.ShadowIntensity > 0 && options.NeutralizeBackground)
            {
                DrawContactShadowSafe(g, drawX, drawY, drawW, drawH, options.ShadowIntensity);
            }

            g.DrawImage(foreground, new Rectangle(drawX, drawY, drawW, drawH), bbox, GraphicsUnit.Pixel);
        }

        return finalBmp;
    }

    private static byte[] FeatherMaskSafe(byte[] mask, int w, int h, int radius)
    {
        byte[] result = new byte[mask.Length];

        for (int y = 0; y < h; y++)
        {
            int yMin = Math.Max(0, y - radius);
            int yMax = Math.Min(h - 1, y + radius);

            for (int x = 0; x < w; x++)
            {
                int xMin = Math.Max(0, x - radius);
                int xMax = Math.Min(w - 1, x + radius);

                int sum = 0;
                int count = 0;

                for (int ny = yMin; ny <= yMax; ny++)
                {
                    int row = ny * w;
                    for (int nx = xMin; nx <= xMax; nx++)
                    {
                        sum += mask[row + nx];
                        count++;
                    }
                }

                result[y * w + x] = (byte)(sum / Math.Max(1, count));
            }
        }

        return result;
    }

    private static void DrawContactShadowSafe(Graphics g, int drawX, int drawY, int drawW, int drawH, int intensity)
    {
        int shadowW = (int)(drawW * 0.78f);
        int shadowH = Math.Max(8, (int)(drawH * 0.12f));
        int shadowX = drawX + (drawW - shadowW) / 2;
        int shadowY = drawY + drawH - (shadowH / 2);

        int alphaVal = (int)(Math.Clamp(intensity, 0, 100) / 100f * 95);
        if (alphaVal <= 0) return;

        using var path = new GraphicsPath();
        path.AddEllipse(shadowX, shadowY, shadowW, shadowH);

        using var pgb = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(alphaVal, 25, 25, 30),
            SurroundColors = [Color.FromArgb(0, 25, 25, 30)]
        };

        g.FillEllipse(pgb, shadowX, shadowY, shadowW, shadowH);
    }

    private static float FastColorDistance(int c1, int c2)
    {
        int r1 = (c1 >> 16) & 0xFF;
        int g1 = (c1 >> 8) & 0xFF;
        int b1 = c1 & 0xFF;

        int r2 = (c2 >> 16) & 0xFF;
        int g2 = (c2 >> 8) & 0xFF;
        int b2 = c2 & 0xFF;

        int dr = r1 - r2;
        int dg = g1 - g2;
        int db = b1 - b2;

        return MathF.Sqrt(0.299f * dr * dr + 0.587f * dg * dg + 0.114f * db * db);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
                return Color.Transparent;

            return ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return Color.White;
        }
    }

    private static void SaveHighQualityJpg(Bitmap bmp, string targetPath, int quality)
    {
        var encoder = GetEncoder(ImageFormat.Jpeg);
        using var ms = new MemoryStream();

        if (encoder != null)
        {
            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
            bmp.Save(ms, encoder, encoderParams);
        }
        else
        {
            bmp.Save(ms, ImageFormat.Jpeg);
        }

        var bytes = ms.ToArray();
        var tempFile = Path.Combine(
            Path.GetDirectoryName(targetPath) ?? AppContext.BaseDirectory,
            $"temp_{Guid.NewGuid():N}.tmp");

        File.WriteAllBytes(tempFile, bytes);

        try
        {
            File.Move(tempFile, targetPath, overwrite: true);
        }
        catch
        {
            File.Copy(tempFile, targetPath, overwrite: true);
            try { File.Delete(tempFile); } catch { }
        }
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageEncoders();
        foreach (var codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        return null;
    }

    private AiImageEnhanceOptions LoadSettingsInternal()
    {
        var result = new AiImageEnhanceOptions();

        // 1. Try reading from appsettings.json first
        try
        {
            var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(appsettingsPath))
            {
                appsettingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "appsettings.json");
            }

            if (File.Exists(appsettingsPath))
            {
                var rawJson = File.ReadAllText(appsettingsPath);
                using var doc = JsonDocument.Parse(rawJson);
                if (doc.RootElement.TryGetProperty("AiSettings", out var aiObj))
                {
                    if (aiObj.TryGetProperty("GoogleApiKey", out var gKey))
                        result.GoogleAiApiKey = gKey.GetString();
                    if (aiObj.TryGetProperty("HuggingFaceApiKey", out var hKey))
                        result.HuggingFaceApiKey = hKey.GetString();
                    if (aiObj.TryGetProperty("EngineMode", out var modeProp) && Enum.TryParse<AiEngineMode>(modeProp.GetString(), out var mode))
                        result.EngineMode = mode;
                    if (aiObj.TryGetProperty("CustomPrompt", out var promptProp))
                        result.CustomPrompt = promptProp.GetString() ?? AiImageEnhanceOptions.DefaultJewelleryPrompt;
                }
            }
        }
        catch { }

        // 2. Supplement from user AppData options
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AiImageEnhanceOptions>(json);
                if (loaded != null)
                {
                    if (!string.IsNullOrWhiteSpace(loaded.GoogleAiApiKey))
                        result.GoogleAiApiKey = loaded.GoogleAiApiKey;
                    if (!string.IsNullOrWhiteSpace(loaded.HuggingFaceApiKey))
                        result.HuggingFaceApiKey = loaded.HuggingFaceApiKey;
                    if (!string.IsNullOrWhiteSpace(loaded.CustomPrompt))
                        result.CustomPrompt = loaded.CustomPrompt;

                    result.EngineMode = loaded.EngineMode;
                    result.BackgroundColor = loaded.BackgroundColor;
                    result.Brightness = loaded.Brightness;
                    result.Contrast = loaded.Contrast;
                    result.Warmth = loaded.Warmth;
                    result.Vibrance = loaded.Vibrance;
                    result.Sharpness = loaded.Sharpness;
                    result.EnableContactShadow = loaded.EnableContactShadow;
                    result.ShadowIntensity = loaded.ShadowIntensity;
                    result.AutoCenterPaddingPercent = loaded.AutoCenterPaddingPercent;
                }
            }
        }
        catch { }

        return result;
    }
}
