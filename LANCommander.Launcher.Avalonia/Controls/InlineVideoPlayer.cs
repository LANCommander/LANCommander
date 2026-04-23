using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LANCommander.Launcher.Avalonia.Helpers;

namespace LANCommander.Launcher.Avalonia.Controls;

/// <summary>
/// Plays a video file muted and looping, rendering frames directly to the
/// Avalonia render surface. The output preserves the source video's aspect
/// ratio (uniform stretch, centred within the control bounds).
/// </summary>
public class InlineVideoPlayer : Control, IDisposable
{
    public static readonly StyledProperty<string?> VideoPathProperty =
        AvaloniaProperty.Register<InlineVideoPlayer, string?>(nameof(VideoPath));

    public string? VideoPath
    {
        get => GetValue(VideoPathProperty);
        set => SetValue(VideoPathProperty, value);
    }

    private VideoFrameRenderer? _renderer;
    private bool _isAttached;
    private bool _disposed;

    /// <summary>Current playback position in milliseconds.</summary>
    public long CurrentTimeMs => _renderer?.Player?.Time ?? 0;

    static InlineVideoPlayer()
    {
        AffectsRender<InlineVideoPlayer>(VideoPathProperty);
    }

    // ── Layout ───────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    // ── Lifecycle ────────────────────────────────────────────────────────

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == VideoPathProperty)
        {
            StopPlayback();

            var path = change.GetNewValue<string?>();
            if (!string.IsNullOrEmpty(path) && _isAttached)
                StartPlayback(path);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;

        if (!string.IsNullOrEmpty(VideoPath) && _renderer == null)
            StartPlayback(VideoPath);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        StopPlayback();
        base.OnDetachedFromVisualTree(e);
    }

    // ── Public transport controls ────────────────────────────────────────

    /// <summary>Pauses playback without disposing the renderer.</summary>
    public void Pause()
    {
        _renderer?.Player?.SetPause(true);
    }

    /// <summary>Seeks to <paramref name="timeMs"/> and resumes muted playback.</summary>
    public void ResumeAt(long timeMs)
    {
        if (_renderer?.Player is not { } player) return;

        player.Time = timeMs;
        player.SetPause(false);
    }

    // ── Playback lifecycle ───────────────────────────────────────────────

    private void StartPlayback(string path)
    {
        try
        {
            // Constrain render to carousel slot size while preserving aspect ratio.
            _renderer = new VideoFrameRenderer(maxWidth: 384, maxHeight: 216);
            _renderer.FrameReady += InvalidateVisual;
            _renderer.Play(path, muted: true, loop: true);
        }
        catch
        {
            // LibVLC initialisation may fail — control remains blank.
        }
    }

    private void StopPlayback()
    {
        if (_renderer != null)
        {
            _renderer.FrameReady -= InvalidateVisual;
            _renderer.Dispose();
            _renderer = null;
        }
    }

    // ── Rendering ────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        if (_renderer?.Bitmap is not { } bmp) return;

        var srcW = (double)bmp.PixelSize.Width;
        var srcH = (double)bmp.PixelSize.Height;
        var dstW = Bounds.Width;
        var dstH = Bounds.Height;

        if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0) return;

        // Uniform stretch: scale to fit, preserving aspect ratio, centred.
        var scale = Math.Min(dstW / srcW, dstH / srcH);
        var scaledW = srcW * scale;
        var scaledH = srcH * scale;
        var offsetX = (dstW - scaledW) / 2;
        var offsetY = (dstH - scaledH) / 2;

        context.DrawImage(
            bmp,
            new Rect(0, 0, srcW, srcH),
            new Rect(offsetX, offsetY, scaledW, scaledH));
    }

    // ── IDisposable ──────────────────────────────────────────────────────

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            StopPlayback();
        }
    }
}
