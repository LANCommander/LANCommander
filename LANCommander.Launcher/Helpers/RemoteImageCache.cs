using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace LANCommander.Launcher.Helpers;

/// <summary>
/// Shared, RAM-only image cache for images loaded from either a local file path
/// or an http(s) URL. Decoded bitmaps are held in a byte-bounded LRU so images
/// that scroll back into view (or are revisited while navigating) render instantly
/// without re-fetching or re-decoding.
///
/// Bitmaps returned by <see cref="LoadAsync"/> / <see cref="TryGet"/> are owned by
/// the cache and must NOT be disposed by callers — eviction disposes them once they
/// fall out of the budget.
/// </summary>
public static class RemoteImageCache
{
    private static readonly HttpClient _httpClient = new();

    private static readonly Dictionary<string, Bitmap> _cache = new();
    private static readonly LinkedList<string> _order = new();
    private static readonly Dictionary<string, LinkedListNode<string>> _nodes = new();
    private static readonly object _lock = new();
    private static long _cacheBytes;
    private const long MaxCacheBytes = 128L * 1024 * 1024;

    /// <summary>
    /// Synchronous fast path: returns a cached bitmap for the given source/decode size
    /// if one is present, refreshing its position in the LRU. Never touches disk or network.
    /// </summary>
    public static bool TryGet(string source, int decodeWidth, int decodeHeight, out Bitmap? bitmap)
    {
        var key = BuildKey(source, decodeWidth, decodeHeight);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out bitmap))
            {
                Touch(key);
                return true;
            }
        }

        bitmap = null;
        return false;
    }

    /// <summary>
    /// Returns a decoded bitmap for <paramref name="source"/> (a local file path or an
    /// http(s) URL), fetching and decoding off the calling thread when not already cached.
    /// Pass <paramref name="decodeWidth"/> to downscale by width, else <paramref name="decodeHeight"/>
    /// to downscale by height, else both zero to decode at full resolution.
    /// </summary>
    public static async Task<Bitmap?> LoadAsync(string source, int decodeWidth, int decodeHeight, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(source))
            return null;

        if (TryGet(source, decodeWidth, decodeHeight, out var cached))
            return cached;

        byte[]? data = null;

        if (IsHttp(source))
        {
            data = await _httpClient.GetByteArrayAsync(source, ct);
            if (ct.IsCancellationRequested)
                return null;
        }

        var bitmap = await Task.Run(() => Decode(source, data, decodeWidth, decodeHeight), ct);

        if (bitmap == null)
            return null;

        if (ct.IsCancellationRequested)
        {
            bitmap.Dispose();
            return null;
        }

        return Insert(BuildKey(source, decodeWidth, decodeHeight), bitmap);
    }

    private static Bitmap? Decode(string source, byte[]? data, int decodeWidth, int decodeHeight)
    {
        Stream stream;

        if (data != null)
        {
            stream = new MemoryStream(data);
        }
        else
        {
            if (!File.Exists(source))
                return null;

            stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        using (stream)
        {
            if (decodeWidth > 0)
                return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality);

            if (decodeHeight > 0)
                return Bitmap.DecodeToHeight(stream, decodeHeight, BitmapInterpolationMode.HighQuality);

            return new Bitmap(stream);
        }
    }

    /// <summary>
    /// Inserts a freshly-decoded bitmap, or returns the already-cached instance if another
    /// caller decoded the same key concurrently (disposing the duplicate). Evicts
    /// least-recently-used entries until back under the memory budget.
    /// </summary>
    private static Bitmap Insert(string key, Bitmap bitmap)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                bitmap.Dispose();
                Touch(key);
                return existing;
            }

            _cache[key] = bitmap;
            _nodes[key] = _order.AddLast(key);
            _cacheBytes += BitmapBytes(bitmap);

            // Evict LRU entries until under budget, always keeping the one just added.
            while (_cacheBytes > MaxCacheBytes && _order.Count > 1)
            {
                var oldest = _order.First!.Value;
                _order.RemoveFirst();
                _nodes.Remove(oldest);

                if (_cache.Remove(oldest, out var evicted))
                {
                    _cacheBytes -= BitmapBytes(evicted);
                    evicted.Dispose();
                }
            }

            return bitmap;
        }
    }

    private static void Touch(string key)
    {
        if (_nodes.TryGetValue(key, out var node))
        {
            _order.Remove(node);
            _order.AddLast(node);
        }
    }

    private static string BuildKey(string source, int decodeWidth, int decodeHeight) =>
        decodeWidth > 0 ? $"{source}|w{decodeWidth}"
        : decodeHeight > 0 ? $"{source}|h{decodeHeight}"
        : $"{source}|full";

    private static bool IsHttp(string source) =>
        source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static long BitmapBytes(Bitmap bitmap) =>
        (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4;
}
