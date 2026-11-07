using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SkiaSharp;

namespace LANCommander.Launcher.Helpers;

/// <summary>
/// Decodes an animated image one frame at a time, compositing each onto a full-size
/// <see cref="Canvas"/>. Frames must be rendered in order (0, 1, 2 … then 0 again), since each
/// builds on the one before it.
/// </summary>
/// <remarks>
/// LibVLC opens GIF and APNG as still images, so animated images are decoded here instead:
/// GIF and WebP through Skia's own codec, and APNG — which Skia doesn't animate — by rebuilding
/// each frame as a standalone PNG for Skia to decode, then applying APNG's dispose/blend rules.
/// </remarks>
internal abstract class AnimatedImageFrames : IDisposable
{
    public abstract int Width { get; }
    public abstract int Height { get; }
    public abstract int FrameCount { get; }

    /// <summary>The composited image after the last <see cref="Render"/>, BGRA premultiplied.</summary>
    public abstract SKBitmap Canvas { get; }

    /// <summary>Composites frame <paramref name="index"/> onto <see cref="Canvas"/> and returns how long it should show.</summary>
    public abstract TimeSpan Render(int index);

    public abstract void Dispose();

    /// <summary>Opens an animated image, or returns null if the file isn't one this can animate.</summary>
    public static AnimatedImageFrames? Open(string path)
    {
        var bytes = File.ReadAllBytes(path);

        if (IsPng(bytes))
            return ApngFrames.TryCreate(bytes);

        var codec = SKCodec.Create(SKData.CreateCopy(bytes));

        if (codec == null || codec.FrameCount < 2)
        {
            codec?.Dispose();
            return null;
        }

        return new CodecFrames(codec);
    }

    /// <summary>
    /// Whether the file holds more than one frame. Cheap for PNG (reads chunk headers up to the
    /// image data); GIF and WebP are counted by Skia.
    /// </summary>
    public static bool IsAnimated(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> header = stackalloc byte[12];

            if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length)
                return false;

            if (IsPng(header))
                return PngHasAnimation(stream);

