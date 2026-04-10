using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LANCommander.Launcher.Avalonia.Converters;

/// <summary>Converts a boolean to FontWeight: true → SemiBold, false → Normal.</summary>
public class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? FontWeight.SemiBold : FontWeight.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
