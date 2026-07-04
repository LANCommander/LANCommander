using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LANCommander.Launcher.Helpers;

namespace LANCommander.Launcher.Views.Components;

public partial class Cover : UserControl
{
    private static readonly HttpClient _httpClient = new();

    // Strong-reference bitmap cache keyed by "path|decodeWidth" so covers that
    // scroll back into view are displayed instantly without re-reading the file.
    // Bounded by total decoded bytes (not entry count) because cover bitmap size
    // scales with DPI — a 500-entry cap is ~300 MB at 100% but ~800 MB at 200%.
    private static readonly Dictionary<string, Bitmap> _bitmapCache = new();
    private static readonly LinkedList<string> _cacheOrder = new();
    private static readonly Dictionary<string, LinkedListNode<string>> _cacheNodes = new();
    private static readonly object _cacheLock = new();
    private static long _cacheBytes;
    private const long MaxCacheBytes = 128L * 1024 * 1024;

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<Cover, string?>(nameof(Source));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Cover, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MimeTypeProperty =
        AvaloniaProperty.Register<Cover, string?>(nameof(MimeType));

    public static readonly StyledProperty<object?> OverlayProperty =
        AvaloniaProperty.Register<Cover, object?>(nameof(Overlay));

    public static readonly StyledProperty<bool> IsPlayingAnimationProperty =
        AvaloniaProperty.Register<Cover, bool>(nameof(IsPlayingAnimation));

    public static readonly StyledProperty<bool> AlwaysAnimateProperty =
        AvaloniaProperty.Register<Cover, bool>(nameof(AlwaysAnimate));

    public static readonly DirectProperty<Cover, bool> HasCoverProperty =
        AvaloniaProperty.RegisterDirect<Cover, bool>(nameof(HasCover), o => o.HasCover);

    public static readonly DirectProperty<Cover, double> FallbackFontSizeProperty =
        AvaloniaProperty.RegisterDirect<Cover, double>(nameof(FallbackFontSize), o => o.FallbackFontSize);

    private bool _hasCover;
    private double _fallbackFontSize = 12;
    private CancellationTokenSource? _loadCts;
    private VideoFrameRenderer? _videoRenderer;
    private bool _isAnimatedCover;
    private bool _receivedFirstFrame;
    private string? _lastLoadedSource;
    // Source for an animated cover whose stream is deferred until it should actually
    // play (hover/focus), so the depot doesn't stream every video cover up front.
    private string? _animatedSource;

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? MimeType
    {
        get => GetValue(MimeTypeProperty);
        set => SetValue(MimeTypeProperty, value);
    }

    public object? Overlay
    {
        get => GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    /// <summary>
    /// When true, an animated cover (video or APNG) will play.
    /// Typically toggled by the parent on hover/focus.
    /// </summary>
    public bool IsPlayingAnimation
    {
        get => GetValue(IsPlayingAnimationProperty);
        set => SetValue(IsPlayingAnimationProperty, value);
    }

    /// <summary>
    /// When true, the cover always animates (used in game detail view).
    /// </summary>
    public bool AlwaysAnimate
    {
        get => GetValue(AlwaysAnimateProperty);
        set => SetValue(AlwaysAnimateProperty, value);
    }

    public bool HasCover
    {
        get => _hasCover;
        private set => SetAndRaise(HasCoverProperty, ref _hasCover, value);
    }

    public double FallbackFontSize
    {
        get => _fallbackFontSize;
        private set => SetAndRaise(FallbackFontSizeProperty, ref _fallbackFontSize, value);
    }

    public Cover()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty || change.Property == MimeTypeProperty)
        {
            LoadCover(Source, MimeType);
        }
        else if (change.Property == IsPlayingAnimationProperty || change.Property == AlwaysAnimateProperty)
        {
            UpdateAnimationState();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!HasCover && !double.IsInfinity(availableSize.Width))
        {
            var constrained = new Size(availableSize.Width, availableSize.Width * 1.5);
            base.MeasureOverride(constrained);
            return constrained;
        }

        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        UpdateFallbackFontSize(result);
        return result;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _lastLoadedSource = null;
        StopVideo();
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateFallbackFontSize(Size size)
    {
        var minDimension = Math.Min(size.Width, size.Height);
        FallbackFontSize = Math.Max(8, minDimension * 0.09);
    }

