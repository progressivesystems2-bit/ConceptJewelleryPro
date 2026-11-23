using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace ConceptJewelleryPro.WPF.Themes;

public sealed record ThemeInfo(string Id, string DisplayName);

public static class ThemeManager
{
    private const string DefaultId = "Fluent";
    private const string ThemeFolderToken = "/Themes/";

    private static readonly Dictionary<string, string> SourceByThemeId = new()
    {
        ["Fluent"] = "/Themes/FluentLight.xaml",
        ["Gold"] = "/Themes/Gold.xaml",
        ["Emerald"] = "/Themes/Emerald.xaml",
        ["Glass"] = "/Themes/Glass.xaml",
        ["BlackGoldDiamond"] = "/Themes/BlackGoldDiamond.xaml"
    };

    private static readonly string PackPrefix =
        $"pack://application:,,,/{typeof(ThemeManager).Assembly.GetName().Name};component/";

    public static IReadOnlyList<ThemeInfo> Themes { get; } = new[]
    {
        new ThemeInfo("Fluent", "Fluent Light"),
        new ThemeInfo("Gold", "Royal Gold"),
        new ThemeInfo("Emerald", "Emerald"),
        new ThemeInfo("Glass", "Glassmorphism"),
        new ThemeInfo("BlackGoldDiamond", "Black Gold Diamond")
    };

    public static string DefaultThemeId => DefaultId;

    public static string CurrentThemeId { get; private set; } = DefaultId;

    private static string SavedFilePath => Path.Combine(AppContext.BaseDirectory, "uitheme.json");

    public static string LoadSavedThemeId()
    {
        try
        {
            if (File.Exists(SavedFilePath))
            {
                var saved = JsonSerializer.Deserialize<ThemePreference>(File.ReadAllText(SavedFilePath));
                if (saved is not null && SourceByThemeId.ContainsKey(saved.ThemeId))
                {
                    return saved.ThemeId;
                }
            }
        }
        catch
        {
        }

        return DefaultId;
    }

    public static void Apply(string themeId)
    {
        if (!SourceByThemeId.TryGetValue(themeId, out var source))
        {
            themeId = DefaultId;
            source = SourceByThemeId[DefaultId];
        }

        var app = System.Windows.Application.Current;
        if (app is null)
        {
            CurrentThemeId = themeId;
            return;
        }

        var merged = app.Resources.MergedDictionaries;

        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var sourceUri = merged[i].Source?.OriginalString;
            if (sourceUri is null)
            {
                continue;
            }

            // Remove only our palette dictionaries. Telerik's own theme URIs also contain
            // "/Themes/" (e.g. /Telerik.Windows.Themes.Windows11;component/Themes/...),
            // so they must be excluded or every Telerik control loses its default style.
            var isOurs = sourceUri.IndexOf("Telerik.Windows.Themes", StringComparison.OrdinalIgnoreCase) < 0
                && sourceUri.IndexOf(ThemeFolderToken, StringComparison.OrdinalIgnoreCase) >= 0;

            if (isOurs)
            {
                merged.RemoveAt(i);
            }
        }

        // Absolute pack URI bound to the WPF assembly so switching works regardless of host.
        merged.Add(new ResourceDictionary { Source = new Uri(PackPrefix + source.TrimStart('/'), UriKind.Absolute) });

        try
        {
            if (string.Equals(themeId, "BlackGoldDiamond", StringComparison.OrdinalIgnoreCase))
            {
                Telerik.Windows.Controls.Windows11Palette.LoadPreset(Telerik.Windows.Controls.Windows11Palette.ColorVariation.Dark);
            }
            else
            {
                Telerik.Windows.Controls.Windows11Palette.LoadPreset(Telerik.Windows.Controls.Windows11Palette.ColorVariation.Light);
            }

            Telerik.Windows.Controls.Windows11Palette.Palette.FocusVisualMargin = new Thickness(0);
        }
        catch
        {
            // Non-fatal
        }

        CurrentThemeId = themeId;
        Save(themeId);
    }

    private static void Save(string themeId)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(SavedFilePath, JsonSerializer.Serialize(new ThemePreference { ThemeId = themeId }, options));
        }
        catch
        {
        }
    }

    private sealed class ThemePreference
    {
        public string ThemeId { get; set; } = DefaultId;
    }
}
