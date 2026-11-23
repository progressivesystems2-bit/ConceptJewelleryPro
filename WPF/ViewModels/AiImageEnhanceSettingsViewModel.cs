using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ConceptJewelleryPro.Application.Services;

namespace ConceptJewelleryPro.WPF.ViewModels;

public sealed class BackgroundPresetItem
{
    public required string Name { get; init; }
    public required string HexColor { get; init; }
    public required string DisplayColor { get; init; }
    public required string Description { get; init; }
}

public sealed class AiImageEnhanceSettingsViewModel : ViewModelBase
{
    private readonly IAiImageEnhancementService _enhancementService;
    private readonly string _sourceImagePath;
    private CancellationTokenSource? _previewCts;
    private readonly DispatcherTimer _debounceTimer;

    private AiEngineMode _engineMode = AiEngineMode.GoogleGeminiStudio;
    private string? _googleAiApiKey;
    private string? _huggingFaceApiKey;
    private string _customPrompt = AiImageEnhanceOptions.DefaultJewelleryPrompt;
    private string _selectedBackgroundColor = "#FFFFFF";
    private bool _neutralizeBackground = true;
    private int _brightness = 10;
    private int _contrast = 15;
    private int _warmth = 0;
    private int _vibrance = 10;
    private int _sharpness = 35;
    private bool _enableContactShadow = true;
    private int _shadowIntensity = 35;
    private int _autoCenterPadding = 8;
    private string _selectedPreset = "Showroom Studio White";

    private BitmapSource? _originalImage;
    private BitmapSource? _enhancedPreview;
    private bool _isGeneratingPreview;
    private string _statusMessage = "Adjust sliders to fine-tune image in real-time.";

    public AiImageEnhanceSettingsViewModel(
        IAiImageEnhancementService enhancementService,
        string sourceImagePath)
    {
        _enhancementService = enhancementService;
        _sourceImagePath = sourceImagePath;

        // Load saved default options
        var saved = _enhancementService.GetDefaultOptions();
        LoadFromOptions(saved);

        ApplyCommand = new RelayCommand(() => RequestClose?.Invoke(this, true));
        SaveAsDefaultCommand = new RelayCommand(SaveAsDefault);
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        ResetPromptCommand = new RelayCommand(() => CustomPrompt = AiImageEnhanceOptions.DefaultJewelleryPrompt);

        SelectBackgroundPresetCommand = new RelayCommand(param =>
        {
            if (param is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                SelectedBackgroundColor = hex;
            }
        });

        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            _ = UpdatePreviewAsync();
        };

