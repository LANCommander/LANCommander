using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LANCommander.Launcher.Converters;

/// <summary>Converts a boolean to a double: true → 1.0, false → 0.5.</summary>
public class BoolToDoubleConverter : IValueConverter
{
    public static readonly BoolToDoubleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? 1.0 : 0.5;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
