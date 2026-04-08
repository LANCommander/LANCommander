using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Force.Crc32;
using LANCommander.SDK.Models.Pack;

namespace LANCommander.SDK.Services;

internal static class PackBinaryWriter
{
    public static async Task WriteHeaderAsync(Stream stream, PackHeader header, CancellationToken ct = default)
    {
        var buffer = new byte[PackHeader.HeaderSize];
        var magic = Encoding.ASCII.GetBytes(PackHeader.Magic);

        Array.Copy(magic, 0, buffer, 0, PackHeader.MagicSize);
        WriteUInt16(buffer, 4, header.Version);
        WriteUInt16(buffer, 6, (ushort)header.Flags);
        WriteUInt64(buffer, 8, header.EntryCount);
        WriteGuid(buffer, 16, header.PackId);
        WriteGuid(buffer, 32, header.ParentPackId);
        WriteVersionString(buffer, 48, header.PackVersion);
        WriteVersionString(buffer, 80, header.ParentVersion);

        await stream.WriteAsync(buffer, ct);
    }

    public static async Task WriteEntryHeaderAsync(Stream stream, PackEntryHeader entry, CancellationToken ct = default)
    {
        var pathBytes = Encoding.UTF8.GetBytes(entry.Path);

        // PathLength (4) + Path (variable) + Operation (1) + Compression (1) +
        // Attributes (4) + Timestamp (8) + UncompressedSize (8) + CompressedSize (8) + Checksum (4)
        var fixedSize = 4 + pathBytes.Length + 1 + 1 + 4 + 8 + 8 + 8 + 4;
        var buffer = new byte[fixedSize];
        var offset = 0;

        WriteUInt32(buffer, offset, (uint)pathBytes.Length); offset += 4;
        Array.Copy(pathBytes, 0, buffer, offset, pathBytes.Length); offset += pathBytes.Length;
        buffer[offset++] = (byte)entry.Operation;
        buffer[offset++] = (byte)entry.Compression;
        WriteUInt32(buffer, offset, entry.Attributes); offset += 4;
        WriteInt64(buffer, offset, entry.Timestamp); offset += 8;
        WriteUInt64(buffer, offset, entry.UncompressedSize); offset += 8;
        WriteUInt64(buffer, offset, entry.CompressedSize); offset += 8;
        WriteUInt32(buffer, offset, entry.Checksum);

        await stream.WriteAsync(buffer, ct);
    }

    public static async Task<uint> WriteDirectoryAsync(Stream stream, List<PackDirectoryEntry> entries, CancellationToken ct = default)
    {
        uint crc = 0;

        foreach (var entry in entries)
        {
            var pathBytes = Encoding.UTF8.GetBytes(entry.Path);
            var entrySize = 4 + pathBytes.Length + 1 + 8 + 8 + 8 + 4;
            var buffer = new byte[entrySize];
            var offset = 0;

            WriteUInt32(buffer, offset, (uint)pathBytes.Length); offset += 4;
            Array.Copy(pathBytes, 0, buffer, offset, pathBytes.Length); offset += pathBytes.Length;
            buffer[offset++] = (byte)entry.Operation;
            WriteUInt64(buffer, offset, entry.Offset); offset += 8;
            WriteUInt64(buffer, offset, entry.UncompressedSize); offset += 8;
            WriteUInt64(buffer, offset, entry.CompressedSize); offset += 8;
            WriteUInt32(buffer, offset, entry.Checksum);

            crc = Crc32Algorithm.Append(crc, buffer);
            await stream.WriteAsync(buffer, ct);
        }

        return crc;
    }

    public static async Task WriteFooterAsync(Stream stream, PackFooter footer, CancellationToken ct = default)
    {
        var buffer = new byte[PackFooter.FooterSize];
        var magic = Encoding.ASCII.GetBytes(PackHeader.Magic);

        WriteUInt64(buffer, 0, footer.DirectoryOffset);
        WriteUInt64(buffer, 8, footer.EntryCount);
        WriteUInt32(buffer, 16, footer.DataChecksum);
        WriteUInt32(buffer, 20, footer.DirectoryChecksum);
        Array.Copy(magic, 0, buffer, 24, PackHeader.MagicSize);

        await stream.WriteAsync(buffer, ct);
    }

    public static async Task WriteChunkHeaderAsync(Stream stream, PackChunkHeader chunk, CancellationToken ct = default)
    {
        var buffer = new byte[PackChunkHeader.ChunkHeaderSize];
        var magic = Encoding.ASCII.GetBytes(PackChunkHeader.ChunkMagic);

        Array.Copy(magic, 0, buffer, 0, 4);
        WriteUInt32(buffer, 4, chunk.ChunkIndex);
        WriteUInt32(buffer, 8, chunk.TotalChunks);
        WriteUInt32(buffer, 12, chunk.ParentCrc);

        await stream.WriteAsync(buffer, ct);
    }

    public static uint ComputeHeaderCrc(PackHeader header)
    {
        var buffer = new byte[PackHeader.HeaderSize];
        var magic = Encoding.ASCII.GetBytes(PackHeader.Magic);

        Array.Copy(magic, 0, buffer, 0, PackHeader.MagicSize);
        WriteUInt16(buffer, 4, header.Version);
        WriteUInt16(buffer, 6, (ushort)header.Flags);
        WriteUInt64(buffer, 8, header.EntryCount);
        WriteGuid(buffer, 16, header.PackId);
        WriteGuid(buffer, 32, header.ParentPackId);
        WriteVersionString(buffer, 48, header.PackVersion);
        WriteVersionString(buffer, 80, header.ParentVersion);

        return Crc32Algorithm.Compute(buffer);
    }

    private static void WriteGuid(byte[] buffer, int offset, Guid value)
    {
        var bytes = value.ToByteArray();
        Array.Copy(bytes, 0, buffer, offset, 16);
    }

    private static void WriteVersionString(byte[] buffer, int offset, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var length = Math.Min(bytes.Length, PackHeader.VersionFieldSize);
        Array.Copy(bytes, 0, buffer, offset, length);
        // Zero-fill remainder
        for (int i = length; i < PackHeader.VersionFieldSize; i++)
            buffer[offset + i] = 0;
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteUInt64(byte[] buffer, int offset, ulong value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
        buffer[offset + 4] = (byte)(value >> 32);
        buffer[offset + 5] = (byte)(value >> 40);
        buffer[offset + 6] = (byte)(value >> 48);
        buffer[offset + 7] = (byte)(value >> 56);
    }

    private static void WriteInt64(byte[] buffer, int offset, long value)
        => WriteUInt64(buffer, offset, (ulong)value);
}
