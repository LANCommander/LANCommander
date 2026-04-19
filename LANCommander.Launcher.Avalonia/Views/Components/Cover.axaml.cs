using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LANCommander.Launcher.Avalonia.Converters;

namespace LANCommander.Launcher.Avalonia.Views.Components;

public partial class Cover : UserControl
{
    private static readonly HttpClient _httpClient = new();

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<Cover, string?>(nameof(Source));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Cover, string?>(nameof(Title));

    public static readonly StyledProperty<object?> OverlayProperty =
        AvaloniaProperty.Register<Cover, object?>(nameof(Overlay));

    public static readonly DirectProperty<Cover, bool> HasCoverProperty =
        AvaloniaProperty.RegisterDirect<Cover, bool>(nameof(HasCover), o => o.HasCover);

    public static readonly DirectProperty<Cover, double> FallbackFontSizeProperty =
        AvaloniaProperty.RegisterDirect<Cover, double>(nameof(FallbackFontSize), o => o.FallbackFontSize);

    private bool _hasCover;
    private double _fallbackFontSize = 12;
    private CancellationTokenSource? _loadCts;

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

    /// <summary>
    /// Content rendered on top of the cover image (e.g. badges, labels).
    /// </summary>
    public object? Overlay
    {
        get => GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
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

        if (change.Property == SourceProperty)
        {
            LoadImage(change.GetNewValue<string?>());
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

    private void UpdateFallbackFontSize(Size size)
    {
        var minDimension = Math.Min(size.Width, size.Height);
        FallbackFontSize = Math.Max(8, minDimension * 0.09);
    }

    private void LoadImage(string? source)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        if (string.IsNullOrEmpty(source))
        {
            SetBitmap(null);
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

    private void SetBitmap(Bitmap? bitmap)
    {
        HasCover = bitmap != null;

        if (CoverImage != null)
            CoverImage.Source = bitmap;
    }
}
