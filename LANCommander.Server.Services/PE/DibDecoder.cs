using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace LANCommander.Server.Services.PE;

/// <summary>
/// Decodes the raw pixel data of an icon image (an RT_ICON resource, or a frame inside an ICO file)
/// into a 32-bit RGBA image.
/// </summary>
/// <remarks>
/// An icon image is either a PNG stream (Vista+ high resolution icons) or a DIB (Device Independent Bitmap).
/// Icon DIBs differ from standalone BMP files in two ways:
/// <list type="number">
///   <item>There is no BITMAPFILEHEADER prefix.</item>
///   <item>
///     <c>biHeight</c> in the BITMAPINFOHEADER is twice the actual pixel height: the first half
///     is the XOR (colour) mask and the second half is the 1-bit AND (transparency) mask.
///   </item>
/// </list>
/// Supported bit depths: 32, 24, 8, 4, and 1.
/// </remarks>
internal static class DibDecoder
{
    /// <summary>Decodes a single icon image, dispatching on the data's magic bytes.</summary>
    public static Image<Rgba32> Decode(byte[] data) =>
        IsPng(data)
            ? Image.Load<Rgba32>(new MemoryStream(data))
            : DecodeDib(data);

    /// <summary>Returns true when <paramref name="data"/> starts with the PNG signature.</summary>
    public static bool IsPng(ReadOnlySpan<byte> data) =>
        data.Length >= 8 &&
        data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
        data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;

