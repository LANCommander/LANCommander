using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Force.Crc32;
using LANCommander.SDK.Models.Pack;

namespace LANCommander.SDK.Services;

internal static class PackBinaryReader
{
    public static async Task<PackHeader> ReadHeaderAsync(Stream stream, CancellationToken ct = default)
    {
        var buffer = new byte[PackHeader.HeaderSize];
        await ReadExactAsync(stream, buffer, ct);

        var magic = Encoding.ASCII.GetString(buffer, 0, PackHeader.MagicSize);

        if (magic != PackHeader.Magic)
            throw new InvalidDataException($"Invalid pack magic: expected '{PackHeader.Magic}', got '{magic}'");

        return new PackHeader
        {
            Version = ReadUInt16(buffer, 4),
            Flags = (PackFlags)ReadUInt16(buffer, 6),
            EntryCount = ReadUInt64(buffer, 8),
            PackId = ReadGuid(buffer, 16),
            ParentPackId = ReadGuid(buffer, 32),
            PackVersion = ReadVersionString(buffer, 48),
            ParentVersion = ReadVersionString(buffer, 80),
        };
    }

    public static async Task<PackEntryHeader> ReadEntryHeaderAsync(Stream stream, CancellationToken ct = default)
    {
        var pathLenBuf = new byte[4];
        await ReadExactAsync(stream, pathLenBuf, ct);
        var pathLength = ReadUInt32(pathLenBuf, 0);

        var pathBuf = new byte[pathLength];
        await ReadExactAsync(stream, pathBuf, ct);
        var path = Encoding.UTF8.GetString(pathBuf);

        // Operation (1) + Compression (1) + Attributes (4) + Timestamp (8) +
        // UncompressedSize (8) + CompressedSize (8) + Checksum (4) = 34
        var fixedBuf = new byte[34];
        await ReadExactAsync(stream, fixedBuf, ct);

        return new PackEntryHeader
        {
            Path = path,
            Operation = (PackEntryOperation)fixedBuf[0],
            Compression = (PackCompression)fixedBuf[1],
            Attributes = ReadUInt32(fixedBuf, 2),
            Timestamp = ReadInt64(fixedBuf, 6),
            UncompressedSize = ReadUInt64(fixedBuf, 14),
            CompressedSize = ReadUInt64(fixedBuf, 22),
            Checksum = ReadUInt32(fixedBuf, 30),
        };
    }

    public static async Task<PackDirectoryEntry> ReadDirectoryEntryAsync(Stream stream, CancellationToken ct = default)
    {
        var pathLenBuf = new byte[4];
        await ReadExactAsync(stream, pathLenBuf, ct);
        var pathLength = ReadUInt32(pathLenBuf, 0);

        var pathBuf = new byte[pathLength];
        await ReadExactAsync(stream, pathBuf, ct);
        var path = Encoding.UTF8.GetString(pathBuf);

        // Operation (1) + Offset (8) + UncompressedSize (8) + CompressedSize (8) + Checksum (4) = 29
        var fixedBuf = new byte[29];
        await ReadExactAsync(stream, fixedBuf, ct);

        return new PackDirectoryEntry
        {
            Path = path,
            Operation = (PackEntryOperation)fixedBuf[0],
            Offset = ReadUInt64(fixedBuf, 1),
            UncompressedSize = ReadUInt64(fixedBuf, 9),
            CompressedSize = ReadUInt64(fixedBuf, 17),
            Checksum = ReadUInt32(fixedBuf, 25),
        };
    }

    public static async Task<PackFooter> ReadFooterAsync(Stream stream, CancellationToken ct = default)
    {
        stream.Seek(-PackFooter.FooterSize, SeekOrigin.End);

        var buffer = new byte[PackFooter.FooterSize];
        await ReadExactAsync(stream, buffer, ct);

        var magic = Encoding.ASCII.GetString(buffer, 24, PackHeader.MagicSize);

        if (magic != PackHeader.Magic)
            throw new InvalidDataException($"Invalid footer magic: expected '{PackHeader.Magic}', got '{magic}'");

        return new PackFooter
        {
            DirectoryOffset = ReadUInt64(buffer, 0),
            EntryCount = ReadUInt64(buffer, 8),
            DataChecksum = ReadUInt32(buffer, 16),
            DirectoryChecksum = ReadUInt32(buffer, 20),
        };
    }

