using System;
using System.Globalization;
using System.Threading.Tasks;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using LANCommander.Launcher.Avalonia.Helpers;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class VideoPlayerOverlay : UserControl
{
    /// <summary>
    /// Raised when the overlay is closed.
    /// The <c>long</c> argument is the playback position in milliseconds at
    /// the moment of closing, so the caller can sync it back to the carousel.
    /// </summary>
    public event EventHandler<long>? Closed;

    private VideoFrameRenderer? _renderer;
    private VideoCanvas? _canvas;
    private DispatcherTimer? _timer;
    private bool _isUpdatingSlider;
    private bool _isMuted;
    private bool _closing;

    public VideoPlayerOverlay()
    {
        InitializeComponent();

        SeekSlider.AddHandler(RangeBase.ValueChangedEvent, SeekSlider_ValueChanged);
    }

    /// <param name="path">Local file path of the video.</param>
    /// <param name="startTimeMs">
    /// Position (in milliseconds) to seek to once playback starts.
    /// Typically the carousel player's current position.
    /// </param>
    public void PlayVideo(string path, long startTimeMs = 0)
    {
        _renderer = new VideoFrameRenderer(maxWidth: 1920, maxHeight: 1080);

        // Use a direct-rendering control instead of Image so bitmap
        // updates are always picked up on every render pass.
        _canvas = new VideoCanvas();
        VideoBorder.Child = _canvas;

        _renderer.BitmapReady += () =>
        {
            _canvas.Bitmap = _renderer?.Bitmap;
            _canvas.InvalidateVisual();
        };
        _renderer.FrameReady += () => _canvas?.InvalidateVisual();

        _renderer.Player!.EndReached += (_, _) =>
            Dispatcher.UIThread.Post(() => PlayPauseIcon.Value = "Play");

        // Use VLC's :start-time option so decoding begins at the right
        // position immediately, avoiding a seek-after-play black flash.
        _renderer.Play(path, muted: false, loop: false, startTimeMs: startTimeMs);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += UpdateTransportControls;
        _timer.Start();

        Focus();
    }

    // ── Transport updates ────────────────────────────────────────────────

    private void UpdateTransportControls(object? sender, EventArgs e)
    {
        if (_renderer?.Player is not { } player) return;

        _isUpdatingSlider = true;
        SeekSlider.Value = player.Position;
        _isUpdatingSlider = false;

        var current = TimeSpan.FromMilliseconds(Math.Max(0, player.Time));
        var total = TimeSpan.FromMilliseconds(Math.Max(0, player.Length));
        TimeLabel.Text = $"{current:m\\:ss} / {total:m\\:ss}";
    }

    // ── Play / Pause ─────────────────────────────────────────────────────

    private void PlayPause_Click(object? sender, RoutedEventArgs e) => TogglePlayPause();

    private void TogglePlayPause()
    {
        if (_renderer?.Player is not { } player) return;

        if (player.IsPlaying)
        {
            player.Pause();
            PlayPauseIcon.Value = "Play";
        }
        else
        {
            player.Play();
            PlayPauseIcon.Value = "Pause";
        }
    }

    // ── Seek ─────────────────────────────────────────────────────────────

    private void SeekSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSlider || _renderer?.Player == null) return;
        _renderer.Player.Position = (float)SeekSlider.Value;
    }

    // ── Volume ───────────────────────────────────────────────────────────

    private void Volume_Click(object? sender, RoutedEventArgs e)
    {
        if (_renderer?.Player == null) return;
        _isMuted = !_isMuted;
        _renderer.Player.Volume = _isMuted ? 0 : 100;
        VolumeIcon.Value = _isMuted ? "SpeakerSlash" : "SpeakerHigh";
    }

    // ── Close / keyboard ─────────────────────────────────────────────────

    private void Close_Click(object? sender, RoutedEventArgs e) => CloseOverlay();

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e) => CloseOverlay();

    private void Video_PointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CloseOverlay();
                e.Handled = true;
                break;
            case Key.Space:
                TogglePlayPause();
                e.Handled = true;
                break;
        }
    }

    private void CloseOverlay()
    {
        if (_closing) return;
        _closing = true;

        _timer?.Stop();
        _timer = null;

        var timeMs = _renderer?.Player?.Time ?? 0;

        if (_renderer != null)
        {
            _renderer.FrameReady = null;
            _renderer.BitmapReady = null;
            var renderer = _renderer;
            _renderer = null;

            // Dispose on a background thread — MediaPlayer.Stop() is a
            // blocking call that waits for all VLC threads to finish. Running
            // it on the UI thread deadlocks if a VLC callback is contending
            // for the WriteableBitmap lock held by the render thread.
            _ = Task.Run(() => renderer.Dispose());
        }

        var layer = OverlayLayer.GetOverlayLayer(this);
        Closed?.Invoke(this, timeMs);
        layer?.Children.Remove(this);
    }

    // ── VideoCanvas ──────────────────────────────────────────────────────

    /// <summary>
    /// Lightweight control that draws a <see cref="WriteableBitmap"/>
    /// directly via <see cref="DrawingContext.DrawImage"/>, guaranteeing
    /// that every render pass reads the latest pixel data.
    /// </summary>
    private sealed class VideoCanvas : Control
    {
        public WriteableBitmap? Bitmap { get; set; }

        protected override Size MeasureOverride(Size availableSize) => availableSize;

        public override void Render(DrawingContext context)
        {
            if (Bitmap is not { } bmp) return;

            var srcW = (double)bmp.PixelSize.Width;
            var srcH = (double)bmp.PixelSize.Height;
            var dstW = Bounds.Width;
            var dstH = Bounds.Height;

            if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0) return;

            // Uniform stretch — fit within bounds, centred.
            var scale = Math.Min(dstW / srcW, dstH / srcH);
            var w = srcW * scale;
            var h = srcH * scale;

            context.DrawImage(
                bmp,
                new Rect(0, 0, srcW, srcH),
                new Rect((dstW - w) / 2, (dstH - h) / 2, w, h));
        }
    }
}
