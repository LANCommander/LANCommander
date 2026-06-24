using System.Buffers.Binary;
using System.Text;

namespace LANCommander.Server.Services
{
    /// <summary>
    /// Reads a ZIP archive's raw central directory to detect entries that a forward-only
    /// (streaming) extractor cannot read reliably.
    ///
    /// The launcher streams archives straight from the HTTP response rather than downloading
    /// them first, so it cannot seek to the central directory and must infer each entry's
    /// boundaries as bytes arrive. For a STORED (uncompressed) entry written with a streaming
    /// data descriptor (general purpose bit 3), the local header carries no size, so the reader
    /// has to scan the payload for the data descriptor signature (PK\x07\x08). On large binary
    /// payloads that signature can occur by coincidence, derailing extraction. Detecting that
    /// exact combination lets the server flag and repack such archives.
    /// </summary>
    public static class ZipStreamingInspector
    {
        private const uint EocdSignature = 0x06054b50;
        private const uint Zip64EocdLocatorSignature = 0x07064b50;
        private const uint Zip64EocdSignature = 0x06064b50;
        private const uint CentralDirectoryHeaderSignature = 0x02014b50;

        private const ushort MethodStored = 0;
        private const ushort FlagDataDescriptor = 0x0008;
        private const ushort Zip64ExtraFieldId = 0x0001;

        public sealed class Entry
        {
            public string Name { get; set; } = string.Empty;
            public ushort CompressionMethod { get; set; }
            public ushort Flags { get; set; }
            public long CompressedSize { get; set; }
            public long UncompressedSize { get; set; }

            /// <summary>The entry stores its size in a trailing data descriptor (GP bit 3).</summary>
            public bool UsesDataDescriptor => (Flags & FlagDataDescriptor) != 0;

            /// <summary>The entry is stored uncompressed (no deflate end-of-stream marker to find).</summary>
            public bool IsStored => CompressionMethod == MethodStored;

            /// <summary>
            /// STORED + streaming data descriptor: the only entry shape that forces the launcher's
            /// streaming reader to scan the payload for a descriptor signature, which can misfire.
            /// </summary>
            public bool IsStreamingUnsafe => IsStored && UsesDataDescriptor;
        }

        public static IReadOnlyList<Entry> ReadCentralDirectory(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ReadCentralDirectory(fs);
        }

        public static IReadOnlyList<Entry> ReadCentralDirectory(Stream stream)
        {
            var eocdOffset = FindEocd(stream);
            if (eocdOffset < 0)
                throw new InvalidDataException("Could not locate the End of Central Directory record; the file is not a valid ZIP archive.");

            stream.Position = eocdOffset;
            var eocd = ReadExactly(stream, 22);

            var totalEntries = (long)BinaryPrimitives.ReadUInt16LittleEndian(eocd.AsSpan(10, 2));
            var cdOffset = (long)BinaryPrimitives.ReadUInt32LittleEndian(eocd.AsSpan(16, 4));

            // A central directory that starts beyond 4 GiB (as in a multi-gigabyte archive) forces
            // ZIP64, where the real offset and entry count live in the ZIP64 EOCD record.
            if (cdOffset == 0xFFFFFFFF || totalEntries == 0xFFFF)
                ResolveZip64Eocd(stream, eocdOffset, ref totalEntries, ref cdOffset);

            var entries = new List<Entry>();
            stream.Position = cdOffset;

            for (long i = 0; i < totalEntries; i++)
            {
                var header = ReadExactly(stream, 46);
                if (BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4)) != CentralDirectoryHeaderSignature)
                    break;

                var entry = new Entry
                {
                    Flags = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8, 2)),
                    CompressionMethod = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(10, 2)),
                    CompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20, 4)),
                    UncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24, 4)),
                };

                var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28, 2));
                var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(30, 2));
                var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(32, 2));

                entry.Name = Encoding.UTF8.GetString(ReadExactly(stream, nameLength));

                var extra = extraLength > 0 ? ReadExactly(stream, extraLength) : Array.Empty<byte>();
                ResolveZip64Sizes(entry, extra);

                if (commentLength > 0)
                    Skip(stream, commentLength);

                entries.Add(entry);
            }

            return entries;
        }

        private static void ResolveZip64Eocd(Stream stream, long eocdOffset, ref long totalEntries, ref long cdOffset)
        {
            // The ZIP64 EOCD locator sits immediately (20 bytes) before the EOCD record.
            var locatorOffset = eocdOffset - 20;
            if (locatorOffset < 0)
                return;

            stream.Position = locatorOffset;
            var locator = ReadExactly(stream, 20);
            if (BinaryPrimitives.ReadUInt32LittleEndian(locator.AsSpan(0, 4)) != Zip64EocdLocatorSignature)
                return;

            var zip64EocdOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(locator.AsSpan(8, 8));
            stream.Position = zip64EocdOffset;
            var zip64 = ReadExactly(stream, 56);
            if (BinaryPrimitives.ReadUInt32LittleEndian(zip64.AsSpan(0, 4)) != Zip64EocdSignature)
                return;

            totalEntries = (long)BinaryPrimitives.ReadUInt64LittleEndian(zip64.AsSpan(32, 8));
            cdOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(zip64.AsSpan(48, 8));
        }

        private static void ResolveZip64Sizes(Entry entry, byte[] extra)
        {
            // ZIP64 extra fields only carry the values that overflowed 32 bits, in a fixed order:
            // uncompressed size, then compressed size.
            var needUncompressed = entry.UncompressedSize == 0xFFFFFFFF;
            var needCompressed = entry.CompressedSize == 0xFFFFFFFF;
            if (!needUncompressed && !needCompressed)
                return;

            var pos = 0;
            while (pos + 4 <= extra.Length)
            {
                var headerId = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(pos, 2));
                var dataSize = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(pos + 2, 2));
                var dataStart = pos + 4;

                if (headerId == Zip64ExtraFieldId)
                {
                    var fieldPos = dataStart;
                    var dataEnd = dataStart + dataSize;

                    if (needUncompressed && fieldPos + 8 <= dataEnd)
                    {
                        entry.UncompressedSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(extra.AsSpan(fieldPos, 8));
                        fieldPos += 8;
                    }
                    if (needCompressed && fieldPos + 8 <= dataEnd)
                        entry.CompressedSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(extra.AsSpan(fieldPos, 8));

                    return;
                }

                pos = dataStart + dataSize;
            }
        }

        private static long FindEocd(Stream stream)
        {
            // The EOCD is 22 bytes plus an optional comment of up to 65535 bytes, so it lives in
            // the final ~64 KiB of the file. Scan backward for the signature.
            var fileLength = stream.Length;
            if (fileLength < 22)
                return -1;

            var scanLength = (int)Math.Min(fileLength, 22 + 0xFFFF);
            var start = fileLength - scanLength;

            stream.Position = start;
            var buffer = ReadExactly(stream, scanLength);

            for (var i = buffer.Length - 22; i >= 0; i--)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(i, 4)) == EocdSignature)
                    return start + i;
            }

            return -1;
        }

        private static byte[] ReadExactly(Stream stream, int count)
        {
            var buffer = new byte[count];
            var read = 0;
            while (read < count)
            {
                var n = stream.Read(buffer, read, count - read);
                if (n == 0)
                    throw new EndOfStreamException("Unexpected end of file while reading ZIP structure.");
                read += n;
            }
            return buffer;
        }

        private static void Skip(Stream stream, int count)
        {
            if (stream.CanSeek)
                stream.Position += count;
            else
                ReadExactly(stream, count);
        }
    }
}
