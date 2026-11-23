using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ConceptJewelleryPro.WPF.Converters;

/// <summary>
/// Decodes images at reduced thumbnail dimensions (e.g., 60px) and caches them in memory.
/// Prevents large memory usage and UI stutters when scrolling thousands of items in virtualized grids.
/// </summary>
public sealed class ImageThumbnailConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> FailedPaths = new(StringComparer.OrdinalIgnoreCase);

    public int DecodePixelWidth { get; set; } = 60;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        if (FailedPaths.ContainsKey(path))
            return null;

        var targetWidth = DecodePixelWidth;
        if (parameter is int paramWidth && paramWidth > 0)
            targetWidth = paramWidth;
        else if (parameter is string s && int.TryParse(s, out var parsedWidth) && parsedWidth > 0)
            targetWidth = parsedWidth;

        var cacheKey = $"{path}_{targetWidth}";

        if (Cache.TryGetValue(cacheKey, out var cachedImage))
            return cachedImage;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = targetWidth;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
            bitmap.EndInit();
            bitmap.Freeze(); // Freezing allows cross-thread access and improves rendering speed

            Cache.TryAdd(cacheKey, bitmap);
            return bitmap;
        }
        catch
        {
            FailedPaths.TryAdd(path, true);
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw presidentialException();
    }

    private static NotImplementedException presidentialException() => new();
}
