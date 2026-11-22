using System;
using System.Text;
using SharpCompress.Common;

namespace LANCommander.SDK.Utilities;

/// <summary>
/// Decodes zip entry names that are not flagged as UTF-8. Many tools (Windows Explorer on a
/// Japanese system, most DLsite releases) write names in the local code page without setting
/// the zip UTF-8 flag. Decoding those bytes as UTF-8 turns every non-ASCII character into U+FFFD.
/// Each name is decoded on its own: strict UTF-8 first (covers ASCII and unflagged UTF-8),
/// then strict Shift-JIS (code page 932), then code page 437, the zip specification's default.
/// The server's archive browser and the launcher's extractor must decode names the same way,
/// otherwise action paths chosen on the server will not match the files on disk.
/// </summary>
public sealed class ZipEntryNameEncoding : Encoding
{
    public static readonly ZipEntryNameEncoding Instance = new ZipEntryNameEncoding();

    static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    static readonly Encoding Utf8 = new UTF8Encoding(false, false);
    static readonly Encoding StrictShiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)!;
    static readonly Encoding CodePage437 = CodePagesEncodingProvider.Instance.GetEncoding(437)!;

    ZipEntryNameEncoding() { }

    /// <summary>Encoding settings for SharpCompress readers and archives.</summary>
    public static ArchiveEncoding CreateArchiveEncoding() => new ArchiveEncoding { Default = Instance };

    public static string Decode(byte[] bytes, int index, int count)
    {
        if (TryDecode(StrictUtf8, bytes, index, count, out var name))
            return name;

        if (TryDecode(StrictShiftJis, bytes, index, count, out name))
            return name;

        return CodePage437.GetString(bytes, index, count);
    }

    static bool TryDecode(Encoding encoding, byte[] bytes, int index, int count, out string result)
    {
        try
        {
            result = encoding.GetString(bytes, index, count);
            return true;
        }
        catch (DecoderFallbackException)
        {
            result = String.Empty;
            return false;
        }
    }

    public override string EncodingName => "Zip entry name (UTF-8, Shift-JIS, CP437)";

    public override string GetString(byte[] bytes, int index, int count) => Decode(bytes, index, count);

    public override int GetCharCount(byte[] bytes, int index, int count) => Decode(bytes, index, count).Length;

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        var decoded = Decode(bytes, byteIndex, byteCount);

        decoded.CopyTo(0, chars, charIndex, decoded.Length);

        return decoded.Length;
    }

    // Entries are only ever written as UTF-8.
    public override int GetByteCount(char[] chars, int index, int count) => Utf8.GetByteCount(chars, index, count);

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) => Utf8.GetBytes(chars, charIndex, charCount, bytes, byteIndex);

    public override int GetMaxByteCount(int charCount) => Utf8.GetMaxByteCount(charCount);

    // None of the candidate encodings produce more UTF-16 code units than input bytes.
    public override int GetMaxCharCount(int byteCount) => byteCount;
}
