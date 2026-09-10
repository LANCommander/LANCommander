using SixLabors.ImageSharp.PixelFormats;

namespace LANCommander.Server.Tests.Helpers;

/// <summary>
/// Constructs raw DIB (Device Independent Bitmap) byte arrays that can be stored as RT_ICON
/// resources in a synthetic PE file.
///
/// PE icon DIBs differ from standalone BMPs in two ways:
///   1. No BITMAPFILEHEADER prefix.
///   2. biHeight in the BITMAPINFOHEADER is 2× the actual icon height: the first half is the
///      XOR (colour) mask and the second half is the 1-bpp AND (transparency) mask.
/// </summary>
internal static class DibBuilder
{
    // ── 32 bpp ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a 32 bpp DIB where each pixel's alpha byte is non-zero.
    /// PEIconExtractor should detect the non-zero alpha and use the BGRA channel directly.
    /// </summary>
    public static byte[] Dib32WithAlpha(int width, int height, Func<int, int, Rgba32> pixel)
        => BuildDib32(width, height, pixel, embedAlpha: true);

    /// <summary>
    /// Builds a 32 bpp DIB where every alpha byte is zero (legacy style).
    /// PEIconExtractor should fall back to the AND mask for transparency.
    /// </summary>
    public static byte[] Dib32WithAndMask(int width, int height, Func<int, int, Rgba32> pixel)
        => BuildDib32(width, height, pixel, embedAlpha: false);

    private static byte[] BuildDib32(int width, int height, Func<int, int, Rgba32> pixel, bool embedAlpha)
    {
        int xorRowBytes = ((32 * width + 31) / 32) * 4;
        int andRowBytes = ((     width + 31) / 32) * 4;

        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);

        WriteBitmapInfoHeader(w, width, height, biBitCount: 32, biClrUsed: 0);

        // XOR mask (colour data, bottom-up)
        for (int y = height - 1; y >= 0; y--)
        {
            int written = 0;
            for (int x = 0; x < width; x++)
            {
                var p = pixel(x, y);
                w.Write(p.B); w.Write(p.G); w.Write(p.R);
                w.Write(embedAlpha ? p.A : (byte)0);
                written += 4;
            }
            WritePadding(w, written, xorRowBytes);
        }

        // AND mask (1 bpp, transparent = 1, bottom-up)
        for (int y = height - 1; y >= 0; y--)
        {
            var andRow = new byte[andRowBytes];
            if (!embedAlpha) // when alpha is zeroed, derive transparency from the pixel's original alpha
                for (int x = 0; x < width; x++)
                    if (pixel(x, y).A == 0)
                        andRow[x / 8] |= (byte)(0x80 >> (x % 8));
            w.Write(andRow);
        }

