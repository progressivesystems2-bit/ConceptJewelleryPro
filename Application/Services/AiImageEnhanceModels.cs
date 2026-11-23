using System;

namespace ConceptJewelleryPro.Application.Services;

public enum AiEngineMode
{
    GoogleGeminiStudio,
    OnlineBriaRmbg,
    OfflineLocal
}

public sealed class AiImageEnhanceOptions
{
    public const string DefaultJewelleryPrompt =
        "Professional luxury jewellery showroom commercial photography. Place the jewellery piece onto a seamless pure white studio background with soft realistic contact ground shadow. Preserve exact geometry, crisp silver and gold metallic luster, diamond facet sparkles, and eliminate all background counter noise.";

    public string PresetName { get; set; } = "Showroom Studio White";

    /// <summary>
    /// AI Engine Mode: Google Gemini Studio (Free API), Online BRIA RMBG, or Offline Local
    /// </summary>
    public AiEngineMode EngineMode { get; set; } = AiEngineMode.GoogleGeminiStudio;

    /// <summary>
    /// Free Google AI Studio API key (from https://aistudio.google.com/)
    /// </summary>
    public string? GoogleAiApiKey { get; set; }

    /// <summary>
    /// Optional Hugging Face API key/token (from https://huggingface.co/settings/tokens)
    /// </summary>
    public string? HuggingFaceApiKey { get; set; }

    /// <summary>
    /// Custom AI prompt sent to Google Gemini / Prompt Vision models
    /// </summary>
    public string CustomPrompt { get; set; } = DefaultJewelleryPrompt;

    /// <summary>
    /// Hex color for the background canvas (e.g., #FFFFFF for pure white, #F8F9FA for soft grey, #00000000 for transparent)
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Whether to isolate the foreground jewellery piece and neutralize background
    /// </summary>
    public bool NeutralizeBackground { get; set; } = true;

    /// <summary>
    /// Exposure/Brightness adjustment (-50 to +50, default +10)
    /// </summary>
    public int Brightness { get; set; } = 10;

    /// <summary>
    /// Contrast adjustment (-50 to +50, default +15)
    /// </summary>
    public int Contrast { get; set; } = 15;

    /// <summary>
    /// Warmth (Gold tone enhancement) (-50 to +50, default 0)
    /// </summary>
    public int Warmth { get; set; } = 0;

    /// <summary>
    /// Color Vibrance (-50 to +50, default +10)
    /// </summary>
    public int Vibrance { get; set; } = 10;

    /// <summary>
    /// Diamond/Facet Sharpness (0 to 100, default 35)
    /// </summary>
    public int Sharpness { get; set; } = 35;

    /// <summary>
    /// Generate natural soft ambient occlusion contact shadow underneath
    /// </summary>
    public bool EnableContactShadow { get; set; } = true;

    /// <summary>
    /// Contact shadow opacity/intensity (0 to 100, default 35)
    /// </summary>
    public int ShadowIntensity { get; set; } = 35;

    /// <summary>
    /// Auto-centering margin padding percentage (0% to 25%, default 8%)
    /// </summary>
    public int AutoCenterPaddingPercent { get; set; } = 8;

    /// <summary>
    /// Create a backup of the original image with _orig suffix before overwriting
    /// </summary>
    public bool BackupOriginal { get; set; } = true;

    public AiImageEnhanceOptions Clone()
    {
        return (AiImageEnhanceOptions)MemberwiseClone();
    }
}

public sealed class AiImageEnhanceResult
{
    public bool Success { get; set; }
    public string? EnhancedImagePath { get; set; }
    public string? OriginalBackupPath { get; set; }
    public string EngineUsed { get; set; } = "Google Image Studio (Gemini)";
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }

    public static AiImageEnhanceResult Succeeded(string path, string? backupPath, string engineUsed, TimeSpan duration) => new()
    {
        Success = true,
        EnhancedImagePath = path,
        OriginalBackupPath = backupPath,
        EngineUsed = engineUsed,
        Duration = duration
    };

    public static AiImageEnhanceResult Failed(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}