    private static bool IsAnimatedMimeType(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType)) return false;
        return mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mimeType, "image/apng", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mimeType, "image/gif", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the pixel width to decode covers at, accounting for DPI scaling
    /// and hover zoom (covers scale to 1.1x on hover). We round up to the
    /// nearest multiple of 20 device pixels so a small layout change doesn't
    /// invalidate the cache.
    /// </summary>
    private int GetDecodePixelWidth()
    {
        var dpi = VisualRoot is TopLevel topLevel
            ? topLevel.RenderScaling
            : 1.0;

        var layoutWidth = Bounds.Width > 0 ? Bounds.Width : 160;

        // Add 15% headroom so hover zoom (1.1x) doesn't upscale beyond
        // the decoded resolution.
        var pixelWidth = (int)Math.Ceiling(layoutWidth * dpi * 1.15);

        // Round up to the nearest 20px to keep cache keys stable across
        // minor layout shifts (e.g. 142px and 148px both decode at 160px).
        return Math.Max(80, ((pixelWidth + 19) / 20) * 20);
    }

    private void LoadCover(string? source, string? mimeType)
    {
        // Skip reload if the source hasn't changed (e.g. recycled with same game)
        if (source == _lastLoadedSource && HasCover)
            return;

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        StopVideo();

        _isAnimatedCover = IsAnimatedMimeType(mimeType);
        _lastLoadedSource = source;
        _animatedSource = _isAnimatedCover ? source : null;

        if (string.IsNullOrEmpty(source))
        {
            SetBitmap(null);
            return;
        }

        // Route all animated covers (video, APNG, GIF) through LibVLC — but only start
        // streaming when we should actually be animating. Otherwise defer until hover
        // (UpdateAnimationState) and show the title placeholder in the meantime.
        if (_isAnimatedCover)
        {
            if (AlwaysAnimate || IsPlayingAnimation)
                LoadAnimatedCover(source);
            else
                SetBitmap(null);

            return;
        }

        var cts = new CancellationTokenSource();
        _loadCts = cts;

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            _ = LoadRemoteImageAsync(uri, cts.Token);
        }
        else
        {
            _ = LoadLocalImageAsync(source, cts.Token);
        }
    }

    private async Task LoadLocalImageAsync(string path, CancellationToken ct)
    {
        var decodeWidth = GetDecodePixelWidth();
        var cacheKey = $"{path}|{decodeWidth}";

        // Fast path: bitmap is already cached from a previous scroll position.
        // No debounce — show it instantly to eliminate flicker on scroll-back.
        lock (_cacheLock)
        {
            if (_bitmapCache.TryGetValue(cacheKey, out var cached))
            {
                // Move to end of LRU list (O(1) via node lookup)
                if (_cacheNodes.TryGetValue(cacheKey, out var node))
                {
                    _cacheOrder.Remove(node);
                    _cacheOrder.AddLast(node);
                }
                SetBitmap(cached);
                return;
            }
        }

        try
        {
            // Debounce: wait briefly so covers that are scrolled past quickly
            // never start an expensive file-read + decode.
            await Task.Delay(50, ct);

            var bitmap = await Task.Run(() =>
            {
                if (!System.IO.File.Exists(path))
                    return null;

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality);
            }, ct);

            if (ct.IsCancellationRequested)
            {
                bitmap?.Dispose();
                return;
            }

            if (bitmap != null)
                bitmap = CacheBitmap(cacheKey, bitmap);

            // Use Background priority so multiple covers completing together
            // coalesce into fewer layout passes instead of each forcing one.
            await Dispatcher.UIThread.InvokeAsync(() => SetBitmap(bitmap), DispatcherPriority.Background);
        }
        catch
        {
            if (!ct.IsCancellationRequested)
                await Dispatcher.UIThread.InvokeAsync(() => SetBitmap(null), DispatcherPriority.Background);
        }
    }

    private async Task LoadRemoteImageAsync(Uri uri, CancellationToken ct)
    {
        var decodeWidth = GetDecodePixelWidth();
        var cacheKey = $"{uri.AbsoluteUri}|{decodeWidth}";

        // Fast path: already cached.
        lock (_cacheLock)
        {
            if (_bitmapCache.TryGetValue(cacheKey, out var cached))
            {
                if (_cacheNodes.TryGetValue(cacheKey, out var node))
                {
                    _cacheOrder.Remove(node);
                    _cacheOrder.AddLast(node);
                }
                SetBitmap(cached);
                return;
            }
        }

        try
        {
            // Debounce remote loads as well.
            await Task.Delay(50, ct);

            var data = await _httpClient.GetByteArrayAsync(uri, ct);
            if (ct.IsCancellationRequested) return;

            using var stream = new MemoryStream(data);
            var bitmap = Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality);

            if (ct.IsCancellationRequested)
            {
                bitmap.Dispose();
                return;
            }

            bitmap = CacheBitmap(cacheKey, bitmap);

            await Dispatcher.UIThread.InvokeAsync(() => SetBitmap(bitmap), DispatcherPriority.Background);
        }
        catch
        {
            if (!ct.IsCancellationRequested)
                await Dispatcher.UIThread.InvokeAsync(() => SetBitmap(null), DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Inserts a freshly-decoded bitmap into the shared LRU cache, or returns the
    /// already-cached instance if another <see cref="Cover"/> decoded the same key
    /// concurrently (disposing the duplicate). Evicts least-recently-used entries
    /// until the cache is back under its memory budget, disposing each evicted
    /// bitmap so its native (Skia) memory is freed immediately.
    /// </summary>
    private static Bitmap CacheBitmap(string cacheKey, Bitmap bitmap)
    {
        lock (_cacheLock)
        {
            if (_bitmapCache.TryGetValue(cacheKey, out var existing))
            {
                bitmap.Dispose();
                bitmap = existing;
                if (_cacheNodes.TryGetValue(cacheKey, out var existingNode))
                {
                    _cacheOrder.Remove(existingNode);
                    _cacheOrder.AddLast(existingNode);
                }
            }
            else
            {
                _bitmapCache[cacheKey] = bitmap;
                _cacheNodes[cacheKey] = _cacheOrder.AddLast(cacheKey);
                _cacheBytes += BitmapBytes(bitmap);
            }

            // Evict least-recently-used entries until under the byte budget, but
            // always keep the entry we just touched (the tail of the LRU list).
            while (_cacheBytes > MaxCacheBytes && _cacheOrder.Count > 1)
            {
                var oldest = _cacheOrder.First!.Value;
                _cacheOrder.RemoveFirst();
                _cacheNodes.Remove(oldest);
                if (_bitmapCache.Remove(oldest, out var evicted))
                {
                    _cacheBytes -= BitmapBytes(evicted);
                    evicted.Dispose();
                }
            }

            return bitmap;
        }
    }

    private static long BitmapBytes(Bitmap bitmap) =>
        (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4;

    // ── Animated cover support (video, APNG, GIF via LibVLC) ────────────

    private void LoadAnimatedCover(string source)
    {
        try
        {
            _receivedFirstFrame = false;
            var decodeWidth = (uint)GetDecodePixelWidth();
            var maxHeight = (uint)(decodeWidth * 1.5);
            _videoRenderer = new VideoFrameRenderer(maxWidth: decodeWidth, maxHeight: maxHeight);
            _videoRenderer.FrameReady += OnVideoFrameReady;
            _videoRenderer.Play(source, muted: true, loop: true);

            HasCover = true;
        }
        catch
        {
            SetBitmap(null);
        }
    }

    private void OnVideoFrameReady()
    {
        if (_videoRenderer == null) return;

        if (!_receivedFirstFrame)
        {
            _receivedFirstFrame = true;

            // Hide the Image control — we render directly via Render()
            CoverImage.IsVisible = false;
            HasCover = true;

            // Pause immediately if we shouldn't be animating yet
            if (!AlwaysAnimate && !IsPlayingAnimation)
            {
                _videoRenderer.Player?.SetPause(true);
            }
        }

        // Redraw with the latest video frame
        InvalidateVisual();
    }

    private void UpdateAnimationState()
    {
        if (!_isAnimatedCover) return;

        var shouldAnimate = AlwaysAnimate || IsPlayingAnimation;

        if (shouldAnimate)
        {
            // Start streaming on first hover/focus if we deferred it in LoadCover.
            if (_videoRenderer == null)
            {
                if (!string.IsNullOrEmpty(_animatedSource))
                    LoadAnimatedCover(_animatedSource);
            }
            else
            {
                _videoRenderer.Player?.SetPause(false);
            }
        }
        else if (_videoRenderer != null)
        {
            // Tear the stream down entirely on un-hover instead of pausing, so idle
            // covers hold no LibVLC/network resources. _isAnimatedCover stays set so a
            // re-hover restarts playback.
            DisposeRenderer();
            SetBitmap(null);
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Tears down the video renderer without clearing <see cref="_isAnimatedCover"/>,
    /// so the cover can restart on a later hover. Contrast with <see cref="StopVideo"/>,
    /// which fully resets animated state when the source itself changes.
    /// </summary>
    private void DisposeRenderer()
    {
        if (_videoRenderer != null)
        {
            _videoRenderer.FrameReady -= OnVideoFrameReady;
            _videoRenderer.Dispose();
            _videoRenderer = null;
        }
        _receivedFirstFrame = false;
    }

    private void StopVideo()
    {
        DisposeRenderer();
        _isAnimatedCover = false;
    }

    // ── Rendering ────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // For animated covers, render the current video frame
        if (_isAnimatedCover && _videoRenderer?.Bitmap is { } bmp)
        {
            var srcW = (double)bmp.PixelSize.Width;
            var srcH = (double)bmp.PixelSize.Height;
            var dstW = Bounds.Width;
            var dstH = Bounds.Height;

            if (srcW > 0 && srcH > 0 && dstW > 0 && dstH > 0)
            {
                // UniformToFill: scale to fill, crop overflow
                var scale = Math.Max(dstW / srcW, dstH / srcH);
                var scaledW = srcW * scale;
                var scaledH = srcH * scale;
                var offsetX = (dstW - scaledW) / 2;
                var offsetY = (dstH - scaledH) / 2;

                context.DrawImage(
                    bmp,
                    new Rect(0, 0, srcW, srcH),
                    new Rect(offsetX, offsetY, scaledW, scaledH));
            }
        }
    }

    private void SetBitmap(Bitmap? bitmap)
    {
        HasCover = bitmap != null;

        if (CoverImage != null)
        {
            CoverImage.IsVisible = bitmap != null;
            CoverImage.Source = bitmap;
        }
    }
}
