using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LANCommander.Launcher.Avalonia.Converters;
using LANCommander.Launcher.Avalonia.Helpers;

namespace LANCommander.Launcher.Avalonia.Views.Components;

public partial class Cover : UserControl
{
    private static readonly HttpClient _httpClient = new();

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

    private void LoadCover(string? source, string? mimeType)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        StopVideo();

        _isAnimatedCover = IsAnimatedMimeType(mimeType);

        if (string.IsNullOrEmpty(source))
        {
            SetBitmap(null);
            return;
        }

        // Route all animated covers (video, APNG, GIF) through LibVLC
        if (_isAnimatedCover)
        {
            LoadAnimatedCover(source);
            return;
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            var cts = new CancellationTokenSource();
            _loadCts = cts;
            _ = LoadRemoteImageAsync(uri, cts.Token);
        }
        else
        {
            LoadLocalImage(source);
        }
    }

    private void LoadLocalImage(string path)
    {
        var bitmap = FilePathToBitmapConverter.Instance.Convert(
            path, typeof(Bitmap), null, System.Globalization.CultureInfo.InvariantCulture) as Bitmap;
        SetBitmap(bitmap);
    }

    private async Task LoadRemoteImageAsync(Uri uri, CancellationToken ct)
    {
        try
        {
            var data = await _httpClient.GetByteArrayAsync(uri, ct);
            if (ct.IsCancellationRequested) return;

            using var stream = new MemoryStream(data);
            var bitmap = Bitmap.DecodeToWidth(stream, 320, BitmapInterpolationMode.HighQuality);

            if (ct.IsCancellationRequested)
            {
                bitmap.Dispose();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => SetBitmap(bitmap));
        }
        catch
        {
            if (!ct.IsCancellationRequested)
                await Dispatcher.UIThread.InvokeAsync(() => SetBitmap(null));
        }
    }

    // ── Animated cover support (video, APNG, GIF via LibVLC) ────────────

    private void LoadAnimatedCover(string source)
    {
        try
        {
            _receivedFirstFrame = false;
            _videoRenderer = new VideoFrameRenderer(maxWidth: 320, maxHeight: 480);
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
        if (!_isAnimatedCover || _videoRenderer?.Player == null) return;

        if (AlwaysAnimate || IsPlayingAnimation)
        {
            _videoRenderer.Player.SetPause(false);
        }
        else
        {
            _videoRenderer.Player.SetPause(true);
        }
    }

    private void StopVideo()
    {
        if (_videoRenderer != null)
        {
            _videoRenderer.FrameReady -= OnVideoFrameReady;
            _videoRenderer.Dispose();
            _videoRenderer = null;
        }
        _isAnimatedCover = false;
        _receivedFirstFrame = false;
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