        LoadOriginalImage();
        _ = UpdatePreviewAsync();
    }

    public event EventHandler<bool>? RequestClose;

    public IReadOnlyList<string> EngineModeOptions { get; } =
    [
        "Google Image Studio (Gemini Free Tier)",
        "Hugging Face (BRIA RMBG Neural)",
        "Offline (Local Fast)"
    ];

    public string SelectedEngineModeString
    {
        get => _engineMode switch
        {
            AiEngineMode.GoogleGeminiStudio => "Google Image Studio (Gemini Free Tier)",
            AiEngineMode.OnlineBriaRmbg => "Hugging Face (BRIA RMBG Neural)",
            _ => "Offline (Local Fast)"
        };
        set
        {
            AiEngineMode mode;
            if (value?.Contains("Google", StringComparison.OrdinalIgnoreCase) == true)
            {
                mode = AiEngineMode.GoogleGeminiStudio;
            }
            else if (value?.Contains("Hugging", StringComparison.OrdinalIgnoreCase) == true)
            {
                mode = AiEngineMode.OnlineBriaRmbg;
            }
            else
            {
                mode = AiEngineMode.OfflineLocal;
            }

            if (_engineMode != mode)
            {
                _engineMode = mode;
                OnPropertyChanged(nameof(SelectedEngineModeString));
                OnPropertyChanged(nameof(IsGoogleEngine));
                OnPropertyChanged(nameof(IsHuggingFaceEngine));
                TriggerDebouncedPreview();
            }
        }
    }

    public bool IsGoogleEngine => _engineMode == AiEngineMode.GoogleGeminiStudio;
    public bool IsHuggingFaceEngine => _engineMode == AiEngineMode.OnlineBriaRmbg;

    public string? GoogleAiApiKey
    {
        get => _googleAiApiKey;
        set
        {
            if (SetProperty(ref _googleAiApiKey, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public string? HuggingFaceApiKey
    {
        get => _huggingFaceApiKey;
        set
        {
            if (SetProperty(ref _huggingFaceApiKey, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public string CustomPrompt
    {
        get => _customPrompt;
        set
        {
            if (SetProperty(ref _customPrompt, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public ObservableCollection<BackgroundPresetItem> BackgroundPresets { get; } =
    [
        new BackgroundPresetItem { Name = "Studio White", HexColor = "#FFFFFF", DisplayColor = "#FFFFFF", Description = "Clean pure white background (Standard for stickers & tags)" },
        new BackgroundPresetItem { Name = "Neutral Grey", HexColor = "#F8F9FA", DisplayColor = "#F8F9FA", Description = "Soft off-white modern luxury finish" },
        new BackgroundPresetItem { Name = "Warm Ivory", HexColor = "#F9F6F0", DisplayColor = "#F9F6F0", Description = "Warm ambient tone for traditional yellow gold" },
        new BackgroundPresetItem { Name = "Velvet Dark", HexColor = "#18181B", DisplayColor = "#18181B", Description = "High-contrast dark canvas for diamonds & platinum" },
        new BackgroundPresetItem { Name = "Transparent", HexColor = "Transparent", DisplayColor = "Transparent", Description = "Cutout with alpha channel for web/custom banners" }
    ];

    public ObservableCollection<string> StylePresets { get; } =
    [
        "Showroom Studio White",
        "High Luster Gold & Diamond",
        "Silver & Platinum Crisp",
        "Subtle Polish & Retouch"
    ];

    public string SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                ApplyStylePreset(value);
            }
        }
    }

    public string SelectedBackgroundColor
    {
        get => _selectedBackgroundColor;
        set
        {
            if (SetProperty(ref _selectedBackgroundColor, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public bool NeutralizeBackground
    {
        get => _neutralizeBackground;
        set
        {
            if (SetProperty(ref _neutralizeBackground, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public int Brightness
    {
        get => _brightness;
        set
        {
            if (SetProperty(ref _brightness, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public int Contrast
    {
        get => _contrast;
        set
        {
            if (SetProperty(ref _contrast, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public int Warmth
    {
        get => _warmth;
        set
        {
            if (SetProperty(ref _warmth, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public int Vibrance
    {
        get => _vibrance;
        set
        {
            if (SetProperty(ref _vibrance, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public int Sharpness
    {
        get => _sharpness;
        set
        {
            if (SetProperty(ref _sharpness, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public bool EnableContactShadow
    {
        get => _enableContactShadow;
        set
        {
            if (SetProperty(ref _enableContactShadow, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public int ShadowIntensity
    {
        get => _shadowIntensity;
        set
        {
            if (SetProperty(ref _shadowIntensity, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public int AutoCenterPadding
    {
        get => _autoCenterPadding;
        set
        {
            if (SetProperty(ref _autoCenterPadding, value))
            {
                TriggerDebouncedPreview();
            }
        }
    }

    public BitmapSource? OriginalImage
    {
        get => _originalImage;
        private set => SetProperty(ref _originalImage, value);
    }

    public BitmapSource? EnhancedPreview
    {
        get => _enhancedPreview;
        private set => SetProperty(ref _enhancedPreview, value);
    }

    public bool IsGeneratingPreview
    {
        get => _isGeneratingPreview;
        private set => SetProperty(ref _isGeneratingPreview, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand ApplyCommand { get; }
    public RelayCommand SaveAsDefaultCommand { get; }
    public RelayCommand ResetDefaultsCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ResetPromptCommand { get; }
    public RelayCommand SelectBackgroundPresetCommand { get; }

    public AiImageEnhanceOptions ToOptions()
    {
        return new AiImageEnhanceOptions
        {
            PresetName = SelectedPreset,
            EngineMode = _engineMode,
            GoogleAiApiKey = _googleAiApiKey,
            HuggingFaceApiKey = _huggingFaceApiKey,
            CustomPrompt = _customPrompt,
            BackgroundColor = SelectedBackgroundColor,
            NeutralizeBackground = NeutralizeBackground,
            Brightness = Brightness,
            Contrast = Contrast,
            Warmth = Warmth,
            Vibrance = Vibrance,
            Sharpness = Sharpness,
            EnableContactShadow = EnableContactShadow,
            ShadowIntensity = ShadowIntensity,
            AutoCenterPaddingPercent = AutoCenterPadding,
            BackupOriginal = true
        };
    }

    private void LoadFromOptions(AiImageEnhanceOptions opt)
    {
        _engineMode = opt.EngineMode;
        _googleAiApiKey = opt.GoogleAiApiKey;
        _huggingFaceApiKey = opt.HuggingFaceApiKey;
        _customPrompt = string.IsNullOrWhiteSpace(opt.CustomPrompt) ? AiImageEnhanceOptions.DefaultJewelleryPrompt : opt.CustomPrompt;
        _selectedPreset = opt.PresetName;
        _selectedBackgroundColor = opt.BackgroundColor;
        _neutralizeBackground = opt.NeutralizeBackground;
        _brightness = opt.Brightness;
        _contrast = opt.Contrast;
        _warmth = opt.Warmth;
        _vibrance = opt.Vibrance;
        _sharpness = opt.Sharpness;
        _enableContactShadow = opt.EnableContactShadow;
        _shadowIntensity = opt.ShadowIntensity;
        _autoCenterPadding = opt.AutoCenterPaddingPercent;
    }

    private void ApplyStylePreset(string preset)
    {
        switch (preset)
        {
            case "Showroom Studio White":
                _selectedBackgroundColor = "#FFFFFF";
                _neutralizeBackground = true;
                _brightness = 10;
                _contrast = 15;
                _warmth = 0;
                _vibrance = 10;
                _sharpness = 35;
                _enableContactShadow = true;
                _shadowIntensity = 35;
                _autoCenterPadding = 8;
                break;

            case "High Luster Gold & Diamond":
                _selectedBackgroundColor = "#FFFFFF";
                _neutralizeBackground = true;
                _brightness = 15;
                _contrast = 20;
                _warmth = 15;
                _vibrance = 20;
                _sharpness = 50;
                _enableContactShadow = true;
                _shadowIntensity = 45;
                _autoCenterPadding = 8;
                break;

            case "Silver & Platinum Crisp":
                _selectedBackgroundColor = "#FFFFFF";
                _neutralizeBackground = true;
                _brightness = 12;
                _contrast = 18;
                _warmth = -5;
                _vibrance = 0;
                _sharpness = 40;
                _enableContactShadow = true;
                _shadowIntensity = 30;
                _autoCenterPadding = 8;
                break;

            case "Subtle Polish & Retouch":
                _neutralizeBackground = false;
                _brightness = 8;
                _contrast = 10;
                _warmth = 5;
                _vibrance = 8;
                _sharpness = 20;
                _enableContactShadow = false;
                _autoCenterPadding = 0;
                break;
        }

        OnPropertyChanged(nameof(SelectedBackgroundColor));
        OnPropertyChanged(nameof(NeutralizeBackground));
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(Contrast));
        OnPropertyChanged(nameof(Warmth));
        OnPropertyChanged(nameof(Vibrance));
        OnPropertyChanged(nameof(Sharpness));
        OnPropertyChanged(nameof(EnableContactShadow));
        OnPropertyChanged(nameof(ShadowIntensity));
        OnPropertyChanged(nameof(AutoCenterPadding));

        TriggerDebouncedPreview();
    }

    private void TriggerDebouncedPreview()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private async Task UpdatePreviewAsync()
    {
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;

        IsGeneratingPreview = true;
        StatusMessage = _engineMode switch
        {
            AiEngineMode.GoogleGeminiStudio => "Rendering AI Preview (Google Gemini Studio Free API)...",
            AiEngineMode.OnlineBriaRmbg => "Rendering AI Preview (BRIA RMBG Neural)...",
            _ => "Rendering AI Local Preview..."
        };

        try
        {
            var options = ToOptions();
            var bytes = await _enhancementService.GeneratePreviewBytesAsync(_sourceImagePath, options, maxPreviewDimension: 700, token);

            if (token.IsCancellationRequested || bytes.Length == 0) return;

            var bmp = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze();

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    EnhancedPreview = bmp;
                    StatusMessage = _engineMode switch
                    {
                        AiEngineMode.GoogleGeminiStudio => "✨ Studio Preview Updated (Google Gemini AI)",
                        AiEngineMode.OnlineBriaRmbg => "✨ Studio Preview Updated (BRIA RMBG Neural)",
                        _ => "AI Local Preview Updated"
                    };
                    IsGeneratingPreview = false;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusMessage = $"Preview note: {ex.Message}";
                IsGeneratingPreview = false;
            });
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsGeneratingPreview = false;
            }
        }
    }

    private void LoadOriginalImage()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_sourceImagePath) && File.Exists(_sourceImagePath))
            {
                var bytes = File.ReadAllBytes(_sourceImagePath);
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                }
                bmp.Freeze();
                OriginalImage = bmp;
            }
        }
        catch { }
    }

    private void SaveAsDefault()
    {
        var options = ToOptions();
        _enhancementService.SaveDefaultOptions(options);
        StatusMessage = "Current AI options, API keys & custom prompt saved as global default!";
    }

    private void ResetDefaults()
    {
        ApplyStylePreset("Showroom Studio White");
        _selectedPreset = "Showroom Studio White";
        _engineMode = AiEngineMode.GoogleGeminiStudio;
        _customPrompt = AiImageEnhanceOptions.DefaultJewelleryPrompt;
        OnPropertyChanged(nameof(SelectedPreset));
        OnPropertyChanged(nameof(SelectedEngineModeString));
        OnPropertyChanged(nameof(IsGoogleEngine));
        OnPropertyChanged(nameof(IsHuggingFaceEngine));
        OnPropertyChanged(nameof(CustomPrompt));
        StatusMessage = "Reset to default Google AI Studio showroom presets.";
    }
}