    public static async Task<List<PackDirectoryEntry>> ReadDirectoryEntriesAsync(Stream stream, PackFooter footer, CancellationToken ct = default)
    {
        stream.Seek((long)footer.DirectoryOffset, SeekOrigin.Begin);

        var entries = new List<PackDirectoryEntry>();

        for (ulong i = 0; i < footer.EntryCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            entries.Add(await ReadDirectoryEntryAsync(stream, ct));
        }

        return entries;
    }

    public static async Task<PackChunkHeader> ReadChunkHeaderAsync(Stream stream, CancellationToken ct = default)
    {
        var buffer = new byte[PackChunkHeader.ChunkHeaderSize];
        await ReadExactAsync(stream, buffer, ct);

        var magic = Encoding.ASCII.GetString(buffer, 0, 4);

        if (magic != PackChunkHeader.ChunkMagic)
            throw new InvalidDataException($"Invalid chunk magic: expected '{PackChunkHeader.ChunkMagic}', got '{magic}'");

        return new PackChunkHeader
        {
            ChunkIndex = ReadUInt32(buffer, 4),
            TotalChunks = ReadUInt32(buffer, 8),
            ParentCrc = ReadUInt32(buffer, 12),
        };
    }

    /// <summary>
    /// Computes the CRC32 of a region of the stream between two positions.
    /// The stream position is restored after computation.
    /// </summary>
    public static async Task<uint> ComputeStreamCrc32Async(Stream stream, long start, long end, CancellationToken ct = default)
    {
        var originalPosition = stream.Position;
        stream.Seek(start, SeekOrigin.Begin);

        uint crc = 0;
        var buffer = new byte[65536];
        var remaining = end - start;

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();
            var toRead = (int)Math.Min(remaining, buffer.Length);
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, toRead), ct);

            if (bytesRead == 0)
                break;

            crc = Crc32Algorithm.Append(crc, buffer, 0, bytesRead);
            remaining -= bytesRead;
        }

        stream.Seek(originalPosition, SeekOrigin.Begin);
        return crc;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);

            if (bytesRead == 0)
                throw new EndOfStreamException($"Unexpected end of stream. Expected {buffer.Length} bytes, got {totalRead}.");

            totalRead += bytesRead;
        }
    }

    private static Guid ReadGuid(byte[] buffer, int offset)
    {
        var bytes = new byte[16];
        Array.Copy(buffer, offset, bytes, 0, 16);
        return new Guid(bytes);
    }

    private static string ReadVersionString(byte[] buffer, int offset)
    {
        // Find the end of the string (first null byte or field boundary)
        var length = 0;
        for (int i = 0; i < PackHeader.VersionFieldSize; i++)
        {
            if (buffer[offset + i] == 0)
                break;
            length++;
        }
        return Encoding.UTF8.GetString(buffer, offset, length);
    }

    private static ushort ReadUInt16(byte[] buffer, int offset)
        => (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    private static uint ReadUInt32(byte[] buffer, int offset)
        => (uint)(buffer[offset]
            | (buffer[offset + 1] << 8)
            | (buffer[offset + 2] << 16)
            | (buffer[offset + 3] << 24));

    private static ulong ReadUInt64(byte[] buffer, int offset)
        => (ulong)buffer[offset]
            | ((ulong)buffer[offset + 1] << 8)
            | ((ulong)buffer[offset + 2] << 16)
            | ((ulong)buffer[offset + 3] << 24)
            | ((ulong)buffer[offset + 4] << 32)
            | ((ulong)buffer[offset + 5] << 40)
            | ((ulong)buffer[offset + 6] << 48)
            | ((ulong)buffer[offset + 7] << 56);

    private static long ReadInt64(byte[] buffer, int offset)
        => (long)ReadUInt64(buffer, offset);
}
