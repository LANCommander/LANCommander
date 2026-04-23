using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;

namespace LANCommander.Launcher.Avalonia.Helpers;

/// <summary>
/// Renders video frames from LibVLC into an Avalonia <see cref="WriteableBitmap"/>.
/// Uses <c>SetVideoFormatCallbacks</c> so the bitmap dimensions match the
/// source video's native aspect ratio (optionally constrained to a max size).
/// </summary>
internal sealed class VideoFrameRenderer : IDisposable
{
    private static LibVLC? s_libVLC;

    internal static LibVLC SharedLibVLC =>
        s_libVLC ??= new LibVLC("--quiet", "--no-video-title-show");

    private readonly uint _maxWidth;
    private readonly uint _maxHeight;

    private MediaPlayer? _player;
    private WriteableBitmap? _bitmap;
    private IntPtr _buffer;
    private uint _width;
    private uint _height;
    private volatile bool _disposed;

    public WriteableBitmap? Bitmap => _bitmap;
    public MediaPlayer? Player => _player;

    /// <summary>Raised on the UI thread after a new frame has been written to <see cref="Bitmap"/>.</summary>
    public Action? FrameReady;

    /// <summary>
    /// Raised on the UI thread once the video format is known and
    /// <see cref="Bitmap"/> has been allocated.
    /// </summary>
    public Action? BitmapReady;

    /// <param name="maxWidth">
    /// Maximum pixel width for the render target. Zero means no constraint
    /// (native resolution). VLC will scale the output while preserving aspect ratio.
    /// </param>
    /// <param name="maxHeight">Same as <paramref name="maxWidth"/> but for height.</param>
    public VideoFrameRenderer(uint maxWidth = 0, uint maxHeight = 0)
    {
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;

        _player = new MediaPlayer(SharedLibVLC);
        _player.SetVideoCallbacks(OnLock, null, OnDisplay);
        _player.SetVideoFormatCallbacks(OnVideoFormat, OnVideoCleanup);
    }

    public void Play(string path, bool muted, bool loop, long startTimeMs = 0)
    {
        if (_player == null || _disposed) return;

        _player.Volume = muted ? 0 : 100;

        using var media = new Media(SharedLibVLC, path, FromType.FromPath);

        if (loop)
            media.AddOption(":input-repeat=65535");

        if (startTimeMs > 0)
        {
            var seconds = startTimeMs / 1000.0;
            media.AddOption($":start-time={seconds.ToString("F3", CultureInfo.InvariantCulture)}");
        }

        _player.Play(media);
    }

    // ── Video-format callback ────────────────────────────────────────────

    private uint OnVideoFormat(ref IntPtr opaque, IntPtr chroma,
        ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        // Constrain to max size while preserving aspect ratio.
        if (_maxWidth > 0 && _maxHeight > 0 && (width > _maxWidth || height > _maxHeight))
        {
            var scale = Math.Min((double)_maxWidth / width, (double)_maxHeight / height);
            width = Math.Max(2, (uint)(width * scale));
            height = Math.Max(2, (uint)(height * scale));
        }

        _width = width;
        _height = height;

        // Request BGRA pixel format.
        Marshal.WriteByte(chroma, 0, (byte)'B');
        Marshal.WriteByte(chroma, 1, (byte)'G');
        Marshal.WriteByte(chroma, 2, (byte)'R');
        Marshal.WriteByte(chroma, 3, (byte)'A');

        pitches = width * 4;
        lines = height;

        _buffer = Marshal.AllocHGlobal((int)(width * height * 4));

        // WriteableBitmap allocation is just memory — safe from any thread.
        _bitmap = new WriteableBitmap(
            new PixelSize((int)width, (int)height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed) BitmapReady?.Invoke();
        });

        return 1; // one picture buffer
    }

    private void OnVideoCleanup(ref IntPtr opaque)
    {
        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }

        _bitmap = null;
        _width = 0;
        _height = 0;
    }

    // ── Frame callbacks ──────────────────────────────────────────────────

    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        Marshal.WriteIntPtr(planes, _buffer);
        return IntPtr.Zero;
    }

    private void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        if (_disposed || _bitmap == null || _buffer == IntPtr.Zero || _width == 0) return;

        try
        {
            using var fb = _bitmap.Lock();
            unsafe
            {
                Buffer.MemoryCopy(
                    (void*)_buffer,
                    (void*)fb.Address,
                    (long)(fb.RowBytes * fb.Size.Height),
                    _width * _height * 4);
            }
        }
        catch
        {
            // Bitmap may have been disposed during shutdown.
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed) FrameReady?.Invoke();
        }, DispatcherPriority.Render);
    }

    // ── Disposal ─────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        FrameReady = null;
        BitmapReady = null;

        // Stop() blocks until all callbacks complete — safe to free afterwards.
        _player?.Stop();
        _player?.Dispose();
        _player = null;

        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }

        _bitmap = null;
    }
}
