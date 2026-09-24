using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.Helpers;
using LANCommander.Launcher.ViewModels.Components;

namespace LANCommander.Launcher.Views;

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
    private EventHandler<EventArgs>? _endReachedHandler;

    // Filmstrip thumbnails, one per item, in item order
    private readonly List<Button> _thumbs = new();

    /// <summary>Screenshot names are often just the uploaded file name, which makes a poor caption.</summary>
    private static readonly Regex FileNamePattern = new(@"\.[A-Za-z0-9]{2,5}$", RegexOptions.Compiled);

    public LightboxOverlay()
    {
        InitializeComponent();
        ModalEscape.Enable(this, CloseOverlay);
        SeekSlider.AddHandler(RangeBase.ValueChangedEvent, SeekSlider_ValueChanged);
    }

    /// <summary>
    /// Opens the lightbox with the given items, starting at the specified index.
    /// </summary>
    /// <param name="items">All items available for navigation.</param>
    /// <param name="startIndex">Index of the item to show first.</param>
    /// <param name="videoStartTimeMs">For video items, the starting playback position.</param>
    /// <param name="title">Shown in the top-left chrome, usually the game's title.</param>
    public void Show(IReadOnlyList<LightboxItem> items, int startIndex = 0, long videoStartTimeMs = 0, string? title = null)
    {
        _items = items;
        _currentIndex = Math.Clamp(startIndex, 0, Math.Max(0, items.Count - 1));
        _videoStartTimeMs = videoStartTimeMs;

        var isManuals = items.Count > 0 && items.All(i => i.Type == LightboxItemType.Pdf);

        ChromeTitle.Text = title ?? string.Empty;
        ChromeTitle.IsVisible = !string.IsNullOrEmpty(title);
        ChromeDivider.IsVisible = ChromeTitle.IsVisible;
        ChromeSection.Text = isManuals ? (items.Count == 1 ? "Manual" : "Manuals") : "Media";

        // Reserve the caption line for the whole session if any item has one, so moving between
        // items never resizes the stage.
        CaptionText.IsVisible = items.Any(i => CaptionFor(i) != null);

        BuildFilmstrip(isManuals);
        ShowCurrentItem();
        Focus();
    }

    /// <summary>Shows the lightbox as an overlay on the main window.</summary>
    public static LightboxOverlay ShowOverlay(IReadOnlyList<LightboxItem> items, int startIndex = 0, long videoStartTimeMs = 0, string? title = null)
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
        overlay.Show(items, startIndex, videoStartTimeMs, title);

        return overlay;
    }

    /// <summary>A manual's title, or a screenshot/video name that reads like a caption; null otherwise.</summary>
    private static string? CaptionFor(LightboxItem item)
    {
        var name = item.Title?.Trim();

        if (string.IsNullOrEmpty(name))
            return null;

        return item.Type == LightboxItemType.Pdf || !FileNamePattern.IsMatch(name) ? name : null;
    }

    // ── Filmstrip ────────────────────────────────────────────────────────

    /// <summary>
    /// One thumbnail per screenshot or video, clickable to jump there. Manuals have no useful
    /// thumbnail, so they're navigated with the arrows only.
    /// </summary>
    private void BuildFilmstrip(bool isManuals)
    {
        Filmstrip.Children.Clear();
        _thumbs.Clear();

        FilmstripScroller.IsVisible = !isManuals && _items.Count > 1;

        if (!FilmstripScroller.IsVisible)
            return;

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var index = i;

            var image = new Image { Stretch = Stretch.UniformToFill };
            var tile = new Panel();

            tile.Children.Add(new Border { Background = (IBrush?)this.FindResource("SurfaceBrush") });
            tile.Children.Add(image);

            if (item.Type == LightboxItemType.Video)
            {
                tile.Children.Add(new Icon
                {
                    Type = IconVariant.Fill,
                    Value = "Play",
                    Width = 18,
                    Height = 18,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                });
            }

            var thumb = new Button
            {
                Content = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    ClipToBounds = true,
                    Margin = new Thickness(1),
                    Child = tile,
                },
            };
            thumb.Classes.Add("FilmstripThumb");
            thumb.Click += (_, _) => GoTo(index);

            _thumbs.Add(thumb);
            Filmstrip.Children.Add(thumb);

            _ = LoadThumbnailAsync(item, image);
        }
    }

    /// <summary>
    /// Decodes a small copy of a screenshot for its thumbnail, off the UI thread. Videos use their
    /// preview frame when the caller supplied one and otherwise keep the play glyph on a plain tile.
    /// </summary>
    private static async Task LoadThumbnailAsync(LightboxItem item, Image target)
    {
        if (item.Type == LightboxItemType.Video)
        {
            target.Source = item.ImageSource;
            return;
        }

        if (string.IsNullOrEmpty(item.Path) || !File.Exists(item.Path))
        {
            target.Source = item.ImageSource;
            return;
        }

        PendingLoads.Begin();

        try
        {
            // Thumbnail strip items are 120 logical px wide.
            var decodeWidth = DisplayScaling.ToPixels(120, target);
            target.Source = await Task.Run(() =>
            {
                using var stream = File.OpenRead(item.Path);
                return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality);
            });
        }
        catch
        {
            target.Source = item.ImageSource;
        }
        finally
        {
            PendingLoads.End();
        }
    }

    private void GoTo(int index)
    {
        if (index == _currentIndex || index < 0 || index >= _items.Count)
            return;

        StopVideo();

        _videoStartTimeMs = 0;
        _currentIndex = index;
        ShowCurrentItem();
        Focus();
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
        CaptionText.Text = CaptionFor(item) ?? string.Empty;

        for (var i = 0; i < _thumbs.Count; i++)
            _thumbs[i].Classes.Set("current", i == _currentIndex);

        if (_currentIndex < _thumbs.Count)
            _thumbs[_currentIndex].BringIntoView();

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

        try
        {
            _renderer = new VideoFrameRenderer(maxWidth: 1920, maxHeight: 1080);
        }
        catch
        {
            // LibVLC may not be available (native libraries missing in deployment).
            // Fall back to showing the image if one is available, otherwise leave blank.
            LoadingIndicator.IsVisible = false;
            VideoBorder.IsVisible = false;
            TransportControls.IsVisible = false;

            if (item.ImageSource != null || !string.IsNullOrEmpty(item.Path))
                ShowImage(item);

            return;
        }

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

        _endReachedHandler = (_, _) =>
            Dispatcher.UIThread.Post(() => PlayPauseIcon.Value = "Play");

        if (_renderer.Player != null)
            _renderer.Player.EndReached += _endReachedHandler;

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
        if (_timer != null)
        {
            _timer.Tick -= UpdateTransportControls;
            _timer.Stop();
            _timer = null;
        }

        if (_renderer != null)
        {
            if (_endReachedHandler != null && _renderer.Player != null)
                _renderer.Player.EndReached -= _endReachedHandler;
            _endReachedHandler = null;
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
