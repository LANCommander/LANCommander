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
/// <see cref="DecodeWidthProperty"/> and <see cref="DecodeHeightProperty"/> are logical (DIP) sizes: they're
/// multiplied by the window's render scaling, so art stays sharp on scaled displays, and server thumbnails
/// are requested at that pixel size.
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

    /// <summary>
    /// Set when the current source couldn't be fetched or decoded, so a view can swap in a fallback.
    /// Cleared whenever the source changes.
    /// </summary>
    public static readonly AttachedProperty<bool> FailedProperty =
        AvaloniaProperty.RegisterAttached<AsyncImage, Image, bool>("Failed");

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

    public static bool GetFailed(Image image) => image.GetValue(FailedProperty);
    public static void SetFailed(Image image, bool value) => image.SetValue(FailedProperty, value);

    private static void Reload(Image image)
    {
        var previous = image.GetValue(LoadCtsProperty);
        previous?.Cancel();
        previous?.Dispose();
        image.SetValue(LoadCtsProperty, null);
        SetFailed(image, false);

        var source = GetSource(image);

        if (string.IsNullOrEmpty(source))
        {
            image.Source = null;
            return;
        }

        // Scaling isn't known until the image is in a window; decoding before then would guess 1x.
        var topLevel = TopLevel.GetTopLevel(image);

        if (topLevel == null)
        {
            image.Source = null;
            image.AttachedToVisualTree -= ReloadOnAttach;
            image.AttachedToVisualTree += ReloadOnAttach;
            return;
        }

        var width = DisplayScaling.ToPixels(GetDecodeWidth(image), topLevel.RenderScaling);
        var height = DisplayScaling.ToPixels(GetDecodeHeight(image), topLevel.RenderScaling);

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

    private static void ReloadOnAttach(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var image = (Image)sender!;

        image.AttachedToVisualTree -= ReloadOnAttach;
        Reload(image);
    }

    private static async void LoadAsync(Image image, string source, int width, int height, CancellationTokenSource cts)
    {
        PendingLoads.Begin();

        try
        {
            var bitmap = await RemoteImageCache.LoadAsync(source, width, height, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            if (bitmap == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => MarkFailed(image, source, cts));
                return;
            }

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
            // Network/decoding failures leave the image blank and raise Failed for views that want a fallback.
            if (!cts.IsCancellationRequested)
                await Dispatcher.UIThread.InvokeAsync(() => MarkFailed(image, source, cts));
        }
        finally
        {
            PendingLoads.End();
        }
    }

    private static void MarkFailed(Image image, string source, CancellationTokenSource cts)
    {
        if (!cts.IsCancellationRequested && GetSource(image) == source)
            SetFailed(image, true);
    }
}
