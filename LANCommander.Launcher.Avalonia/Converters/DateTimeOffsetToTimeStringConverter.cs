using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LANCommander.Launcher.Avalonia.Converters;

/// <summary>Formats a DateTimeOffset as a short local-time string (HH:mm).</summary>
public class DateTimeOffsetToTimeStringConverter : IValueConverter
{
    public static readonly DateTimeOffsetToTimeStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto)
            return dto.LocalDateTime.ToString("HH:mm", culture);
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
