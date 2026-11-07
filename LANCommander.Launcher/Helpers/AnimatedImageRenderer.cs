using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;

namespace LANCommander.Launcher.Helpers;

/// <summary>
/// Plays an animated GIF, APNG or WebP into a <see cref="WriteableBitmap"/>, looping, decoding
/// each frame on a background thread at the image's own timing. The counterpart of
/// <see cref="VideoFrameRenderer"/> for the formats LibVLC shows as stills.
/// </summary>
internal sealed class AnimatedImageRenderer : IFrameAnimation
{
    private readonly CancellationTokenSource _cts = new();
    private readonly object _pauseLock = new();
    private TaskCompletionSource? _resume; // set while paused
    private volatile bool _disposed;
    private volatile WriteableBitmap? _bitmap;

    // The frame bitmap the decode loop writes into. Freed once the renderer is disposed and the
    // loop has stopped writing to it, whichever happens last.
    private readonly object _outputLock = new();
    private WriteableBitmap? _output;
    private bool _loopFinished;

    public Bitmap? Frame => _bitmap;
    public Action? FrameReady { get; set; }

    /// <param name="maxWidth">Largest output width; the image is scaled down, keeping its aspect, to fit. Zero means native size.</param>
    /// <param name="maxHeight">As <paramref name="maxWidth"/>, for height.</param>
    public AnimatedImageRenderer(string path, uint maxWidth = 0, uint maxHeight = 0)
    {
        var token = _cts.Token;
        _ = Task.Run(() => RunAsync(path, maxWidth, maxHeight, token), token);
    }

    public void SetPaused(bool paused)
    {
        lock (_pauseLock)
        {
            if (paused)
            {
                _resume ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            else
            {
                _resume?.TrySetResult();
                _resume = null;
            }
        }
    }

    private Task WhilePausedAsync(CancellationToken ct)
    {
        lock (_pauseLock)
            return _resume?.Task.WaitAsync(ct) ?? Task.CompletedTask;
    }

    private async Task RunAsync(string path, uint maxWidth, uint maxHeight, CancellationToken ct)
    {
        try
        {
            using var frames = AnimatedImageFrames.Open(path);

            if (frames == null)
                return;

            var (width, height) = Fit(frames.Width, frames.Height, maxWidth, maxHeight);
            var scaledInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var scaled = new SKBitmap(scaledInfo);
            var output = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

            lock (_outputLock)
                _output = output;
            var index = 0;

            while (!ct.IsCancellationRequested)
            {
                await WhilePausedAsync(ct);

                var started = Stopwatch.GetTimestamp();
                var delay = frames.Render(index);

                frames.Canvas.ScalePixels(scaled, SKFilterQuality.Medium);
                CopyInto(scaled, output);

                _bitmap = output;
                Dispatcher.UIThread.Post(() =>
                {
                    if (!_disposed) FrameReady?.Invoke();
                }, DispatcherPriority.Render);

                index = (index + 1) % frames.FrameCount;

                var remaining = delay - Stopwatch.GetElapsedTime(started);
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Unreadable or malformed: the cover keeps its still image.
        }
        finally
        {
            lock (_outputLock)
                _loopFinished = true;

            ReleaseOutput();
        }
    }

    /// <summary>
    /// Disposes the frame bitmap once nothing can use it: the host dropped this renderer and the
    /// loop stopped. It's native (Skia) memory the GC barely sees, so finalizers would leave it
    /// piling up. Disposed on the UI thread, where the host draws it.
    /// </summary>
    private void ReleaseOutput()
    {
        WriteableBitmap? output;

        lock (_outputLock)
        {
            if (!_disposed || !_loopFinished || _output == null)
                return;

            output = _output;
            _output = null;
        }

        _bitmap = null;
        Dispatcher.UIThread.Post(output.Dispose);
    }

    private static unsafe void CopyInto(SKBitmap source, WriteableBitmap target)
    {
        using var buffer = target.Lock();
        var sourceRowBytes = source.RowBytes;
        var rowBytes = Math.Min(sourceRowBytes, buffer.RowBytes);
        var src = (byte*)source.GetPixels();
        var dst = (byte*)buffer.Address;

        for (var y = 0; y < buffer.Size.Height; y++)
            Buffer.MemoryCopy(src + y * sourceRowBytes, dst + y * buffer.RowBytes, buffer.RowBytes, rowBytes);
    }

    private static (int Width, int Height) Fit(int width, int height, uint maxWidth, uint maxHeight)
    {
        if (maxWidth == 0 || maxHeight == 0 || (width <= maxWidth && height <= maxHeight))
            return (width, height);

        var scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
        return (Math.Max(2, (int)(width * scale)), Math.Max(2, (int)(height * scale)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        FrameReady = null;
        _cts.Cancel();
        _bitmap = null;

        // A paused loop wakes on the cancellation too (WhilePausedAsync waits with the token)
        ReleaseOutput();
    }
}
