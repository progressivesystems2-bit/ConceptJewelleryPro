using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ConceptJewelleryPro.WPF.Converters;

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        if (value is string s)
        {
            return string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;
        }

        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }
        return false;
    }
}