            var isGif = header[..4].SequenceEqual("GIF8"u8);
            var isWebP = header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8);

            if (!isGif && !isWebP)
                return false;

            stream.Position = 0;
            using var codec = SKCodec.Create(stream);
            return codec?.FrameCount > 1;
        }
        catch
        {
            return false;
        }
    }

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 && bytes[..8].SequenceEqual(PngSignature);

    /// <summary>An APNG declares itself with an acTL chunk, which must come before the first IDAT.</summary>
    private static bool PngHasAnimation(Stream stream)
    {
        stream.Position = 8;
        Span<byte> chunk = stackalloc byte[12];

        while (stream.ReadAtLeast(chunk[..8], 8, throwOnEndOfStream: false) == 8)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(chunk);
            var type = chunk[4..8];

            if (type.SequenceEqual("acTL"u8))
            {
                if (stream.ReadAtLeast(chunk[..4], 4, throwOnEndOfStream: false) < 4)
                    return false;

                return BinaryPrimitives.ReadUInt32BigEndian(chunk) > 1;
            }

            if (type.SequenceEqual("IDAT"u8) || type.SequenceEqual("IEND"u8))
                return false;

            stream.Seek(length + 4, SeekOrigin.Current); // data + CRC
        }

        return false;
    }

    /// <summary>A shortest frame delay below which the image's own timing is ignored, as browsers do.</summary>
    private static TimeSpan ClampDelay(TimeSpan delay, TimeSpan floor, TimeSpan fallback) =>
        delay < floor ? fallback : delay;

    // ── GIF / WebP ───────────────────────────────────────────────────────

    private sealed class CodecFrames : AnimatedImageFrames
    {
        private readonly SKCodec _codec;
        private readonly SKCodecFrameInfo[] _frames;
        private readonly SKImageInfo _info;
        private readonly SKBitmap _canvas;

        public CodecFrames(SKCodec codec)
        {
            _codec = codec;
            _frames = codec.FrameInfo;
            _info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            _canvas = new SKBitmap(_info);
        }

        public override int Width => _info.Width;
        public override int Height => _info.Height;
        public override int FrameCount => _frames.Length;
        public override SKBitmap Canvas => _canvas;

        public override TimeSpan Render(int index)
        {
            // The canvas holds frame index-1, so Skia can build on it when this frame depends on
            // earlier ones. If it can't (e.g. the prior frame restores-to-previous), let Skia
            // decode the chain itself.
            var prior = index > 0 && _frames[index].RequiredFrame != -1 ? index - 1 : -1;

            if (prior == -1)
                _canvas.Erase(SKColors.Transparent);

            var result = _codec.GetPixels(_info, _canvas.GetPixels(), new SKCodecOptions(index, prior));

            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput) && prior != -1)
                _codec.GetPixels(_info, _canvas.GetPixels(), new SKCodecOptions(index));

            // GIFs commonly carry 0–10ms delays that browsers treat as 100ms.
            return ClampDelay(TimeSpan.FromMilliseconds(_frames[index].Duration),
                TimeSpan.FromMilliseconds(11), TimeSpan.FromMilliseconds(100));
        }

        public override void Dispose()
        {
            _canvas.Dispose();
            _codec.Dispose();
        }
    }

    // ── APNG ─────────────────────────────────────────────────────────────

    private sealed class ApngFrames : AnimatedImageFrames
    {
        private enum FrameDisposal : byte { None = 0, Background = 1, Previous = 2 }
        private enum FrameBlend : byte { Source = 0, Over = 1 }

        private sealed record Frame(int Width, int Height, int X, int Y, TimeSpan Delay, FrameDisposal DisposeOp, FrameBlend BlendOp)
        {
            public List<ArraySegment<byte>> Data { get; } = new();
            public SKRectI Rect => SKRectI.Create(X, Y, Width, Height);
        }

        private readonly byte[] _ihdr;
        private readonly List<ArraySegment<byte>> _sharedChunks;
        private readonly List<Frame> _frames;
        private readonly SKBitmap _canvas;
        private readonly SKCanvas _drawing;
        private SKBitmap? _snapshot;

        private ApngFrames(byte[] ihdr, List<ArraySegment<byte>> sharedChunks, List<Frame> frames, int width, int height)
        {
            _ihdr = ihdr;
            _sharedChunks = sharedChunks;
            _frames = frames;
            _canvas = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            _drawing = new SKCanvas(_canvas);
        }

        public override int Width => _canvas.Width;
        public override int Height => _canvas.Height;
        public override int FrameCount => _frames.Count;
        public override SKBitmap Canvas => _canvas;

        public static ApngFrames? TryCreate(byte[] png)
        {
            byte[]? ihdr = null;
            var shared = new List<ArraySegment<byte>>();
            var frames = new List<Frame>();
            Frame? current = null;
            var seenImageData = false;
            var position = 8;

            while (position + 12 <= png.Length)
            {
                var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position));

                if (length < 0 || position + 12 + length > png.Length)
                    break;

                var type = Encoding.ASCII.GetString(png, position + 4, 4);
                var data = new ArraySegment<byte>(png, position + 8, length);

                switch (type)
                {
                    case "IHDR":
                        ihdr = data.ToArray();
                        break;

                    case "acTL":
                        break;

                    case "fcTL" when length >= 26:
                        var span = data.AsSpan();
                        var delayNum = BinaryPrimitives.ReadUInt16BigEndian(span[20..]);
                        var delayDen = BinaryPrimitives.ReadUInt16BigEndian(span[22..]);
                        var delay = TimeSpan.FromSeconds(delayNum / (double)(delayDen == 0 ? 100 : delayDen));

                        current = new Frame(
                            Width: (int)BinaryPrimitives.ReadUInt32BigEndian(span[4..]),
                            Height: (int)BinaryPrimitives.ReadUInt32BigEndian(span[8..]),
                            X: (int)BinaryPrimitives.ReadUInt32BigEndian(span[12..]),
                            Y: (int)BinaryPrimitives.ReadUInt32BigEndian(span[16..]),
                            Delay: ClampDelay(delay, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(100)),
                            DisposeOp: (FrameDisposal)Math.Min(span[24], (byte)2),
                            BlendOp: (FrameBlend)Math.Min(span[25], (byte)1));
                        frames.Add(current);
                        break;

                    // With no fcTL ahead of it, the IDAT image is a fallback for viewers that don't
                    // animate, not part of the animation; current is null then, so it's skipped.
                    case "IDAT":
                        seenImageData = true;
                        current?.Data.Add(data);
                        break;

                    case "fdAT" when length > 4:
                        current?.Data.Add(data[4..]); // after the sequence number
                        break;

                    case "IEND":
                        position = png.Length;
                        continue;

                    default:
                        // Palette, transparency, gamma, colour profile… apply to every frame.
                        if (!seenImageData)
                            shared.Add(new ArraySegment<byte>(png, position, length + 12));
                        break;
                }

                position += length + 12;
            }

            if (ihdr is not { Length: 13 })
                return null;

            frames.RemoveAll(f => f.Data.Count == 0 || f.Width <= 0 || f.Height <= 0);

            if (frames.Count < 2)
                return null;

            var width = (int)BinaryPrimitives.ReadUInt32BigEndian(ihdr);
            var height = (int)BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(4));

            return width > 0 && height > 0 ? new ApngFrames(ihdr, shared, frames, width, height) : null;
        }

        public override TimeSpan Render(int index)
        {
            var frame = _frames[index];

            if (index == 0)
            {
                // Each loop starts from a transparent canvas.
                _canvas.Erase(SKColors.Transparent);
            }
            else
            {
                var previous = _frames[index - 1];

                if (previous.DisposeOp == FrameDisposal.Previous && _snapshot != null)
                {
                    using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
                    _drawing.DrawBitmap(_snapshot, previous.Rect, previous.Rect, paint);
                }
                else if (previous.DisposeOp != FrameDisposal.None)
                {
                    ClearRect(previous.Rect);
                }
            }

            if (frame.DisposeOp == FrameDisposal.Previous)
            {
                _snapshot ??= new SKBitmap(_canvas.Info);
                _canvas.CopyTo(_snapshot);
            }

            using (var bitmap = DecodeFrame(frame))
            {
                if (bitmap != null)
                {
                    using var paint = new SKPaint { BlendMode = frame.BlendOp == FrameBlend.Source ? SKBlendMode.Src : SKBlendMode.SrcOver };
                    _drawing.DrawBitmap(bitmap, frame.X, frame.Y, paint);
                }
            }

            _drawing.Flush();

            return frame.Delay;
        }

        private void ClearRect(SKRectI rect)
        {
            _drawing.Save();
            _drawing.ClipRect(rect);
            _drawing.Clear(SKColors.Transparent);
            _drawing.Restore();
        }

        /// <summary>Rebuilds the frame as a self-contained PNG: the image's header at the frame's size, its shared chunks, and the frame's data as IDAT.</summary>
        private SKBitmap? DecodeFrame(Frame frame)
        {
            var dataLength = 0;
            foreach (var segment in frame.Data)
                dataLength += segment.Count;

            using var png = new MemoryStream(8 + 25 + dataLength + 12 + 12 + 1024);
            png.Write(PngSignature);

            var ihdr = (byte[])_ihdr.Clone();
            BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)frame.Width);
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)frame.Height);
            WriteChunk(png, "IHDR"u8, [ihdr]);

            foreach (var chunk in _sharedChunks)
                png.Write(chunk);

            WriteChunk(png, "IDAT"u8, frame.Data);
            WriteChunk(png, "IEND"u8, []);

            return SKBitmap.Decode(png.ToArray());
        }

        private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, IReadOnlyList<ArraySegment<byte>> data)
        {
            var length = 0;
            foreach (var segment in data)
                length += segment.Count;

            Span<byte> word = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(word, (uint)length);
            stream.Write(word);
            stream.Write(type);

            var crc = Crc32.Update(Crc32.Initial, type);

            foreach (var segment in data)
            {
                stream.Write(segment);
                crc = Crc32.Update(crc, segment);
            }

            BinaryPrimitives.WriteUInt32BigEndian(word, crc ^ 0xFFFFFFFFu);
            stream.Write(word);
        }

        public override void Dispose()
        {
            _drawing.Dispose();
            _canvas.Dispose();
            _snapshot?.Dispose();
        }
    }

    /// <summary>The PNG chunk CRC (ISO 3309 / zlib CRC-32).</summary>
    private static class Crc32
    {
        public const uint Initial = 0xFFFFFFFFu;

        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];

            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }

            return table;
        }

        public static uint Update(uint crc, ReadOnlySpan<byte> bytes)
        {
            foreach (var b in bytes)
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }
}