    public static Image<Rgba32> DecodeDib(byte[] dibData)
    {
        if (dibData.Length < 40)
            throw new InvalidDataException("DIB resource data is too small to contain a valid BITMAPINFOHEADER.");

        using var ms     = new MemoryStream(dibData);
        using var reader = new BinaryReader(ms, Encoding.Default, leaveOpen: false);

        // BITMAPINFOHEADER
        uint   biSize        = reader.ReadUInt32();  // typically 40
        int    biWidth       = reader.ReadInt32();
        int    biHeightRaw   = reader.ReadInt32();   // 2x actual icon height (XOR + AND masks)
        reader.ReadUInt16();                         // biPlanes
        ushort biBitCount    = reader.ReadUInt16();
        reader.ReadUInt32();                         // biCompression
        reader.ReadUInt32();                         // biSizeImage
        reader.ReadInt32();                          // biXPelsPerMeter
        reader.ReadInt32();                          // biYPelsPerMeter
        uint   biClrUsed     = reader.ReadUInt32();
        reader.ReadUInt32();                         // biClrImportant

        // Skip any extended header bytes (e.g. BITMAPV4 / BITMAPV5)
        if (biSize > 40) ms.Position = biSize;

        // Icon pixel height is half of biHeight (XOR + AND together)
        int height = Math.Abs(biHeightRaw) / 2;
        if (height == 0) height = Math.Abs(biHeightRaw);
        int width = biWidth;

        if (width <= 0 || height <= 0)
            throw new InvalidDataException($"Invalid DIB dimensions: {width}x{height}.");

        // Colour table (palette)
        uint[] palette = Array.Empty<uint>();
        if (biBitCount <= 8)
        {
            int numColors = biClrUsed > 0 ? (int)biClrUsed : (1 << biBitCount);
            palette = new uint[numColors];
            for (int i = 0; i < numColors; i++)
            {
                byte palB = reader.ReadByte();
                byte palG = reader.ReadByte();
                byte palR = reader.ReadByte();
                reader.ReadByte(); // Reserved
                // Store as packed ARGB (A=255 placeholder; transparency comes from the AND mask)
                palette[i] = (uint)((255 << 24) | (palR << 16) | (palG << 8) | palB);
            }
        }

        // XOR (colour) mask. Each row is zero-padded to a 4-byte (DWORD) boundary.
        int xorRowBytes = ((biBitCount * width + 31) / 32) * 4;
        byte[] xorData  = reader.ReadBytes(xorRowBytes * height);

        // AND (transparency) mask. Always 1 bpp; each row padded to a 4-byte boundary.
        int andRowBytes = ((width + 31) / 32) * 4;
        byte[] andData  = reader.ReadBytes(andRowBytes * height);

        // For 32 bpp: modern icons embed alpha in the fourth byte.
        // Legacy 32 bpp icons set all alpha bytes to 0 and rely on the AND mask instead.
        bool use32BppAlpha = biBitCount == 32 && HasNonZeroAlpha(xorData, width, height, xorRowBytes);

        var image = new Image<Rgba32>(width, height);

        for (int y = 0; y < height; y++)
        {
            // DIBs are stored bottom-up
            int srcY = height - 1 - y;

            for (int x = 0; x < width; x++)
            {
                bool andOpaque = true;
                if (andData.Length > 0)
                {
                    int andByte = srcY * andRowBytes + x / 8;
                    int andBit  = 7 - (x % 8);
                    if (andByte < andData.Length)
                        andOpaque = ((andData[andByte] >> andBit) & 1) == 0; // 0 = opaque, 1 = transparent
                }

                byte pr, pg, pb, pa;

                switch (biBitCount)
                {
                    case 32:
                    {
                        int o = srcY * xorRowBytes + x * 4;
                        pb = xorData[o];
                        pg = xorData[o + 1];
                        pr = xorData[o + 2];
                        pa = use32BppAlpha ? xorData[o + 3] : (andOpaque ? (byte)255 : (byte)0);
                        break;
                    }
                    case 24:
                    {
                        int o = srcY * xorRowBytes + x * 3;
                        pb = xorData[o];
                        pg = xorData[o + 1];
                        pr = xorData[o + 2];
                        pa = andOpaque ? (byte)255 : (byte)0;
                        break;
                    }
                    case 8:
                    {
                        int idx = xorData[srcY * xorRowBytes + x];
                        uint c  = (uint)(idx < palette.Length ? palette[idx] : 0);
                        pb = (byte)( c        & 0xFF);
                        pg = (byte)((c >>  8) & 0xFF);
                        pr = (byte)((c >> 16) & 0xFF);
                        pa = andOpaque ? (byte)255 : (byte)0;
                        break;
                    }
                    case 4:
                    {
                        int  byteIdx = srcY * xorRowBytes + x / 2;
                        int  nibble  = (x % 2 == 0) ? ((xorData[byteIdx] >> 4) & 0x0F)
                                                    : ( xorData[byteIdx]       & 0x0F);
                        uint c = (uint)(nibble < palette.Length ? palette[nibble] : 0);
                        pb = (byte)( c        & 0xFF);
                        pg = (byte)((c >>  8) & 0xFF);
                        pr = (byte)((c >> 16) & 0xFF);
                        pa = andOpaque ? (byte)255 : (byte)0;
                        break;
                    }
                    case 1:
                    {
                        int  byteIdx = srcY * xorRowBytes + x / 8;
                        int  bitPos  = 7 - (x % 8);
                        int  idx     = (xorData[byteIdx] >> bitPos) & 1;
                        uint c = (uint)(idx < palette.Length ? palette[idx]
                                                            : (idx != 0 ? 0x00FFFFFFu : 0x00000000u));
                        pb = (byte)( c        & 0xFF);
                        pg = (byte)((c >>  8) & 0xFF);
                        pr = (byte)((c >> 16) & 0xFF);
                        pa = andOpaque ? (byte)255 : (byte)0;
                        break;
                    }
                    default:
                        pr = pg = pb = 0;
                        pa = andOpaque ? (byte)255 : (byte)0;
                        break;
                }

                image[x, y] = new Rgba32(pr, pg, pb, pa);
            }
        }

        return image;
    }

    /// <summary>Returns true if any pixel in the 32 bpp XOR mask has a non-zero alpha byte.</summary>
    private static bool HasNonZeroAlpha(byte[] xorData, int width, int height, int rowBytes)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width;  x++)
        {
            int alphaOffset = y * rowBytes + x * 4 + 3;
            if (alphaOffset < xorData.Length && xorData[alphaOffset] != 0)
                return true;
        }
        return false;
    }
}
