using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace LANCommander.Launcher.Avalonia.Converters;

/// <summary>
/// Converts a file path string to a thumbnail Bitmap.
/// Decodes at a fixed width so full-resolution source images don't consume
/// excessive memory, and caches bitmaps via WeakReference so they are freed
/// by the GC once no control is displaying them (e.g. after virtualization
/// scrolls them out of view) and reloaded on next access.
/// </summary>
public class FilePathToBitmapConverter : IValueConverter
{
    // Covers are displayed at 140–220 px wide. 320 px gives enough headroom
    // for high-DPI screens while keeping per-image memory around 400 KB
    // instead of several MB for full-resolution sources.
    private const int DecodeWidth = 320;

    // WeakReference cache: keeps bitmaps alive while on-screen, lets the GC
    // collect them once nothing holds a strong reference (i.e. off-screen).
    // Key is "{path}|{decodeWidth}" so full-res and thumbnail don't collide.
    private static readonly Dictionary<string, WeakReference<Bitmap>> _cache = new();

    public static readonly FilePathToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        // ConverterParameter="full" bypasses downscaling (e.g. game detail view)
        bool fullRes = parameter is string p && p == "full";
        var cacheKey = fullRes ? $"{path}|full" : $"{path}|{DecodeWidth}";

        if (_cache.TryGetValue(cacheKey, out var weakRef) && weakRef.TryGetTarget(out var cached))
            return cached;

        try
        {
            if (!File.Exists(path))
                return null;

            using var stream = File.OpenRead(path);
            var bitmap = fullRes
                ? new Bitmap(stream)
                : Bitmap.DecodeToWidth(stream, DecodeWidth, BitmapInterpolationMode.HighQuality);
            _cache[cacheKey] = new WeakReference<Bitmap>(bitmap);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