        return ms.ToArray();
    }

    // ── 24 bpp ────────────────────────────────────────────────────────────────

    /// <summary>Builds a 24 bpp DIB. Transparency comes from the AND mask (alpha channel → transparent).</summary>
    public static byte[] Dib24(int width, int height, Func<int, int, Rgba32> pixel)
    {
        int xorRowBytes = ((24 * width + 31) / 32) * 4;
        int andRowBytes = ((     width + 31) / 32) * 4;

        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);

        WriteBitmapInfoHeader(w, width, height, biBitCount: 24, biClrUsed: 0);

        for (int y = height - 1; y >= 0; y--)
        {
            int written = 0;
            for (int x = 0; x < width; x++)
            {
                var p = pixel(x, y);
                w.Write(p.B); w.Write(p.G); w.Write(p.R);
                written += 3;
            }
            WritePadding(w, written, xorRowBytes);
        }

        for (int y = height - 1; y >= 0; y--)
        {
            var andRow = new byte[andRowBytes];
            for (int x = 0; x < width; x++)
                if (pixel(x, y).A == 0)
                    andRow[x / 8] |= (byte)(0x80 >> (x % 8));
            w.Write(andRow);
        }

        return ms.ToArray();
    }

    // ── 8 bpp ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an 8 bpp (paletted) DIB.
    /// </summary>
    /// <param name="palette">Up to 256 colours; entries are RGBQUAD order (B, G, R, 0 written to file).</param>
    /// <param name="index">Returns the palette index for pixel (x, y).</param>
    /// <param name="transparent">Returns true when pixel (x, y) should be transparent (AND mask = 1).</param>
    public static byte[] Dib8(int width, int height,
        Rgba32[] palette,
        Func<int, int, int> index,
        Func<int, int, bool> transparent)
        => BuildPalettedDib(width, height, biBitCount: 8,
            palette: palette, getIndex: index, isTransparent: transparent);

    // ── 4 bpp ─────────────────────────────────────────────────────────────────

    /// <summary>Builds a 4 bpp (paletted, up to 16 colours) DIB.</summary>
    public static byte[] Dib4(int width, int height,
        Rgba32[] palette,
        Func<int, int, int> index,
        Func<int, int, bool> transparent)
        => BuildPalettedDib(width, height, biBitCount: 4,
            palette: palette, getIndex: index, isTransparent: transparent);

    // ── 1 bpp ─────────────────────────────────────────────────────────────────

    /// <summary>Builds a 1 bpp (2-colour) DIB.</summary>
    public static byte[] Dib1(int width, int height,
        Rgba32[] palette,  // exactly 2 entries
        Func<int, int, int> index,
        Func<int, int, bool> transparent)
        => BuildPalettedDib(width, height, biBitCount: 1,
            palette: palette, getIndex: index, isTransparent: transparent);

    // ── Common paletted path ──────────────────────────────────────────────────

    private static byte[] BuildPalettedDib(int width, int height, ushort biBitCount,
        Rgba32[] palette, Func<int, int, int> getIndex, Func<int, int, bool> isTransparent)
    {
        int xorRowBytes = ((biBitCount * width + 31) / 32) * 4;
        int andRowBytes = ((            width + 31) / 32) * 4;

        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);

        WriteBitmapInfoHeader(w, width, height, biBitCount, biClrUsed: (uint)palette.Length);

        // Palette (RGBQUAD: B, G, R, Reserved)
        foreach (var c in palette) { w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write((byte)0); }

        // XOR mask (bottom-up)
        for (int y = height - 1; y >= 0; y--)
        {
            var xorRow = new byte[xorRowBytes];
            for (int x = 0; x < width; x++)
            {
                int idx = getIndex(x, y);
                switch (biBitCount)
                {
                    case 8:
                        xorRow[x] = (byte)idx;
                        break;
                    case 4:
                        if (x % 2 == 0)
                            xorRow[x / 2] |= (byte)((idx & 0x0F) << 4);
                        else
                            xorRow[x / 2] |= (byte)(idx & 0x0F);
                        break;
                    case 1:
                        if (idx != 0)
                            xorRow[x / 8] |= (byte)(0x80 >> (x % 8));
                        break;
                }
            }
            w.Write(xorRow);
        }

        // AND mask (1 bpp, bottom-up)
        for (int y = height - 1; y >= 0; y--)
        {
            var andRow = new byte[andRowBytes];
            for (int x = 0; x < width; x++)
                if (isTransparent(x, y))
                    andRow[x / 8] |= (byte)(0x80 >> (x % 8));
            w.Write(andRow);
        }

        return ms.ToArray();
    }

    // ── Header helper ─────────────────────────────────────────────────────────

    private static void WriteBitmapInfoHeader(BinaryWriter w, int width, int height,
        ushort biBitCount, uint biClrUsed)
    {
        w.Write((uint)40);          // biSize
        w.Write((int)width);        // biWidth
        w.Write((int)(height * 2)); // biHeight — 2× because PE icons include XOR + AND masks
        w.Write((ushort)1);         // biPlanes
        w.Write(biBitCount);        // biBitCount
        w.Write((uint)0);           // biCompression = BI_RGB
        w.Write((uint)0);           // biSizeImage
        w.Write((int)0);            // biXPelsPerMeter
        w.Write((int)0);            // biYPelsPerMeter
        w.Write(biClrUsed);         // biClrUsed
        w.Write((uint)0);           // biClrImportant
    }

    private static void WritePadding(BinaryWriter w, int written, int rowBytes)
    {
        for (int i = written; i < rowBytes; i++) w.Write((byte)0);
    }
}
