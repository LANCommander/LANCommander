using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace LANCommander.Launcher.Avalonia.Converters;

/// <summary>
/// Converts a file path string to a Bitmap, loading the image bytes directly
/// to work around files without extensions.
/// </summary>
public class FilePathToBitmapConverter : IValueConverter
{
    public static readonly FilePathToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        try
        {
            if (!File.Exists(path))
                return null;

            // Load image from file bytes - this works regardless of file extension
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch
        {
            // If we can't load the image, return null (fallback will be shown)
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
