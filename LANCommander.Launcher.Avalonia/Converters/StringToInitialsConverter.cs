using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LANCommander.Launcher.Avalonia.Converters;

/// <summary>Returns the first character of a string, uppercased, for use as an avatar initial.</summary>
public class StringToInitialsConverter : IValueConverter
{
    public static readonly StringToInitialsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && s.Length > 0)
            return char.ToUpperInvariant(s[0]).ToString();
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
