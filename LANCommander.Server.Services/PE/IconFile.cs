using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace LANCommander.Server.Services.PE;

/// <summary>
/// Reads and writes the Windows ICO container format.
/// </summary>
/// <remarks>
/// An ICO file is an <c>ICONDIR</c> header followed by one <c>ICONDIRENTRY</c> per image and then the
/// image payloads themselves. The payloads are byte-for-byte identical to the RT_ICON resources inside a
/// PE binary, so converting a PE icon group into an ICO file is purely a matter of rewriting the directory:
/// the <c>Id</c> field of a <c>GRPICONDIRENTRY</c> becomes a file offset in an <c>ICONDIRENTRY</c>.
/// </remarks>
public static class IconFile
{
    /// <summary>The MIME type ICO files are served and stored under.</summary>
    public const string MimeType = "image/x-icon";

    private const int DirectoryHeaderSize = 6;
    private const int DirectoryEntrySize = 16;

    /// <summary>Returns true when <paramref name="data"/> begins with a plausible ICO directory header.</summary>
    public static bool HasIconHeader(ReadOnlySpan<byte> data) =>
        data.Length >= DirectoryHeaderSize &&
        data[0] == 0x00 && data[1] == 0x00 && // Reserved
        data[2] == 0x01 && data[3] == 0x00 && // Type == 1 (icon)
        (data[4] | (data[5] << 8)) > 0;       // At least one image

    /// <summary>
    /// Decodes the highest quality image inside an ICO file — the largest by pixel area, breaking ties on
    /// colour depth.
    /// </summary>
    /// <exception cref="InvalidDataException">The data is not a readable ICO file.</exception>
    public static Image<Rgba32> DecodeLargestFrame(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!HasIconHeader(data))
            throw new InvalidDataException("Data does not start with a valid ICO directory header.");

        using var ms = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(ms, Encoding.Default, leaveOpen: true);

        reader.ReadUInt16(); // Reserved
        reader.ReadUInt16(); // Type
        int count = reader.ReadUInt16();

        (int Area, int BitCount, uint Offset, uint Size) best = default;

        for (int i = 0; i < count; i++)
        {
            if (ms.Position + DirectoryEntrySize > ms.Length)
                break;

            int width = reader.ReadByte();
            int height = reader.ReadByte();
            reader.ReadByte();   // ColorCount
            reader.ReadByte();   // Reserved
            reader.ReadUInt16(); // Planes
            int bitCount = reader.ReadUInt16();
            uint size = reader.ReadUInt32();
            uint offset = reader.ReadUInt32();

            if (size == 0 || offset + size > data.Length)
                continue;

            // A stored dimension of 0 means 256 pixels.
            int area = (width == 0 ? 256 : width) * (height == 0 ? 256 : height);

            if (area > best.Area || (area == best.Area && bitCount > best.BitCount))
                best = (area, bitCount, offset, size);
        }

        if (best.Size == 0)
            throw new InvalidDataException("ICO file does not contain any readable images.");

        var frame = new byte[best.Size];
        Array.Copy(data, best.Offset, frame, 0, best.Size);

        return DibDecoder.Decode(frame);
    }

    /// <summary>Reads <paramref name="stream"/> to the end and decodes its largest image.</summary>
    public static Image<Rgba32> DecodeLargestFrame(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        return DecodeLargestFrame(ms.ToArray());
    }

    /// <summary>Serializes <paramref name="images"/> into a complete ICO file.</summary>
    internal static byte[] Build(IReadOnlyList<IconFileImage> images)
    {
        if (images.Count == 0)
            throw new InvalidDataException("Cannot build an ICO file with no images.");

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.Default, leaveOpen: true);

        // ICONDIR
        writer.Write((ushort)0); // Reserved
        writer.Write((ushort)1); // Type = icon
        writer.Write((ushort)images.Count);

        // ICONDIRENTRY per image. Payloads follow the directory, in order.
        var offset = DirectoryHeaderSize + images.Count * DirectoryEntrySize;

        foreach (var image in images)
        {
            writer.Write(image.Width);
            writer.Write(image.Height);
            writer.Write(image.ColorCount);
            writer.Write((byte)0); // Reserved
            writer.Write(image.Planes);
            writer.Write(image.BitCount);
            writer.Write((uint)image.Data.Length);
            writer.Write((uint)offset);

            offset += image.Data.Length;
        }

        foreach (var image in images)
            writer.Write(image.Data);

        writer.Flush();

        return ms.ToArray();
    }
}

/// <summary>A single image destined for an ICO file, with the directory fields describing it.</summary>
internal sealed record IconFileImage(byte Width, byte Height, byte ColorCount, ushort Planes, ushort BitCount, byte[] Data);
