using System.Runtime.InteropServices;
using LANCommander.Server.Services.PE;
using LANCommander.Server.Tests.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LANCommander.Server.Tests.Services;

// These are pure in-memory unit tests that need no fixture, but they join the "Application" collection
// anyway: the collection's shared authentication token is not safe against tests running in other
// collections concurrently, and leaving these to run in parallel makes VersioningTests fail with a 401.

// ─────────────────────────────────────────────────────────────────────────────
//  Helper to reduce boilerplate in tests that need a synthetic PE
// ─────────────────────────────────────────────────────────────────────────────
file static class PEFixture
{
    /// <summary>4×4 32 bpp DIB with known pixel values (all semi-transparent red).</summary>
    public static byte[] SimpleRedDib(int width = 4, int height = 4) =>
        DibBuilder.Dib32WithAlpha(width, height, (_, _) => new Rgba32(255, 0, 0, 128));

    /// <summary>Minimal 1×1 PNG (a red pixel).</summary>
    public static byte[] MinimalPng()
    {
        using var img = new Image<Rgba32>(1, 1);
        img[0, 0] = new Rgba32(255, 0, 0, 255);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>Builds a PE containing one 4×4 32 bpp icon in one group.</summary>
    public static byte[] SingleGroupPe(
        int iconId    = 1,
        int groupId   = 100,
        byte width    = 4,
        byte height   = 4,
        ushort bits   = 32,
        byte[]? dib   = null)
    {
        var data = dib ?? SimpleRedDib(width, height);
        return PEBuilder.Build(
            new Dictionary<int, byte[]> { [iconId] = data },
            [(groupId, [(iconId, width, height, bits)])]);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Constructor / input validation
// ─────────────────────────────────────────────────────────────────────────────
[Collection("Application")]
public class PEIconExtractorConstructorTests
{
    [Fact]
    public void NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PEIconExtractor((Stream)null!));
    }

    [Fact]
    public void NonSeekableStream_IsReadIntoMemory()
    {
        // Upload streams (e.g. Blazor's InputFile) cannot seek, so the extractor buffers whatever it is given.
        using var inner = new MemoryStream(PEFixture.SingleGroupPe());
        using var ns    = new NonSeekableStream(inner);

        using var extractor = new PEIconExtractor(ns);

        Assert.Single(extractor.GetIcons());
    }

    [Fact]
    public void NonExistentFilePath_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(
            () => new PEIconExtractor(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  GetIcons — PE structure parsing
// ─────────────────────────────────────────────────────────────────────────────
[Collection("Application")]
public class PEIconExtractorGetIconsTests
{
    [Fact]
    public void InvalidMzSignature_ThrowsInvalidDataException()
    {
        var bad = new byte[512];
        bad[0] = 0xFF; bad[1] = 0xFF; // not "MZ"

        using var extractor = new PEIconExtractor(new MemoryStream(bad));

        Assert.Throws<InvalidDataException>(() => extractor.GetIcons());
    }

    [Fact]
    public void NoResourceSection_ReturnsEmptyList()
    {
        var pe = PEBuilder.BuildWithoutResources();

        using var extractor = new PEIconExtractor(new MemoryStream(pe));

        Assert.Empty(extractor.GetIcons());
    }

    [Fact]
    public void SingleGroup_SingleImage_ReturnsExactlyOneIconInfo()
    {
        var pe = PEFixture.SingleGroupPe();

        using var extractor = new PEIconExtractor(new MemoryStream(pe));

        Assert.Single(extractor.GetIcons());
    }

    [Fact]
    public void IconInfo_HasExpectedGroupId()
    {
        var pe = PEFixture.SingleGroupPe(groupId: 42);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        var icon = extractor.GetIcons()[0];

        Assert.Equal(42, icon.GroupId);
    }

    [Fact]
    public void IconInfo_HasExpectedImageId()
    {
        var pe = PEFixture.SingleGroupPe(iconId: 7);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        var icon = extractor.GetIcons()[0];

        Assert.Equal(7, icon.ImageId);
    }

    [Fact]
    public void IconInfo_HasExpectedWidth()
    {
        var pe = PEFixture.SingleGroupPe(width: 32);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        var icon = extractor.GetIcons()[0];

        Assert.Equal(32, icon.Width);
    }

    [Fact]
    public void IconInfo_HasExpectedHeight()
    {
        var pe = PEFixture.SingleGroupPe(height: 32);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        var icon = extractor.GetIcons()[0];

        Assert.Equal(32, icon.Height);
    }

    [Fact]
    public void IconInfo_WidthZeroInHeader_ReportedAs256()
    {
        // The PE convention for 256×256 icons is to store 0 in the width/height byte.
        // PEIconExtractor must translate 0 → 256.
        var dib = DibBuilder.Dib32WithAlpha(256, 256, (_, _) => new Rgba32(0, 0, 0, 255));
        var pe  = PEFixture.SingleGroupPe(width: 0, height: 0, dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        var icon = extractor.GetIcons()[0];

        Assert.Equal(256, icon.Width);
        Assert.Equal(256, icon.Height);
    }

    [Fact]
    public void IconInfo_HasExpectedBitCount()
    {
        var dib = DibBuilder.Dib32WithAlpha(4, 4, (_, _) => new Rgba32(0, 0, 0, 255));
        var pe  = PEFixture.SingleGroupPe(bits: 32, dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        var icon = extractor.GetIcons()[0];

        Assert.Equal(32, icon.BitCount);
    }

    [Fact]
    public void DibIcon_IsPng_IsFalse()
    {
        var pe = PEFixture.SingleGroupPe();

        using var extractor = new PEIconExtractor(new MemoryStream(pe));

        Assert.False(extractor.GetIcons()[0].IsPng);
    }

    [Fact]
    public void PngIcon_IsPng_IsTrue()
    {
        var png = PEFixture.MinimalPng();
        var pe  = PEFixture.SingleGroupPe(dib: png);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));

        Assert.True(extractor.GetIcons()[0].IsPng);
    }

    [Fact]
    public void MultipleGroups_AllGroupsReturned()
    {
        var dib1 = DibBuilder.Dib32WithAlpha(4, 4, (_, _) => new Rgba32(255, 0, 0, 255));
        var dib2 = DibBuilder.Dib32WithAlpha(8, 8, (_, _) => new Rgba32(0, 255, 0, 255));

        var pe = PEBuilder.Build(
            new Dictionary<int, byte[]> { [1] = dib1, [2] = dib2 },
            [
                (10, [(1, 4, 4, (ushort)32)]),
                (20, [(2, 8, 8, (ushort)32)]),
            ]);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        var icons = extractor.GetIcons();

        Assert.Equal(2, icons.Count);
        Assert.Contains(icons, i => i.GroupId == 10);
        Assert.Contains(icons, i => i.GroupId == 20);
    }

    [Fact]
    public void SingleGroup_MultipleImages_AllImagesReturned()
    {
        var dib32 = DibBuilder.Dib32WithAlpha(32, 32, (_, _) => new Rgba32(0, 0, 255, 255));
        var dib16 = DibBuilder.Dib32WithAlpha(16, 16, (_, _) => new Rgba32(0, 0, 255, 255));

        var pe = PEBuilder.Build(
            new Dictionary<int, byte[]> { [1] = dib32, [2] = dib16 },
            [(1, [(1, 32, 32, (ushort)32), (2, 16, 16, (ushort)32)])]);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        var icons = extractor.GetIcons();

        Assert.Equal(2, icons.Count);
    }

    [Fact]
    public void CalledTwice_ReturnsSameResults()
    {
        // Verifies that EnsureParsed caching works correctly.
        var pe = PEFixture.SingleGroupPe();
        using var extractor = new PEIconExtractor(new MemoryStream(pe));

        var first  = extractor.GetIcons();
        var second = extractor.GetIcons();

        Assert.Equal(first.Count, second.Count);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  GetIcon — DIB decoding
// ─────────────────────────────────────────────────────────────────────────────
[Collection("Application")]
public class PEIconExtractorGetIconTests
{
    [Fact]
    public void UnknownImageId_ThrowsInvalidOperationException()
    {
        var pe = PEFixture.SingleGroupPe(iconId: 1);
        using var extractor = new PEIconExtractor(new MemoryStream(pe));

        var badInfo = extractor.GetIcons()[0] with { ImageId = 999 };

        Assert.Throws<InvalidOperationException>(() => extractor.GetIcon(badInfo));
    }

    [Fact]
    public void NullIconInfo_ThrowsArgumentNullException()
    {
        var pe = PEFixture.SingleGroupPe();
        using var extractor = new PEIconExtractor(new MemoryStream(pe));

        Assert.Throws<ArgumentNullException>(() => extractor.GetIcon(null!));
    }

    // ── 32 bpp with embedded alpha ───────────────────────────────────────────

    [Fact]
    public void Dib32_WithAlpha_CorrectDimensions()
    {
        var pe = PEFixture.SingleGroupPe(width: 4, height: 4);
        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        var icon = extractor.GetIcons()[0];

        using var img = extractor.GetIcon(icon);

        Assert.Equal(4, img.Width);
        Assert.Equal(4, img.Height);
    }

    [Fact]
    public void Dib32_WithAlpha_PixelColourCorrect()
    {
        // Every pixel = (R=200, G=100, B=50, A=255)
        var dib = DibBuilder.Dib32WithAlpha(4, 4, (_, _) => new Rgba32(200, 100, 50, 255));
        var pe  = PEFixture.SingleGroupPe(dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
        {
            var p = img[x, y];
            Assert.Equal(200, p.R);
            Assert.Equal(100, p.G);
            Assert.Equal(50,  p.B);
            Assert.Equal(255, p.A);
        }
    }

    [Fact]
    public void Dib32_WithAlpha_TransparentPixel_AlphaPreserved()
    {
        // Checkerboard: even pixels opaque red, odd pixels fully transparent
        var dib = DibBuilder.Dib32WithAlpha(4, 4,
            (x, y) => (x + y) % 2 == 0 ? new Rgba32(255, 0, 0, 255) : new Rgba32(0, 0, 0, 0));
        var pe = PEFixture.SingleGroupPe(dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(255, img[0, 0].A); // opaque
        Assert.Equal(0,   img[1, 0].A); // transparent
    }

    // ── 32 bpp with zero alpha → fall back to AND mask ───────────────────────

    [Fact]
    public void Dib32_AndMask_OpaquePixel_AlphaIs255()
    {
        // Alpha byte zeroed; pixel (0,0) is opaque (AND mask bit = 0)
        var dib = DibBuilder.Dib32WithAndMask(2, 2,
            (x, y) => new Rgba32(255, 0, 0, 255)); // alpha=255 used for AND mask, but NOT stored in pixel
        var pe = PEFixture.SingleGroupPe(dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(255, img[0, 0].A);
    }

    [Fact]
    public void Dib32_AndMask_TransparentPixel_AlphaIsZero()
    {
        // alpha=0 pixels should become transparent via AND mask
        var dib = DibBuilder.Dib32WithAndMask(2, 2,
            (x, y) => x == 0 && y == 0 ? new Rgba32(0, 0, 0, 0) : new Rgba32(255, 0, 0, 255));
        var pe = PEFixture.SingleGroupPe(dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(0,   img[0, 0].A);
        Assert.Equal(255, img[1, 0].A);
    }

    // ── 24 bpp ────────────────────────────────────────────────────────────────

    [Fact]
    public void Dib24_OpaquePixels_RgbCorrect()
    {
        var dib = DibBuilder.Dib24(4, 4, (_, _) => new Rgba32(10, 20, 30, 255));
        var pe  = PEFixture.SingleGroupPe(bits: 24, dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(10, img[0, 0].R);
        Assert.Equal(20, img[0, 0].G);
        Assert.Equal(30, img[0, 0].B);
        Assert.Equal(255, img[0, 0].A);
    }

    [Fact]
    public void Dib24_AndMask_TransparentPixel_AlphaIsZero()
    {
        // Pixel (1,0) has alpha=0 → AND mask bit = 1 → decoded as transparent
        var dib = DibBuilder.Dib24(2, 2,
            (x, y) => x == 1 && y == 0 ? new Rgba32(255, 255, 255, 0) : new Rgba32(0, 0, 0, 255));
        var pe = PEFixture.SingleGroupPe(bits: 24, dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(0,   img[1, 0].A);
        Assert.Equal(255, img[0, 0].A);
    }

    // ── 8 bpp ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Dib8_PaletteLookup_RgbCorrect()
    {
        Rgba32[] palette = [new Rgba32(255, 0, 0, 255), new Rgba32(0, 255, 0, 255)];
        // Checkerboard: (0,0)→index 0 (red), (1,0)→index 1 (green)
        var dib = DibBuilder.Dib8(2, 2, palette,
            index:       (x, y) => (x + y) % 2,
            transparent: (x, y) => false);
        var pe = PEFixture.SingleGroupPe(bits: 8, dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(255, img[0, 0].R); Assert.Equal(0,   img[0, 0].G); // red
        Assert.Equal(0,   img[1, 0].R); Assert.Equal(255, img[1, 0].G); // green
    }

    [Fact]
    public void Dib8_AndMask_TransparentPixel_AlphaIsZero()
    {
        Rgba32[] palette = [new Rgba32(255, 0, 0, 255)];
        var dib = DibBuilder.Dib8(2, 2, palette,
            index:       (x, y) => 0,
            transparent: (x, y) => x == 1 && y == 0);
        var pe = PEFixture.SingleGroupPe(bits: 8, dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(0,   img[1, 0].A);
        Assert.Equal(255, img[0, 0].A);
    }

    // ── 4 bpp ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Dib4_PaletteLookup_RgbCorrect()
    {
        Rgba32[] palette =
        [
            new Rgba32(255, 0,   0,   255), // index 0 = red
            new Rgba32(0,   0,   255, 255), // index 1 = blue
        ];
        // 4×4: left half index 0 (red), right half index 1 (blue)
        var dib = DibBuilder.Dib4(4, 4, palette,
            index:       (x, _) => x < 2 ? 0 : 1,
            transparent: (_, _) => false);
        var pe = PEFixture.SingleGroupPe(bits: 4, dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(255, img[0, 0].R); Assert.Equal(0,   img[0, 0].B); // red
        Assert.Equal(0,   img[3, 0].R); Assert.Equal(255, img[3, 0].B); // blue
    }

    // ── 1 bpp ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Dib1_PaletteLookup_RgbCorrect()
    {
        Rgba32[] palette =
        [
            new Rgba32(0,   0, 0,   255), // index 0 = black
            new Rgba32(255, 255, 255, 255), // index 1 = white
        ];
        // Left column black, right column white
        var dib = DibBuilder.Dib1(4, 4, palette,
            index:       (x, _) => x < 2 ? 0 : 1,
            transparent: (_, _) => false);
        var pe = PEFixture.SingleGroupPe(bits: 1, dib: dib);

        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(0,   img[0, 0].R); // black
        Assert.Equal(255, img[3, 0].R); // white
    }

    // ── PNG icon ──────────────────────────────────────────────────────────────

    [Fact]
    public void PngIcon_DecodesCorrectly_CorrectDimensions()
    {
        // Build a 16×16 PNG in memory and embed it as an RT_ICON resource
        using var png = new Image<Rgba32>(16, 16);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            png[x, y] = new Rgba32(0, 128, 255, 255);

        using var pngMs = new MemoryStream();
        png.SaveAsPng(pngMs);
        byte[] pngBytes = pngMs.ToArray();

        var pe = PEFixture.SingleGroupPe(dib: pngBytes);
        using var extractor = new PEIconExtractor(new MemoryStream(pe));

        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(16, img.Width);
        Assert.Equal(16, img.Height);
    }

    [Fact]
    public void PngIcon_DecodesCorrectly_PixelColourPreserved()
    {
        using var png = new Image<Rgba32>(2, 2);
        png[0, 0] = new Rgba32(255, 0, 0, 255);   // red
        png[1, 0] = new Rgba32(0, 255, 0, 255);   // green
        png[0, 1] = new Rgba32(0, 0, 255, 255);   // blue
        png[1, 1] = new Rgba32(128, 0, 128, 255); // purple

        using var pngMs = new MemoryStream();
        png.SaveAsPng(pngMs);

        var pe = PEFixture.SingleGroupPe(dib: pngMs.ToArray());
        using var extractor = new PEIconExtractor(new MemoryStream(pe));
        using var img = extractor.GetIcon(extractor.GetIcons()[0]);

        Assert.Equal(255, img[0, 0].R); // red
        Assert.Equal(255, img[1, 0].G); // green
        Assert.Equal(255, img[0, 1].B); // blue
    }

    // ── File-path constructor ─────────────────────────────────────────────────

    [Fact]
    public void FilePathConstructor_ParsesCorrectly()
    {
        var pe   = PEFixture.SingleGroupPe();
        var path = Path.Combine(Path.GetTempPath(), $"lc-pe-test-{Guid.NewGuid()}.exe");
        File.WriteAllBytes(path, pe);
        try
        {
            using var extractor = new PEIconExtractor(path);
            Assert.Single(extractor.GetIcons());
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    [Fact]
    public void FilePathConstructor_DoesNotHoldTheFileOpen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lc-pe-disp-{Guid.NewGuid()}.exe");
        File.WriteAllBytes(path, PEFixture.SingleGroupPe());
        try
        {
            using var extractor = new PEIconExtractor(path);
            extractor.GetIcons();

            // The binary is copied into memory up front, so opening the file for exclusive
            // access must succeed even while the extractor is still alive.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(fs.CanRead);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BorrowedStream_NotDisposedWithExtractor()
    {
        var pe = PEFixture.SingleGroupPe();
        using var ms = new MemoryStream(pe);

        using (var extractor = new PEIconExtractor(ms))
            extractor.GetIcons();

        // MemoryStream should still be readable after the extractor is disposed.
        Assert.True(ms.CanRead);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  FromStreamAsync — bounded buffering
// ─────────────────────────────────────────────────────────────────────────────
[Collection("Application")]
public class PEIconExtractorFromStreamTests
{
    [Fact]
    public async Task WithinLimit_ParsesNormally()
    {
        var pe = PEFixture.SingleGroupPe();
        using var ms = new MemoryStream(pe);

        using var extractor = await PEIconExtractor.FromStreamAsync(ms, pe.Length);

        Assert.Single(extractor.GetIcons());
    }

    [Fact]
    public async Task OverLimit_ThrowsInvalidDataException()
    {
        var pe = PEFixture.SingleGroupPe();
        using var ms = new MemoryStream(pe);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => PEIconExtractor.FromStreamAsync(ms, pe.Length - 1));
    }

    [Fact]
    public async Task OverLimit_StopsReadingNearTheLimit()
    {
        // The limit has to be enforced while reading, not after — otherwise a huge upload is fully
        // buffered before it is rejected. Reads are chunked, so the cut-off lands within one buffer
        // of the limit rather than exactly on it.
        const int limit = 64 * 1024;

        using var inner = new MemoryStream(new byte[8 * 1024 * 1024]);
        using var counted = new CountingStream(inner);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => PEIconExtractor.FromStreamAsync(counted, limit));

        Assert.True(counted.BytesRead < 2 * limit, $"Read {counted.BytesRead} bytes for a {limit} byte limit.");
    }

    [Fact]
    public async Task NonPositiveLimit_ThrowsArgumentOutOfRangeException()
    {
        using var ms = new MemoryStream(PEFixture.SingleGroupPe());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => PEIconExtractor.FromStreamAsync(ms, 0));
    }

    [Fact]
    public async Task NonExecutableContent_ThrowsInvalidDataExceptionOnParse()
    {
        var png = PEFixture.MinimalPng();
        using var ms = new MemoryStream(png);

        using var extractor = await PEIconExtractor.FromStreamAsync(ms, png.Length);

        Assert.Throws<InvalidDataException>(() => extractor.GetIcons());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  HasExecutableHeader
// ─────────────────────────────────────────────────────────────────────────────
[Collection("Application")]
public class PEIconExtractorHeaderTests
{
    [Fact]
    public void MzSignature_IsRecognised()
    {
        Assert.True(PEIconExtractor.HasExecutableHeader(PEFixture.SingleGroupPe()));
    }

    [Fact]
    public void NonExecutable_IsRejected()
    {
        Assert.False(PEIconExtractor.HasExecutableHeader(PEFixture.MinimalPng()));
    }

    [Fact]
    public void EmptyData_IsRejected()
    {
        Assert.False(PEIconExtractor.HasExecutableHeader([]));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Icon group selection and ICO export
// ─────────────────────────────────────────────────────────────────────────────
[Collection("Application")]
public class PEIconExtractorExtractIconTests
{
    [Fact]
    public void NoIcons_PrimaryGroupIsNull()
    {
        using var extractor = new PEIconExtractor(PEBuilder.BuildWithoutResources());

        Assert.Null(extractor.GetPrimaryIconGroup());
    }

    [Fact]
    public void NoIcons_ExtractPrimaryIconReturnsNull()
    {
        using var extractor = new PEIconExtractor(PEBuilder.BuildWithoutResources());

        Assert.Null(extractor.ExtractPrimaryIcon());
    }

    [Fact]
    public void MultipleGroups_LowestIdIsPrimary()
    {
        // Windows indexes icon groups in resource directory order, so the lowest numeric ID wins.
        var dib = PEFixture.SimpleRedDib();
        var pe = PEBuilder.Build(
            new Dictionary<int, byte[]> { [1] = dib, [2] = dib },
            [
                (77, [(1, 4, 4, (ushort)32)]),
                (5,  [(2, 4, 4, (ushort)32)]),
            ]);

        using var extractor = new PEIconExtractor(pe);

        Assert.Equal(5, extractor.GetPrimaryIconGroup()!.Id);
    }

    [Fact]
    public void IconGroup_ExposesItsImages()
    {
        var dib32 = DibBuilder.Dib32WithAlpha(32, 32, (_, _) => new Rgba32(0, 0, 255, 255));
        var dib16 = DibBuilder.Dib32WithAlpha(16, 16, (_, _) => new Rgba32(0, 0, 255, 255));

        var pe = PEBuilder.Build(
            new Dictionary<int, byte[]> { [1] = dib32, [2] = dib16 },
            [(1, [(1, 32, 32, (ushort)32), (2, 16, 16, (ushort)32)])]);

        using var extractor = new PEIconExtractor(pe);
        var group = extractor.GetPrimaryIconGroup()!;

        Assert.Equal(2, group.Images.Count);
        Assert.Contains(group.Images, i => i.Width == 32);
        Assert.Contains(group.Images, i => i.Width == 16);
    }

    [Fact]
    public void ExtractedIco_HasIconFileHeader()
    {
        using var extractor = new PEIconExtractor(PEFixture.SingleGroupPe());

        var ico = extractor.ExtractPrimaryIcon()!;

        Assert.True(IconFile.HasIconHeader(ico));
    }

    [Fact]
    public void ExtractedIco_ContainsEveryImageInTheGroup()
    {
        var dib32 = DibBuilder.Dib32WithAlpha(32, 32, (_, _) => new Rgba32(0, 0, 255, 255));
        var dib16 = DibBuilder.Dib32WithAlpha(16, 16, (_, _) => new Rgba32(0, 0, 255, 255));

        var pe = PEBuilder.Build(
            new Dictionary<int, byte[]> { [1] = dib32, [2] = dib16 },
            [(1, [(1, 32, 32, (ushort)32), (2, 16, 16, (ushort)32)])]);

        using var extractor = new PEIconExtractor(pe);
        var ico = extractor.ExtractPrimaryIcon()!;

        Assert.Equal(2, BitConverter.ToUInt16(ico, 4)); // ICONDIR.Count
    }

    [Fact]
    public void ExtractedIco_OnlyContainsTheSelectedGroup()
    {
        var dib = PEFixture.SimpleRedDib();
        var pe = PEBuilder.Build(
            new Dictionary<int, byte[]> { [1] = dib, [2] = dib },
            [
                (5,  [(1, 4, 4, (ushort)32)]),
                (77, [(2, 4, 4, (ushort)32)]),
            ]);

        using var extractor = new PEIconExtractor(pe);
        var ico = extractor.ExtractPrimaryIcon()!;

        Assert.Equal(1, BitConverter.ToUInt16(ico, 4)); // ICONDIR.Count
    }

    [Fact]
    public void ExtractedIco_ImageOffsetsPointAtTheirPayloads()
    {
        var dib32 = DibBuilder.Dib32WithAlpha(32, 32, (_, _) => new Rgba32(0, 0, 255, 255));
        var dib16 = DibBuilder.Dib32WithAlpha(16, 16, (_, _) => new Rgba32(0, 0, 255, 255));

        var pe = PEBuilder.Build(
            new Dictionary<int, byte[]> { [1] = dib32, [2] = dib16 },
            [(1, [(1, 32, 32, (ushort)32), (2, 16, 16, (ushort)32)])]);

        using var extractor = new PEIconExtractor(pe);
        var ico = extractor.ExtractPrimaryIcon()!;

        // Directory entries start at byte 6 and are 16 bytes each; size is at +8 and offset at +12.
        var firstSize   = BitConverter.ToUInt32(ico, 6 + 8);
        var firstOffset = BitConverter.ToUInt32(ico, 6 + 12);
        var secondSize   = BitConverter.ToUInt32(ico, 6 + 16 + 8);
        var secondOffset = BitConverter.ToUInt32(ico, 6 + 16 + 12);

        Assert.Equal(6 + 2 * 16u, firstOffset);
        Assert.Equal((uint)dib32.Length, firstSize);
        Assert.Equal(firstOffset + firstSize, secondOffset);
        Assert.Equal((uint)dib16.Length, secondSize);
        Assert.Equal(secondOffset + secondSize, (uint)ico.Length);
    }

    [Fact]
    public void ExtractedIco_LargestFrameDecodesToTheOriginalPixels()
    {
        var dib32 = DibBuilder.Dib32WithAlpha(32, 32, (_, _) => new Rgba32(12, 34, 56, 255));
        var dib16 = DibBuilder.Dib32WithAlpha(16, 16, (_, _) => new Rgba32(200, 100, 50, 255));

        var pe = PEBuilder.Build(
            new Dictionary<int, byte[]> { [1] = dib16, [2] = dib32 },
            [(1, [(1, 16, 16, (ushort)32), (2, 32, 32, (ushort)32)])]);

        using var extractor = new PEIconExtractor(pe);
        var ico = extractor.ExtractPrimaryIcon()!;

        using var img = IconFile.DecodeLargestFrame(ico);

        Assert.Equal(32, img.Width);
        Assert.Equal(32, img.Height);
        Assert.Equal(12, img[0, 0].R);
        Assert.Equal(34, img[0, 0].G);
        Assert.Equal(56, img[0, 0].B);
    }

    [Fact]
    public void ExtractedIco_256PxImage_KeepsZeroDimensionConvention()
    {
        // A 256x256 image is stored with 0 in the width/height byte, in the PE group header and in the ICO alike.
        var dib = DibBuilder.Dib32WithAlpha(256, 256, (_, _) => new Rgba32(0, 0, 0, 255));
        var pe  = PEFixture.SingleGroupPe(width: 0, height: 0, dib: dib);

        using var extractor = new PEIconExtractor(pe);
        var ico = extractor.ExtractPrimaryIcon()!;

        Assert.Equal(0, ico[6]);     // ICONDIRENTRY.Width
        Assert.Equal(0, ico[6 + 1]); // ICONDIRENTRY.Height

        using var img = IconFile.DecodeLargestFrame(ico);

        Assert.Equal(256, img.Width);
    }

    [Fact]
    public void ExtractIcon_UnknownGroup_ThrowsInvalidOperationException()
    {
        using var extractor = new PEIconExtractor(PEFixture.SingleGroupPe());

        var stranger = new IconGroupInfo { Id = 999 };

        Assert.Throws<InvalidOperationException>(() => extractor.ExtractIcon(stranger));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  IconFile
// ─────────────────────────────────────────────────────────────────────────────
[Collection("Application")]
public class IconFileTests
{
    [Fact]
    public void HasIconHeader_RejectsPng()
    {
        Assert.False(IconFile.HasIconHeader(PEFixture.MinimalPng()));
    }

    [Fact]
    public void HasIconHeader_RejectsCursorType()
    {
        // Type 2 is a cursor, not an icon.
        byte[] cursor = [0x00, 0x00, 0x02, 0x00, 0x01, 0x00];

        Assert.False(IconFile.HasIconHeader(cursor));
    }

    [Fact]
    public void HasIconHeader_RejectsEmptyDirectory()
    {
        byte[] empty = [0x00, 0x00, 0x01, 0x00, 0x00, 0x00];

        Assert.False(IconFile.HasIconHeader(empty));
    }

    [Fact]
    public void DecodeLargestFrame_NonIconData_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => IconFile.DecodeLargestFrame(PEFixture.MinimalPng()));
    }

    [Fact]
    public void DecodeLargestFrame_TruncatedPayload_ThrowsInvalidDataException()
    {
        using var extractor = new PEIconExtractor(PEFixture.SingleGroupPe());
        var ico = extractor.ExtractPrimaryIcon()!;

        // Chop off the image data, leaving a directory that points past the end of the file.
        var truncated = ico.Take(6 + 16).ToArray();

        Assert.Throws<InvalidDataException>(() => IconFile.DecodeLargestFrame(truncated));
    }

    [Fact]
    public void DecodeLargestFrame_FromStream_MatchesByteOverload()
    {
        using var extractor = new PEIconExtractor(PEFixture.SingleGroupPe());
        var ico = extractor.ExtractPrimaryIcon()!;

        using var ms = new MemoryStream(ico);
        using var img = IconFile.DecodeLargestFrame(ms);

        Assert.Equal(4, img.Width);
        Assert.Equal(4, img.Height);
    }

    [Fact]
    public void DecodeLargestFrame_PrefersHigherColourDepthAtEqualSize()
    {
        var dib4 = DibBuilder.Dib4(16, 16, [new Rgba32(255, 0, 0, 255), new Rgba32(0, 0, 255, 255)],
            index:       (_, _) => 0,
            transparent: (_, _) => false);
        var dib32 = DibBuilder.Dib32WithAlpha(16, 16, (_, _) => new Rgba32(0, 255, 0, 255));

        var pe = PEBuilder.Build(
            new Dictionary<int, byte[]> { [1] = dib4, [2] = dib32 },
            [(1, [(1, 16, 16, (ushort)4), (2, 16, 16, (ushort)32)])]);

        using var extractor = new PEIconExtractor(pe);
        using var img = IconFile.DecodeLargestFrame(extractor.ExtractPrimaryIcon()!);

        Assert.Equal(255, img[0, 0].G); // the 32 bpp green image, not the 4 bpp red one
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Windows integration — real binaries (skipped on non-Windows)
// ─────────────────────────────────────────────────────────────────────────────
[Collection("Application")]
public class PEIconExtractorWindowsIntegrationTests
{
    private static readonly string NotepadPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");

    [Fact]
    public void Notepad_HasAtLeastOneIconGroup()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(NotepadPath))
            return;

        using var extractor = new PEIconExtractor(NotepadPath);

        Assert.NotEmpty(extractor.GetIcons());
    }

    [Fact]
    public void Notepad_ContainsBothDibAndPngIcons()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(NotepadPath))
            return;

        using var extractor = new PEIconExtractor(NotepadPath);
        var icons = extractor.GetIcons();

        Assert.Contains(icons, i => !i.IsPng);
        Assert.Contains(icons, i => i.IsPng);
    }

    [Fact]
    public void Notepad_AllIconsDecodeWithoutException()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(NotepadPath))
            return;

        using var extractor = new PEIconExtractor(NotepadPath);

        foreach (var info in extractor.GetIcons())
        {
            var ex = Record.Exception(() =>
            {
                using var img = extractor.GetIcon(info);
                Assert.True(img.Width > 0 && img.Height > 0);
            });
            Assert.Null(ex);
        }
    }

    [Fact]
    public void Notepad_IconDimensionsMatchExpected()
    {
        // notepad.exe ships with 16×16, 32×32, 48×48, and 256×256 variants
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(NotepadPath))
            return;

        using var extractor = new PEIconExtractor(NotepadPath);
        var icons = extractor.GetIcons();

        Assert.Contains(icons, i => i.Width == 16  && i.Height == 16);
        Assert.Contains(icons, i => i.Width == 32  && i.Height == 32);
        Assert.Contains(icons, i => i.Width == 48  && i.Height == 48);
        Assert.Contains(icons, i => i.Width == 256 && i.Height == 256);
    }

    [Fact]
    public void Notepad_PrimaryIconExportsToADecodableIco()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(NotepadPath))
            return;

        using var extractor = new PEIconExtractor(NotepadPath);
        var ico = extractor.ExtractPrimaryIcon();

        Assert.NotNull(ico);
        Assert.True(IconFile.HasIconHeader(ico));

        using var img = IconFile.DecodeLargestFrame(ico);

        Assert.Equal(256, img.Width);
        Assert.Equal(256, img.Height);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Test infrastructure
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A stream wrapper that records how many bytes were pulled through it.</summary>
file sealed class CountingStream(Stream inner) : Stream
{
    public long BytesRead { get; private set; }

    public override bool CanRead  => inner.CanRead;
    public override bool CanSeek  => false;
    public override bool CanWrite => false;
    public override long Length   => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);

        BytesRead += read;

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value)                => throw new NotSupportedException();
    public override void Write(byte[] buffer, int o, int c)   => throw new NotSupportedException();
    protected override void Dispose(bool disposing)           { if (disposing) inner.Dispose(); base.Dispose(disposing); }
}

/// <summary>A stream wrapper that reports <see cref="CanSeek"/> as false.</summary>
file sealed class NonSeekableStream(Stream inner) : Stream
{
    public override bool CanRead  => inner.CanRead;
    public override bool CanSeek  => false;
    public override bool CanWrite => inner.CanWrite;
    public override long Length   => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void  Flush()                              => inner.Flush();
    public override int   Read(byte[] buffer, int o, int c)   => inner.Read(buffer, o, c);
    public override long  Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void  SetLength(long value)                => throw new NotSupportedException();
    public override void  Write(byte[] buffer, int o, int c)  => inner.Write(buffer, o, c);
    protected override void Dispose(bool disposing)           { if (disposing) inner.Dispose(); base.Dispose(disposing); }
}
