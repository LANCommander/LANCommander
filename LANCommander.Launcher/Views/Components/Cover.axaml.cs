using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.Helpers;

namespace LANCommander.Launcher.Views.Components;

public partial class Cover : UserControl, ICarouselLazyItem
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

    public static readonly DirectProperty<Cover, Thickness> FallbackPaddingProperty =
        AvaloniaProperty.RegisterDirect<Cover, Thickness>(nameof(FallbackPadding), o => o.FallbackPadding);

    public static readonly DirectProperty<Cover, IBrush> FallbackBackgroundProperty =
        AvaloniaProperty.RegisterDirect<Cover, IBrush>(nameof(FallbackBackground), o => o.FallbackBackground);

    // Fallback plate tones: six hues at matched lightness, each run as a 155° gradient into a darker
    // stop of the same hue, so a shelf of games without art has some depth without reading as six
    // accent colours. The title picks one via a stable hash.
    private static readonly (Color Light, Color Dark)[] FallbackTones =
    {
        (Color.Parse("#33383F"), Color.Parse("#1C1F24")), // slate
        (Color.Parse("#313A34"), Color.Parse("#1B211D")), // green
        (Color.Parse("#363140"), Color.Parse("#1E1B24")), // violet
        (Color.Parse("#3A352D"), Color.Parse("#201D18")), // amber
        (Color.Parse("#3A3339"), Color.Parse("#201C20")), // plum
        (Color.Parse("#2E363C"), Color.Parse("#191D21")), // steel
    };

    private static readonly IBrush[] FallbackBrushes = Array.ConvertAll(FallbackTones, tone =>
        (IBrush)new ImmutableLinearGradientBrush(
            new[] { new ImmutableGradientStop(0, tone.Light), new ImmutableGradientStop(1, tone.Dark) },
            startPoint: new RelativePoint(0.29, 0.05, RelativeUnit.Relative),
            endPoint: new RelativePoint(0.71, 0.95, RelativeUnit.Relative)));

    private bool _hasCover;
    private double _fallbackFontSize = 12;
    private Thickness _fallbackPadding = new(12);
    private IBrush _fallbackBackground = FallbackBrushes[0];
    private CancellationTokenSource? _loadCts;
    private IFrameAnimation? _animation;
    private bool _isAnimatedCover;
    // Video plays through LibVLC from the placeholder; an animated image (GIF/APNG/WebP) through
    // AnimatedImageRenderer, over its own still first frame.
    private bool _animatedIsVideo;
    private bool _receivedFirstFrame;
    private string? _lastLoadedSource;
    // Source for an animated cover whose stream is deferred until it should actually
    // play (hover/focus), so the depot doesn't stream every video cover up front.
    private string? _animatedSource;

    // The window hosting the cover. Animated covers hold their current frame while it isn't the
    // active window, so a launcher left behind other apps doesn't keep decoding and redrawing video.
    private WindowBase? _window;
    private bool IsWindowActive => _window?.IsActive ?? true;

    // Loads wait until the cover is attached. Inside a carousel they wait further, until the carousel
    // reports the cover within a page of the viewport, so a long carousel doesn't fetch every cover.
    private (string? Source, string? MimeType)? _pendingLoad;
    private CarouselControl? _carousel;
    private bool _nearViewport;

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

    public Thickness FallbackPadding
    {
        get => _fallbackPadding;
        private set => SetAndRaise(FallbackPaddingProperty, ref _fallbackPadding, value);
    }

    public IBrush FallbackBackground
    {
        get => _fallbackBackground;
        private set => SetAndRaise(FallbackBackgroundProperty, ref _fallbackBackground, value);
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
            RequestLoad(Source, MimeType);
        }
        else if (change.Property == IsPlayingAnimationProperty || change.Property == AlwaysAnimateProperty)
        {
            UpdateAnimationState();
        }
        else if (change.Property == TitleProperty)
        {
            FallbackBackground = GetFallbackBrush(Title);
        }
    }

    /// <summary>
    /// The placeholder plate brush for a game with no art. Shared with other art
    /// surfaces (e.g. <see cref="HeroCard"/>) so a game keeps the same tone everywhere.
    /// </summary>
    public static IBrush GetFallbackBrush(string? title) => FallbackBrushes[FallbackToneIndex(title)];

    /// <summary>
    /// FNV-1a over the title, so a game keeps its plate across launches
    /// (<see cref="string.GetHashCode()"/> is randomised per process).
    /// </summary>
    private static int FallbackToneIndex(string? title)
    {
        if (string.IsNullOrEmpty(title))
            return 0;

        var hash = 2166136261u;
        foreach (var c in title)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return (int)(hash % (uint)FallbackBrushes.Length);
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _window = TopLevel.GetTopLevel(this) as WindowBase;

        if (_window != null)
        {
            _window.Activated += OnWindowActivationChanged;
            _window.Deactivated += OnWindowActivationChanged;
        }

        _carousel = this.FindAncestorOfType<CarouselControl>();

        if (_carousel != null)
            _carousel.QueueVisibilityUpdate();
        else
            FlushPendingLoad();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _lastLoadedSource = null;
        _carousel = null;
        _nearViewport = false;
        StopVideo();
        ReleaseSnapshot();

        if (_window != null)
        {
            _window.Activated -= OnWindowActivationChanged;
            _window.Deactivated -= OnWindowActivationChanged;
            _window = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnWindowActivationChanged(object? sender, EventArgs e) => UpdateAnimationState();

    /// <inheritdoc />
    public void SetCarouselNearViewport(bool near)
    {
        _nearViewport = near;

        if (near)
            FlushPendingLoad();
    }

    private void RequestLoad(string? source, string? mimeType)
    {
        var canLoad = VisualRoot != null && (_carousel == null || _nearViewport);

        if (!canLoad)
        {
            _pendingLoad = (source, mimeType);
            return;
        }

        _pendingLoad = null;
        LoadCover(source, mimeType);
    }

    private void FlushPendingLoad()
    {
        if (_pendingLoad is not { } pending)
            return;

        _pendingLoad = null;
        LoadCover(pending.Source, pending.MimeType);
    }

    private void UpdateFallbackFontSize(Size size)
    {
        // Title and inset both track the cover's short side: ~16px type in 12px of inset on a
        // 160px shelf cover, capped so the detail page's large cover doesn't shout.
        var minDimension = Math.Min(size.Width, size.Height);
        FallbackFontSize = Math.Clamp(minDimension * 0.1, 8, 28);
        FallbackPadding = new Thickness(Math.Clamp(minDimension * 0.08, 6, 20));
    }

    private static bool IsVideoMimeType(string? mimeType) =>
        mimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true;

    // Whether a cached cover file is animated, keyed by path and write time. Not taken from the
    // media's MIME type: an APNG uploads as image/png, so only the file itself can tell.
    private static readonly ConcurrentDictionary<string, bool> _animatedImageProbes = new();

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

        _isAnimatedCover = IsVideoMimeType(mimeType);
        _animatedIsVideo = _isAnimatedCover;
        _lastLoadedSource = source;
        _animatedSource = _isAnimatedCover ? source : null;

        if (string.IsNullOrEmpty(source))
        {
            SetBitmap(null);
            return;
        }

        // Video covers go through LibVLC — but only start streaming when we should actually be
        // animating. Otherwise defer until hover (UpdateAnimationState) and show the title
        // placeholder in the meantime. Images load as stills; DetectAnimatedImageAsync then
        // marks a local GIF/APNG/WebP as animated.
        if (_isAnimatedCover)
        {
            if (AlwaysAnimate || IsPlayingAnimation)
                UpdateAnimationState();
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
                _ = DetectAnimatedImageAsync(path, ct);
                return;
            }
        }

        PendingLoads.Begin();

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

            if (bitmap != null)
                _ = DetectAnimatedImageAsync(path, ct);
        }
        catch
        {
            if (!ct.IsCancellationRequested)
                await Dispatcher.UIThread.InvokeAsync(() => SetBitmap(null), DispatcherPriority.Background);
        }
        finally
        {
            PendingLoads.End();
        }
    }

    /// <summary>
    /// Checks whether a local cover file is an animated image and, if so, makes the cover
    /// animated: its still stays up, and playback starts whenever it should animate.
    /// Remote covers are resized server thumbnails, so are never animated.
    /// </summary>
    private async Task DetectAnimatedImageAsync(string path, CancellationToken ct)
    {
        try
        {
            var animated = await Task.Run(() =>
            {
                var key = $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}";
                return _animatedImageProbes.GetOrAdd(key, _ => AnimatedImageFrames.IsAnimated(path));
            }, ct);

            if (!animated || ct.IsCancellationRequested)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ct.IsCancellationRequested || _lastLoadedSource != path || _isAnimatedCover)
                    return;

                _isAnimatedCover = true;
                _animatedIsVideo = false;
                _animatedSource = path;
                UpdateAnimationState();
            });
        }
        catch (OperationCanceledException)
        {
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

        PendingLoads.Begin();

        try
        {
            // Debounce remote loads as well.
            await Task.Delay(50, ct);

            var data = await _httpClient.GetByteArrayAsync(ThumbnailUrl.WithSize(uri.AbsoluteUri, decodeWidth, 0), ct);
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
        finally
        {
            PendingLoads.End();
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

    // ── Animated cover support (video via LibVLC; GIF/APNG/WebP decoded) ──

    // Playback eases between still and full speed rather than cutting: the rate runs 0..1 over
    // FadeDuration (scaled by how far it has to go, so a reversal mid-fade is quick), and at 0
    // the renderer pauses on whatever frame it reached.
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(450);

    // Slowest rate handed to a renderer; below it the fade just pauses.
    private const double MinRate = 1.0 / 32;

    // A paused renderer still holds a decoder (and for video a LibVLC player), so only the most
    // recently paused few are kept. Older ones are swapped for a still copy of their frame.
    private const int MaxHeldRenderers = 8;
    private static readonly LinkedList<Cover> _heldCovers = new();
    private LinkedListNode<Cover>? _heldNode;

    private DispatcherTimer? _fadeTimer;
    private long _fadeStarted;
    private double _fadeFrom;
    private TimeSpan _fadeLength;
    private double _rate;          // current playback rate, 0 = paused
    private double _targetRate;
    private double _appliedRate;   // last rate pushed to the renderer
    private bool _paused;          // whether the renderer is actually paused

    // A copy of a released renderer's last frame, shown by CoverImage. Owned by this cover.
    private Bitmap? _snapshot;

    private void LoadAnimatedCover(string source)
    {
        try
        {
            _receivedFirstFrame = false;
            _paused = false;
            _rate = 0;
            _appliedRate = MinRate;

            var decodeWidth = (uint)GetDecodePixelWidth();
            var maxHeight = (uint)(decodeWidth * 1.5);

            if (_animatedIsVideo)
            {
                var video = new VideoFrameRenderer(maxWidth: decodeWidth, maxHeight: maxHeight);
                video.FrameReady += OnAnimationFrameReady;
                _animation = video;
                video.SetRate(MinRate);
                video.Play(source, muted: true, loop: true);
            }
            else
            {
                _animation = new AnimatedImageRenderer(source, maxWidth: decodeWidth, maxHeight: maxHeight);
                _animation.SetRate(MinRate);
                _animation.FrameReady += OnAnimationFrameReady;
            }

            HasCover = true;
        }
        catch
        {
            // A video has nothing else to show; an image keeps its still.
            if (_animatedIsVideo)
                SetBitmap(null);
        }
    }

    private void OnAnimationFrameReady()
    {
        if (_animation == null) return;

        if (!_receivedFirstFrame)
        {
            _receivedFirstFrame = true;

            // Hide the Image control — we render directly via Render()
            CoverImage.IsVisible = false;
            HasCover = true;

            // A renderer can't be paused before it has produced a frame (LibVLC ignores it), so a
            // fade that reached zero first is applied now: this frame stands in as the still.
            ApplyRate();
        }

        // Redraw with the latest frame
        InvalidateVisual();
    }

    private void UpdateAnimationState()
    {
        if (!_isAnimatedCover) return;

        var shouldAnimate = AlwaysAnimate || IsPlayingAnimation;

        // Start playback on first hover/focus if it was deferred in LoadCover, or the renderer
        // was released while paused (see ReleaseHeldRenderer).
        if (shouldAnimate && _animation == null && !string.IsNullOrEmpty(_animatedSource))
            LoadAnimatedCover(_animatedSource);

        // Un-hovering (or the window going to the background) eases playback to a stop and holds
        // the frame it lands on; hovering again eases back in from that frame.
        FadeTo(shouldAnimate && IsWindowActive ? 1 : 0);
    }

    private void FadeTo(double target)
    {
        if (_animation == null)
        {
            StopFade();
            return;
        }

        if (target == _targetRate && (_fadeTimer?.IsEnabled == true || _rate == target))
            return;

        _targetRate = target;
        _fadeFrom = _rate;
        _fadeStarted = Stopwatch.GetTimestamp();
        _fadeLength = FadeDuration * Math.Abs(target - _rate);

        _fadeTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => FadeStep());
        _fadeTimer.Start();
        FadeStep();
    }

    private void FadeStep()
    {
        if (_animation == null)
        {
            StopFade();
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(_fadeStarted);
        var t = _fadeLength <= TimeSpan.Zero ? 1 : Math.Clamp(elapsed / _fadeLength, 0, 1);

        // Smoothstep, so the speed change has no visible kink at either end
        var eased = t * t * (3 - 2 * t);
        _rate = t >= 1 ? _targetRate : _fadeFrom + (_targetRate - _fadeFrom) * eased;

        ApplyRate();

        if (t >= 1)
            StopFade();
    }

    private void StopFade() => _fadeTimer?.Stop();

    private void ApplyRate()
    {
        if (_animation == null)
            return;

        if (_rate < MinRate)
        {
            // Only pause on the way down; the first instants of a fade-in also sit below MinRate.
            if (!_paused && _receivedFirstFrame && _targetRate == 0)
            {
                _animation.SetPaused(true);
                _paused = true;
                Hold();
            }

            return;
        }

        if (_paused)
        {
            _animation.SetPaused(false);
            _paused = false;
            Unhold();
        }

        // Don't push a rate change to LibVLC every tick; steps this small aren't visible.
        if (Math.Abs(_rate - _appliedRate) >= 0.03 || (_rate == _targetRate && _rate != _appliedRate))
        {
            _animation.SetRate(_rate);
            _appliedRate = _rate;
        }
    }

    private void Hold()
    {
        Unhold();
        _heldNode = _heldCovers.AddLast(this);

        while (_heldCovers.Count > MaxHeldRenderers)
            _heldCovers.First!.Value.ReleaseHeldRenderer();
    }

    private void Unhold()
    {
        if (_heldNode == null)
            return;

        _heldCovers.Remove(_heldNode);
        _heldNode = null;
    }

    /// <summary>
    /// Swaps a paused renderer for a still copy of the frame it's paused on, freeing its decoder.
    /// <see cref="_isAnimatedCover"/> stays set, so hovering again restarts playback (from the start).
    /// </summary>
    private void ReleaseHeldRenderer()
    {
        var snapshot = CopyFrame(_animation?.Frame);

        DisposeRenderer();

        if (snapshot != null)
        {
            SetBitmap(snapshot);
            _snapshot = snapshot;
        }
        else if (_animatedIsVideo)
            SetBitmap(null);
        else if (CoverImage != null)
            CoverImage.IsVisible = CoverImage.Source != null;

        InvalidateVisual();
    }

    private static WriteableBitmap? CopyFrame(Bitmap? frame)
    {
        if (frame == null)
            return null;

        try
        {
            var copy = new WriteableBitmap(frame.PixelSize, frame.Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);

            using (var buffer = copy.Lock())
                frame.CopyPixels(new PixelRect(frame.PixelSize), buffer.Address, buffer.RowBytes * buffer.Size.Height, buffer.RowBytes);

            return copy;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tears down the renderer without clearing <see cref="_isAnimatedCover"/>,
    /// so the cover can restart on a later hover. Contrast with <see cref="StopVideo"/>,
    /// which fully resets animated state when the source itself changes.
    /// </summary>
    private void DisposeRenderer()
    {
        StopFade();
        Unhold();

        if (_animation != null)
        {
            _animation.FrameReady -= OnAnimationFrameReady;
            _animation.Dispose();
            _animation = null;
        }

        _receivedFirstFrame = false;
        _paused = false;
        _rate = 0;
        _targetRate = 0;
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

        // For animated covers, render the current frame
        if (_isAnimatedCover && _animation?.Frame is { } bmp)
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

        ReleaseSnapshot(except: bitmap);
    }

    private void ReleaseSnapshot(Bitmap? except = null)
    {
        if (_snapshot == null || _snapshot == except)
            return;

        var snapshot = _snapshot;
        _snapshot = null;

        if (CoverImage != null && CoverImage.Source == snapshot)
            CoverImage.Source = null;

        snapshot.Dispose();
    }
}
