using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Data;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using LANCommander.Launcher.Avalonia.Helpers;
using LANCommander.Launcher.Avalonia.ViewModels.Components;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class LightboxOverlay : UserControl
{
    public event EventHandler? Closed;

    /// <summary>
    /// Raised when a video overlay closes. The long is the playback position in ms.
    /// The int is the item index that was playing.
    /// </summary>
    public event EventHandler<(int Index, long TimeMs)>? VideoClosed;

    private IReadOnlyList<LightboxItem> _items = Array.Empty<LightboxItem>();
    private int _currentIndex;
    private bool _closing;

    // Video state
    private VideoFrameRenderer? _renderer;
    private VideoCanvas? _canvas;
    private DispatcherTimer? _timer;
    private bool _isUpdatingSlider;
    private bool _isMuted;
    private long _videoStartTimeMs;

    public LightboxOverlay()
    {
        InitializeComponent();
        SeekSlider.AddHandler(RangeBase.ValueChangedEvent, SeekSlider_ValueChanged);
    }

    /// <summary>
    /// Opens the lightbox with the given items, starting at the specified index.
    /// </summary>
    /// <param name="items">All items available for navigation.</param>
    /// <param name="startIndex">Index of the item to show first.</param>
    /// <param name="videoStartTimeMs">For video items, the starting playback position.</param>
    public void Show(IReadOnlyList<LightboxItem> items, int startIndex = 0, long videoStartTimeMs = 0)
    {
        _items = items;
        _currentIndex = Math.Clamp(startIndex, 0, Math.Max(0, items.Count - 1));
        _videoStartTimeMs = videoStartTimeMs;

        ShowCurrentItem();
        Focus();
    }

    /// <summary>Shows the lightbox as an overlay on the main window.</summary>
    public static LightboxOverlay ShowOverlay(IReadOnlyList<LightboxItem> items, int startIndex = 0, long videoStartTimeMs = 0)
    {
        var overlay = new LightboxOverlay
        {
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
        };

        var mainWindow = (global::Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        var layer = OverlayLayer.GetOverlayLayer(mainWindow);
        if (layer is null)
            return overlay;

        overlay.Bind(global::Avalonia.Layout.Layoutable.WidthProperty,
            new Binding("Bounds.Width") { Source = layer });
        overlay.Bind(global::Avalonia.Layout.Layoutable.HeightProperty,
            new Binding("Bounds.Height") { Source = layer });

        layer.Children.Add(overlay);
        overlay.Show(items, startIndex, videoStartTimeMs);

        return overlay;
    }

    // ── Navigation ───────────────────────────────────────────────────────

    private void Navigate(int delta)
    {
        if (_items.Count <= 1) return;

        StopVideo();

        _videoStartTimeMs = 0;
        _currentIndex = (_currentIndex + delta + _items.Count) % _items.Count;
        ShowCurrentItem();
    }

    private void ShowCurrentItem()
    {
        if (_items.Count == 0) return;

        var item = _items[_currentIndex];

        // Hide all content
        ImageBorder.IsVisible = false;
        VideoBorder.IsVisible = false;
        PdfBorder.IsVisible = false;
        TransportControls.IsVisible = false;
        LoadingIndicator.IsVisible = false;

        // Update navigation
        PrevButton.IsVisible = _items.Count > 1;
        NextButton.IsVisible = _items.Count > 1;
        ItemCounter.Text = _items.Count > 1 ? $"{_currentIndex + 1} / {_items.Count}" : string.Empty;

        switch (item.Type)
        {
            case LightboxItemType.Image:
                ShowImage(item);
                break;
            case LightboxItemType.Video:
                ShowVideo(item);
                break;
            case LightboxItemType.Pdf:
                ShowPdf(item);
                break;
        }
    }

    // ── Image ────────────────────────────────────────────────────────────

    private void ShowImage(LightboxItem item)
    {
        if (item.ImageSource != null)
        {
            LightboxImage.Source = item.ImageSource;
        }
        else if (!string.IsNullOrEmpty(item.Path))
        {
            try
            {
                LightboxImage.Source = new Bitmap(item.Path);
            }
            catch
            {
                LightboxImage.Source = null;
            }
        }

        ImageBorder.IsVisible = true;
    }

    // ── Video ────────────────────────────────────────────────────────────

    private void ShowVideo(LightboxItem item)
    {
        LoadingIndicator.IsVisible = true;
        VideoBorder.IsVisible = true;
        TransportControls.IsVisible = true;

        _renderer = new VideoFrameRenderer(maxWidth: 1920, maxHeight: 1080);
        _canvas = new VideoCanvas();
        VideoBorder.Child = _canvas;

        _renderer.BitmapReady += () =>
        {
            _canvas.Bitmap = _renderer?.Bitmap;
            _canvas.InvalidateVisual();
        };
        _renderer.FrameReady += () =>
        {
            if (LoadingIndicator.IsVisible)
                LoadingIndicator.IsVisible = false;
            _canvas?.InvalidateVisual();
        };

        _renderer.Player!.EndReached += (_, _) =>
            Dispatcher.UIThread.Post(() => PlayPauseIcon.Value = "Play");

        _renderer.Play(item.Path, muted: false, loop: false, startTimeMs: _videoStartTimeMs);

        PlayPauseIcon.Value = "Pause";
        _isMuted = false;
        VolumeIcon.Value = "SpeakerHigh";

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += UpdateTransportControls;
        _timer.Start();
    }

    private void StopVideo()
    {
        _timer?.Stop();
        _timer = null;

        if (_renderer != null)
        {
            _renderer.FrameReady = null;
            _renderer.BitmapReady = null;
            var renderer = _renderer;
            _renderer = null;
            _ = Task.Run(() => renderer.Dispose());
        }

        _canvas = null;
        VideoBorder.Child = null;
    }

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

    private void SeekSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSlider || _renderer?.Player == null) return;
        _renderer.Player.Position = (float)SeekSlider.Value;
    }

    private void Volume_Click(object? sender, RoutedEventArgs e)
    {
        if (_renderer?.Player == null) return;
        _isMuted = !_isMuted;
        _renderer.Player.Volume = _isMuted ? 0 : 100;
        VolumeIcon.Value = _isMuted ? "SpeakerSlash" : "SpeakerHigh";
    }

    // ── PDF ──────────────────────────────────────────────────────────────

    private void ShowPdf(LightboxItem item)
    {
        PdfViewer.Source = item.Path;
        PdfBorder.IsVisible = true;
    }

    // ── Close / keyboard ─────────────────────────────────────────────────

    private void Close_Click(object? sender, RoutedEventArgs e) => CloseOverlay();
    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e) => CloseOverlay();
    private void Content_PointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;
    private void Prev_Click(object? sender, RoutedEventArgs e) => Navigate(-1);
    private void Next_Click(object? sender, RoutedEventArgs e) => Navigate(1);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CloseOverlay();
                e.Handled = true;
                break;
            case Key.Space:
                if (_items.Count > 0 && _items[_currentIndex].Type == LightboxItemType.Video)
                    TogglePlayPause();
                e.Handled = true;
                break;
            case Key.Left:
                Navigate(-1);
                e.Handled = true;
                break;
            case Key.Right:
                Navigate(1);
                e.Handled = true;
                break;
        }
    }

    private void CloseOverlay()
    {
        if (_closing) return;
        _closing = true;

        var videoTimeMs = _renderer?.Player?.Time ?? 0;
        var videoIndex = _currentIndex;
        var wasVideo = _items.Count > 0 && _items[_currentIndex].Type == LightboxItemType.Video;

        StopVideo();

        // Clean up PDF
        PdfViewer.Source = null;

        var layer = OverlayLayer.GetOverlayLayer(this);

        if (wasVideo)
            VideoClosed?.Invoke(this, (videoIndex, videoTimeMs));

        Closed?.Invoke(this, EventArgs.Empty);
        layer?.Children.Remove(this);
    }

    // ── VideoCanvas ──────────────────────────────────────────────────────

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
