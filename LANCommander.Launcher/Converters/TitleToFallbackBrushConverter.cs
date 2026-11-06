using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LANCommander.Launcher.Views.Components;

namespace LANCommander.Launcher.Converters;

/// <summary>
/// Maps a game title to its placeholder plate brush, the same title-hashed tone
/// <see cref="Cover"/> draws when a game has no cover art.
/// </summary>
public class TitleToFallbackBrushConverter : IValueConverter
{
    public static readonly TitleToFallbackBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Cover.GetFallbackBrush(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
