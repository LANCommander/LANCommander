using System.IO.Compression;
using System.Text;
using LANCommander.SDK.Utilities;
using SharpCompress.Readers;

namespace LANCommander.SDK.Tests.Utilities;

public class ZipEntryNameEncodingTests : IDisposable
{
    static readonly string[] Names = ["ゲーム/起動.exe", "ゲーム/データ/セーブ.dat", "readme.txt"];

    readonly string _path = Path.Combine(Path.GetTempPath(), $"lc-zipnames-{Guid.NewGuid()}.zip");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    /// <summary>Writes the names in <paramref name="encoding"/>; non-UTF-8 encodings leave the zip UTF-8 flag unset.</summary>
    void CreateArchive(Encoding encoding)
    {
        using var zip = ZipFile.Open(_path, ZipArchiveMode.Create, encoding);

        foreach (var name in Names)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.WriteByte(1);
        }
    }

    static Encoding ShiftJis => CodePagesEncodingProvider.Instance.GetEncoding(932)!;

    [Fact]
    public void SystemIOCompressionDecodesUnflaggedShiftJisNames()
    {
        CreateArchive(ShiftJis);

        using var zip = ZipFile.Open(_path, ZipArchiveMode.Read, ZipEntryNameEncoding.Instance);

        Assert.Equal(Names, zip.Entries.Select(e => e.FullName));
    }

    [Fact]
    public void SharpCompressDecodesUnflaggedShiftJisNames()
    {
        CreateArchive(ShiftJis);

        var names = new List<string>();

        using (var stream = File.OpenRead(_path))
        using (var reader = ReaderFactory.OpenReader(stream, new ReaderOptions { ArchiveEncoding = ZipEntryNameEncoding.CreateArchiveEncoding() }))
        {
            while (reader.MoveToNextEntry())
                names.Add(reader.Entry.Key!);
        }

        Assert.Equal(Names, names);
    }

    [Fact]
    public void Utf8NamesAreUnchanged()
    {
        CreateArchive(Encoding.UTF8);

        using var zip = ZipFile.Open(_path, ZipArchiveMode.Read, ZipEntryNameEncoding.Instance);

        Assert.Equal(Names, zip.Entries.Select(e => e.FullName));
    }

    [Fact]
    public void UnflaggedUtf8BytesDecodeAsUtf8()
    {
        var bytes = Encoding.UTF8.GetBytes("ゲーム/起動.exe");

        Assert.Equal("ゲーム/起動.exe", ZipEntryNameEncoding.Decode(bytes, 0, bytes.Length));
    }

    [Fact]
    public void BytesInvalidInUtf8AndShiftJisFallBackToCodePage437()
    {
        // 0x82 followed by a space is not a valid Shift-JIS pair; CP437 maps 0x82 to é.
        var bytes = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0x82, (byte)' ', (byte)'1' };

        Assert.Equal("café 1", ZipEntryNameEncoding.Decode(bytes, 0, bytes.Length));
    }
}
