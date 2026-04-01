using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace LANCommander.Launcher.Avalonia.Converters;

/// <summary>
/// Multi-value converter that treats the first value as a format string and the
/// remaining values as arguments — identical to string.Format(values[0], values[1..]).
///
/// XAML usage:
///   &lt;MultiBinding Converter="{StaticResource StringFormatConverter}"&gt;
///       &lt;Binding Source="{StaticResource GameReadyFormat}" /&gt;  &lt;!-- "{0} is ready to play!" --&gt;
///       &lt;Binding Path="Title" /&gt;
///   &lt;/MultiBinding&gt;
/// </summary>
public class StringFormatConverter : IMultiValueConverter
{
    public static readonly StringFormatConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 0 || values[0] is not string format)
            return null;

        var args = values.Skip(1).ToArray();

        return args.Length == 0 ? format : string.Format(format, args);
    }
}
