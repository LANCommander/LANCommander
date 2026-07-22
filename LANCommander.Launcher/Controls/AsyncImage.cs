using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LANCommander.Launcher.Helpers;

namespace LANCommander.Launcher.Controls;

/// <summary>
/// Attached properties that asynchronously populate an <see cref="Image.Source"/> from a
/// local file path or an http(s) URL, routing through <see cref="RemoteImageCache"/> so
/// bitmaps are fetched/decoded off the UI thread and reused across the app.
///
/// Usage: <c>&lt;Image controls:AsyncImage.Source="{Binding HeroPath}" controls:AsyncImage.DecodeWidth="480" /&gt;</c>
///
/// Replaces the file-only <c>FilePathToBitmapConverter</c> where images may live on the server.
/// </summary>
public class AsyncImage : AvaloniaObject
{
    public static readonly AttachedProperty<string?> SourceProperty =
        AvaloniaProperty.RegisterAttached<AsyncImage, Image, string?>("Source");

    public static readonly AttachedProperty<int> DecodeWidthProperty =
        AvaloniaProperty.RegisterAttached<AsyncImage, Image, int>("DecodeWidth");

    public static readonly AttachedProperty<int> DecodeHeightProperty =
        AvaloniaProperty.RegisterAttached<AsyncImage, Image, int>("DecodeHeight");

    // Per-Image cancellation token for the in-flight load, so rapid source changes
    // (e.g. carousel container recycling) don't race to set a stale bitmap.
    private static readonly AttachedProperty<CancellationTokenSource?> LoadCtsProperty =
        AvaloniaProperty.RegisterAttached<AsyncImage, Image, CancellationTokenSource?>("LoadCts");

    static AsyncImage()
    {
        SourceProperty.Changed.AddClassHandler<Image>((image, _) => Reload(image));
        DecodeWidthProperty.Changed.AddClassHandler<Image>((image, _) => Reload(image));
        DecodeHeightProperty.Changed.AddClassHandler<Image>((image, _) => Reload(image));
    }

    public static string? GetSource(Image image) => image.GetValue(SourceProperty);
    public static void SetSource(Image image, string? value) => image.SetValue(SourceProperty, value);

    public static int GetDecodeWidth(Image image) => image.GetValue(DecodeWidthProperty);
    public static void SetDecodeWidth(Image image, int value) => image.SetValue(DecodeWidthProperty, value);

    public static int GetDecodeHeight(Image image) => image.GetValue(DecodeHeightProperty);
    public static void SetDecodeHeight(Image image, int value) => image.SetValue(DecodeHeightProperty, value);

    private static void Reload(Image image)
    {
        var previous = image.GetValue(LoadCtsProperty);
        previous?.Cancel();
        previous?.Dispose();
        image.SetValue(LoadCtsProperty, null);

        var source = GetSource(image);

        if (string.IsNullOrEmpty(source))
        {
            image.Source = null;
            return;
        }

        var width = GetDecodeWidth(image);
        var height = GetDecodeHeight(image);

        // Instant path: already decoded, avoid a flash of empty space on scroll-back.
        if (RemoteImageCache.TryGet(source, width, height, out var cached))
        {
            image.Source = cached;
            return;
        }

        image.Source = null;

        var cts = new CancellationTokenSource();
        image.SetValue(LoadCtsProperty, cts);

        LoadAsync(image, source, width, height, cts);
    }

    private static async void LoadAsync(Image image, string source, int width, int height, CancellationTokenSource cts)
    {
        try
        {
            var bitmap = await RemoteImageCache.LoadAsync(source, width, height, cts.Token);

            if (bitmap == null || cts.IsCancellationRequested)
                return;

            // Apply at Background priority so a burst of image completions (the whole
            // depot realizes at once — carousels aren't virtualized) yields to scroll
            // input and rendering instead of forcing a layout pass ahead of them.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Only apply if this load is still the current one for this image.
                if (!cts.IsCancellationRequested && GetSource(image) == source)
                    image.Source = bitmap;
            }, DispatcherPriority.Background);
        }
        catch
        {
            // Network/decoding failures leave the image blank; visibility is driven by the path binding.
        }
    }
}
