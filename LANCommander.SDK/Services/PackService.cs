using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Force.Crc32;
using LANCommander.SDK.Models.Pack;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

public class PackService(ILogger<PackService> logger)
{
    private const int BufferSize = 65536;

    public delegate void OnProgressDelegate(long position, long length);

    public event OnProgressDelegate? OnProgress;
    public event Action<PackEntryHeader>? OnEntryStarted;
    public event Action<PackEntryHeader>? OnEntryCompleted;

    #region Packing

    /// <summary>
    /// Packs a source directory into a pack stream. All files are written as Create operations.
    /// </summary>
    public async Task PackAsync(string sourceDirectory, Stream output, PackOptions options, CancellationToken ct = default)
    {
        var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        var basePath = sourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var header = new PackHeader
        {
            Version = 2,
            Flags = options.WriteDirectory ? PackFlags.HasDirectory : PackFlags.None,
            EntryCount = (ulong)files.Length,
            PackId = options.PackId,
            ParentPackId = options.ParentPackId,
            PackVersion = options.PackVersion,
            ParentVersion = options.ParentVersion,
        };

        await PackBinaryWriter.WriteHeaderAsync(output, header, ct);

        var directoryEntries = new List<PackDirectoryEntry>(files.Length);
        uint dataCrc = 0;
        long totalBytes = files.Sum(f => new FileInfo(f).Length);
        long bytesWritten = 0;

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(basePath, filePath).Replace('\\', '/');
            var fileInfo = new FileInfo(filePath);
            var entryOffset = (ulong)output.Position;

            var entryHeader = new PackEntryHeader
            {
                Path = relativePath,
                Operation = PackEntryOperation.Create,
                Compression = PackCompression.None,
                Attributes = (uint)fileInfo.Attributes,
                Timestamp = fileInfo.LastWriteTimeUtc.Ticks,
                UncompressedSize = (ulong)fileInfo.Length,
                CompressedSize = (ulong)fileInfo.Length,
            };

            // Compute CRC32 of the file
            entryHeader.Checksum = await ComputeFileCrc32Async(filePath, ct);

            OnEntryStarted?.Invoke(entryHeader);

            // Write entry header, tracking data CRC
            var headerStartPos = output.Position;
            await PackBinaryWriter.WriteEntryHeaderAsync(output, entryHeader, ct);
            var headerBytes = await ReadStreamRangeAsync(output, headerStartPos, output.Position, ct);
            dataCrc = Crc32Algorithm.Append(dataCrc, headerBytes);

            // Write file data
            await using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buffer = new byte[BufferSize];
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer, ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    dataCrc = Crc32Algorithm.Append(dataCrc, buffer, 0, bytesRead);
                    bytesWritten += bytesRead;
                    OnProgress?.Invoke(bytesWritten, totalBytes);
                }
            }

            OnEntryCompleted?.Invoke(entryHeader);

            directoryEntries.Add(new PackDirectoryEntry
            {
                Path = relativePath,
                Operation = PackEntryOperation.Create,
                Offset = entryOffset,
                UncompressedSize = entryHeader.UncompressedSize,
                CompressedSize = entryHeader.CompressedSize,
                Checksum = entryHeader.Checksum,
            });
        }

        // Write directory and footer
        var footer = new PackFooter
        {
            EntryCount = (ulong)files.Length,
            DataChecksum = dataCrc,
        };

        if (options.WriteDirectory)
        {
            footer.DirectoryOffset = (ulong)output.Position;
            footer.DirectoryChecksum = await PackBinaryWriter.WriteDirectoryAsync(output, directoryEntries, ct);
        }

        await PackBinaryWriter.WriteFooterAsync(output, footer, ct);

        logger.LogInformation("Packed {Count} files from {Directory}", files.Length, sourceDirectory);
    }

    /// <summary>
    /// Packs a source directory into chunked files. Returns the list of chunk file paths.
    /// </summary>
    public async Task<IReadOnlyList<string>> PackChunkedAsync(
        string sourceDirectory,
        string outputDirectory,
        string baseName,
        PackOptions options,
        long maxChunkSize = 4L * 1024 * 1024 * 1024 - 1,
        CancellationToken ct = default)
    {
        // First, pack into a single temp file
        var tempPath = Path.Combine(outputDirectory, $"{baseName}.lcp.tmp");
        await using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            await PackAsync(sourceDirectory, tempStream, options, ct);
        }

        var tempFileInfo = new FileInfo(tempPath);

        // If it fits in one chunk, just rename
        if (tempFileInfo.Length <= maxChunkSize)
        {
            var finalPath = Path.Combine(outputDirectory, $"{baseName}.lcp");

            if (File.Exists(finalPath))
                File.Delete(finalPath);

            File.Move(tempPath, finalPath);
            return [finalPath];
        }

        // Split into chunks
        var chunkPaths = new List<string>();

        await using var sourceStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var headerBytes = new byte[PackHeader.HeaderSize];
        await sourceStream.ReadAsync(headerBytes.AsMemory(), ct);
        sourceStream.Seek(0, SeekOrigin.Begin);

        var parentCrc = Crc32Algorithm.Compute(headerBytes);
        var chunkIndex = 0u;
        var totalChunks = (uint)Math.Ceiling((double)tempFileInfo.Length / maxChunkSize);

        while (sourceStream.Position < sourceStream.Length)
        {
            ct.ThrowIfCancellationRequested();

            var chunkFileName = chunkIndex == 0
                ? $"{baseName}.lcp"
                : $"{baseName}.lcp.{chunkIndex:D3}";
            var chunkPath = Path.Combine(outputDirectory, chunkFileName);
            chunkPaths.Add(chunkPath);

            await using var chunkStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None);

            long chunkBytesAvailable = maxChunkSize;

            // Write chunk header for non-first chunks
            if (chunkIndex > 0)
            {
                var chunkHeader = new PackChunkHeader
                {
                    ChunkIndex = chunkIndex,
                    TotalChunks = totalChunks,
                    ParentCrc = parentCrc,
                };

                await PackBinaryWriter.WriteChunkHeaderAsync(chunkStream, chunkHeader, ct);
                chunkBytesAvailable -= PackChunkHeader.ChunkHeaderSize;
            }

            // Copy data from source to chunk
            var buffer = new byte[BufferSize];

            while (chunkBytesAvailable > 0 && sourceStream.Position < sourceStream.Length)
            {
                var toRead = (int)Math.Min(chunkBytesAvailable, buffer.Length);
                var bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, toRead), ct);

                if (bytesRead == 0)
                    break;

                await chunkStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                chunkBytesAvailable -= bytesRead;
            }

            chunkIndex++;
        }

        // Clean up temp file
        File.Delete(tempPath);

        logger.LogInformation("Split pack into {Count} chunks in {Directory}", chunkPaths.Count, outputDirectory);
        return chunkPaths;
    }

    #endregion

    #region Unpacking

    /// <summary>
    /// Unpacks a pack stream to a destination directory. Handles Create, Modify, and Delete operations.
    /// Works on forward-only (non-seekable) streams.
    /// </summary>
    public async Task<ExtractionResult> UnpackAsync(
        Stream input,
        string destinationDirectory,
        PackExtractionOptions options,
        CancellationToken ct = default)
    {
        var result = new ExtractionResult
        {
            Directory = destinationDirectory,
        };

        try
        {
            Directory.CreateDirectory(destinationDirectory);

            var header = await PackBinaryReader.ReadHeaderAsync(input, ct);
            long totalBytesRead = PackHeader.HeaderSize;

            for (ulong i = 0; i < header.EntryCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entryHeader = await PackBinaryReader.ReadEntryHeaderAsync(input, ct);
                OnEntryStarted?.Invoke(entryHeader);

                if (entryHeader.Operation == PackEntryOperation.Delete)
                {
                    var deletePath = Path.Combine(destinationDirectory, entryHeader.Path.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(deletePath))
                    {
                        File.Delete(deletePath);
                        logger.LogDebug("Deleted {Path}", entryHeader.Path);
                    }

                    OnEntryCompleted?.Invoke(entryHeader);
                    continue;
                }

                var localPath = Path.Combine(destinationDirectory, entryHeader.Path.Replace('/', Path.DirectorySeparatorChar));
                var localDir = Path.GetDirectoryName(localPath);

                if (localDir != null)
                    Directory.CreateDirectory(localDir);

                // Skip unchanged files if option is set
                if (options.SkipUnchangedFiles && File.Exists(localPath))
                {
                    var localCrc = await ComputeFileCrc32Async(localPath, ct);

                    if (localCrc == entryHeader.Checksum)
                    {
                        // Skip the data in the stream
                        await SkipBytesAsync(input, (long)entryHeader.CompressedSize, ct);
                        totalBytesRead += (long)entryHeader.CompressedSize;

                        result.Files.Add(new ExtractionResult.FileEntry
                        {
                            EntryPath = entryHeader.Path,
                            LocalPath = localPath,
                        });

                        logger.LogDebug("Skipped unchanged file {Path}", entryHeader.Path);
                        OnEntryCompleted?.Invoke(entryHeader);
                        continue;
                    }
                }

                // Extract file
                uint crc = 0;

                await using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[BufferSize];
                    var remaining = (long)entryHeader.CompressedSize;

                    while (remaining > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        var toRead = (int)Math.Min(remaining, buffer.Length);
                        var bytesRead = await input.ReadAsync(buffer.AsMemory(0, toRead), ct);

                        if (bytesRead == 0)
                            throw new EndOfStreamException($"Unexpected end of stream while extracting {entryHeader.Path}");

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                        crc = Crc32Algorithm.Append(crc, buffer, 0, bytesRead);
                        remaining -= bytesRead;
                        totalBytesRead += bytesRead;
                        OnProgress?.Invoke(totalBytesRead, input.CanSeek ? input.Length : 0);
                    }
                }

                // Verify checksum
                if (options.VerifyChecksums && crc != entryHeader.Checksum)
                {
                    logger.LogWarning("Checksum mismatch for {Path}: expected {Expected:X8}, got {Actual:X8}",
                        entryHeader.Path, entryHeader.Checksum, crc);
                }

                // Preserve timestamps
                if (options.PreserveTimestamps && entryHeader.Timestamp != 0)
                {
                    var timestamp = new DateTime(entryHeader.Timestamp, DateTimeKind.Utc);
                    File.SetLastWriteTimeUtc(localPath, timestamp);
                }

                result.Files.Add(new ExtractionResult.FileEntry
                {
                    EntryPath = entryHeader.Path,
                    LocalPath = localPath,
                });

                logger.LogDebug("Extracted {Path} ({Size} bytes)", entryHeader.Path, entryHeader.UncompressedSize);
                OnEntryCompleted?.Invoke(entryHeader);
            }

            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            result.Canceled = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unpack to {Directory}", destinationDirectory);
        }

        return result;
    }

    /// <summary>
    /// Unpacks chunked pack files to a destination directory.
    /// </summary>
    public async Task<ExtractionResult> UnpackChunkedAsync(
        IReadOnlyList<string> chunkPaths,
        string destinationDirectory,
        PackExtractionOptions options,
        CancellationToken ct = default)
    {
        // First chunk has no extra header; subsequent chunks have a ChunkHeaderSize to skip
        var skipBytes = new long[chunkPaths.Count];

        for (int i = 1; i < chunkPaths.Count; i++)
            skipBytes[i] = PackChunkHeader.ChunkHeaderSize;

        await using var stream = new ConcatenatingStream(chunkPaths, skipBytes);
        return await UnpackAsync(stream, destinationDirectory, options, ct);
    }

    #endregion

    #region Directory Browsing

    /// <summary>
    /// Reads the directory and footer from a seekable stream. Returns the full pack manifest.
    /// </summary>
    public async Task<PackManifest> ReadDirectoryAsync(Stream seekableStream, CancellationToken ct = default)
    {
        if (!seekableStream.CanSeek)
            throw new NotSupportedException("ReadDirectoryAsync requires a seekable stream.");

        var footer = await PackBinaryReader.ReadFooterAsync(seekableStream, ct);

        // Read header
        seekableStream.Seek(0, SeekOrigin.Begin);
        var header = await PackBinaryReader.ReadHeaderAsync(seekableStream, ct);

        var entries = new List<PackDirectoryEntry>();

        if (footer.DirectoryOffset > 0)
            entries = await PackBinaryReader.ReadDirectoryEntriesAsync(seekableStream, footer, ct);

        return new PackManifest
        {
            Header = header,
            Entries = entries,
            Footer = footer,
        };
    }

    /// <summary>
    /// Reads the directory from a pack file path.
    /// </summary>
    public async Task<PackManifest> ReadDirectoryAsync(string packFilePath, CancellationToken ct = default)
    {
        await using var stream = new FileStream(packFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await ReadDirectoryAsync(stream, ct);
    }

    #endregion

    #region Verification

    /// <summary>
    /// Verifies all entry checksums by reading the pack sequentially.
    /// Requires a seekable stream.
    /// </summary>
    public async Task<PackVerificationResult> VerifyAsync(Stream seekableStream, CancellationToken ct = default)
    {
        if (!seekableStream.CanSeek)
            throw new NotSupportedException("VerifyAsync requires a seekable stream.");

        var result = new PackVerificationResult { IsValid = true };

        seekableStream.Seek(0, SeekOrigin.Begin);
        var header = await PackBinaryReader.ReadHeaderAsync(seekableStream, ct);

        for (ulong i = 0; i < header.EntryCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entryHeader = await PackBinaryReader.ReadEntryHeaderAsync(seekableStream, ct);

            if (entryHeader.Operation == PackEntryOperation.Delete)
                continue;

            uint crc = 0;
            var buffer = new byte[BufferSize];
            var remaining = (long)entryHeader.CompressedSize;

            while (remaining > 0)
            {
                var toRead = (int)Math.Min(remaining, buffer.Length);
                var bytesRead = await seekableStream.ReadAsync(buffer.AsMemory(0, toRead), ct);

                if (bytesRead == 0)
                    break;

                crc = Crc32Algorithm.Append(crc, buffer, 0, bytesRead);
                remaining -= bytesRead;
            }

            if (crc != entryHeader.Checksum)
            {
                result.IsValid = false;
                result.Failures.Add(new PackVerificationFailure
                {
                    Path = entryHeader.Path,
                    ExpectedChecksum = entryHeader.Checksum,
                    ActualChecksum = crc,
                });
            }
        }

        // Verify data section checksum against footer
        var footer = await PackBinaryReader.ReadFooterAsync(seekableStream, ct);
        var dataEnd = footer.DirectoryOffset > 0 ? (long)footer.DirectoryOffset : seekableStream.Length - PackFooter.FooterSize;
        var dataCrc = await PackBinaryReader.ComputeStreamCrc32Async(seekableStream, PackHeader.HeaderSize, dataEnd, ct);

        if (dataCrc != footer.DataChecksum)
        {
            result.IsValid = false;
            logger.LogWarning("Data section checksum mismatch: expected {Expected:X8}, got {Actual:X8}",
                footer.DataChecksum, dataCrc);
        }

        return result;
    }

    #endregion

    #region Diffing and Patching

    /// <summary>
    /// Compares two pack manifests and returns a manifest representing the diff.
    /// The returned manifest contains entries with appropriate operations (Create, Modify, Delete).
    /// </summary>
    public PackManifest ComputeDiff(PackManifest oldPack, PackManifest newPack)
    {
        var oldEntries = oldPack.Entries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var newEntries = newPack.Entries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);

        var diffEntries = new List<PackDirectoryEntry>();

        // Find added and modified entries
        foreach (var newEntry in newPack.Entries)
        {
            if (!oldEntries.TryGetValue(newEntry.Path, out var oldEntry))
            {
                // New file
                diffEntries.Add(new PackDirectoryEntry
                {
                    Path = newEntry.Path,
                    Operation = PackEntryOperation.Create,
                    Offset = newEntry.Offset,
                    UncompressedSize = newEntry.UncompressedSize,
                    CompressedSize = newEntry.CompressedSize,
                    Checksum = newEntry.Checksum,
                });
            }
            else if (oldEntry.Checksum != newEntry.Checksum)
            {
                // Modified file
                diffEntries.Add(new PackDirectoryEntry
                {
                    Path = newEntry.Path,
                    Operation = PackEntryOperation.Modify,
                    Offset = newEntry.Offset,
                    UncompressedSize = newEntry.UncompressedSize,
                    CompressedSize = newEntry.CompressedSize,
                    Checksum = newEntry.Checksum,
                });
            }
        }

        // Find deleted entries
        foreach (var oldEntry in oldPack.Entries)
        {
            if (!newEntries.ContainsKey(oldEntry.Path))
            {
                diffEntries.Add(new PackDirectoryEntry
                {
                    Path = oldEntry.Path,
                    Operation = PackEntryOperation.Delete,
                    Offset = 0,
                    UncompressedSize = 0,
                    CompressedSize = 0,
                    Checksum = 0,
                });
            }
        }

        return new PackManifest
        {
            Header = new PackHeader
            {
                Version = 2,
                Flags = PackFlags.HasDirectory,
                EntryCount = (ulong)diffEntries.Count,
            },
            Entries = diffEntries,
            Footer = new PackFooter
            {
                EntryCount = (ulong)diffEntries.Count,
            },
        };
    }

    /// <summary>
    /// Creates a patch pack from a diff manifest and the new pack stream.
    /// Seeks into the new pack to extract only changed/added entries, and writes
    /// Delete entries as header-only (no data).
    /// </summary>
    public async Task CreatePatchAsync(
        PackManifest diff,
        Stream newPackStream,
        Stream patchOutput,
        PackOptions options,
        CancellationToken ct = default)
    {
        if (!newPackStream.CanSeek)
            throw new NotSupportedException("CreatePatchAsync requires a seekable new pack stream.");

        var header = new PackHeader
        {
            Version = 2,
            Flags = options.WriteDirectory ? PackFlags.HasDirectory : PackFlags.None,
            EntryCount = (ulong)diff.Entries.Count,
            PackId = options.PackId,
            ParentPackId = options.ParentPackId,
            PackVersion = options.PackVersion,
            ParentVersion = options.ParentVersion,
        };

        await PackBinaryWriter.WriteHeaderAsync(patchOutput, header, ct);

        var patchDirectoryEntries = new List<PackDirectoryEntry>();
        uint dataCrc = 0;

        foreach (var entry in diff.Entries)
        {
            ct.ThrowIfCancellationRequested();

            var patchOffset = (ulong)patchOutput.Position;

            if (entry.Operation == PackEntryOperation.Delete)
            {
                var deleteHeader = new PackEntryHeader
                {
                    Path = entry.Path,
                    Operation = PackEntryOperation.Delete,
                    Compression = PackCompression.None,
                    Attributes = 0,
                    Timestamp = 0,
                    UncompressedSize = 0,
                    CompressedSize = 0,
                    Checksum = 0,
                };

                var headerStartPos = patchOutput.Position;
                await PackBinaryWriter.WriteEntryHeaderAsync(patchOutput, deleteHeader, ct);
                var headerBytes = await ReadStreamRangeAsync(patchOutput, headerStartPos, patchOutput.Position, ct);
                dataCrc = Crc32Algorithm.Append(dataCrc, headerBytes);

                patchDirectoryEntries.Add(new PackDirectoryEntry
                {
                    Path = entry.Path,
                    Operation = PackEntryOperation.Delete,
                    Offset = patchOffset,
                });
            }
            else
            {
                // Seek to the entry in the new pack and copy its header + data
                newPackStream.Seek((long)entry.Offset, SeekOrigin.Begin);
                var sourceEntry = await PackBinaryReader.ReadEntryHeaderAsync(newPackStream, ct);

                var entryHeader = new PackEntryHeader
                {
                    Path = sourceEntry.Path,
                    Operation = entry.Operation,
                    Compression = sourceEntry.Compression,
                    Attributes = sourceEntry.Attributes,
                    Timestamp = sourceEntry.Timestamp,
                    UncompressedSize = sourceEntry.UncompressedSize,
                    CompressedSize = sourceEntry.CompressedSize,
                    Checksum = sourceEntry.Checksum,
                };

                OnEntryStarted?.Invoke(entryHeader);

                var headerStartPos = patchOutput.Position;
                await PackBinaryWriter.WriteEntryHeaderAsync(patchOutput, entryHeader, ct);
                var headerBytes = await ReadStreamRangeAsync(patchOutput, headerStartPos, patchOutput.Position, ct);
                dataCrc = Crc32Algorithm.Append(dataCrc, headerBytes);

                // Copy file data
                var buffer = new byte[BufferSize];
                var remaining = (long)sourceEntry.CompressedSize;

                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(remaining, buffer.Length);
                    var bytesRead = await newPackStream.ReadAsync(buffer.AsMemory(0, toRead), ct);

                    if (bytesRead == 0)
                        throw new EndOfStreamException($"Unexpected end of stream while reading {sourceEntry.Path}");

                    await patchOutput.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    dataCrc = Crc32Algorithm.Append(dataCrc, buffer, 0, bytesRead);
                    remaining -= bytesRead;
                }

                OnEntryCompleted?.Invoke(entryHeader);

                patchDirectoryEntries.Add(new PackDirectoryEntry
                {
                    Path = entry.Path,
                    Operation = entry.Operation,
                    Offset = patchOffset,
                    UncompressedSize = sourceEntry.UncompressedSize,
                    CompressedSize = sourceEntry.CompressedSize,
                    Checksum = sourceEntry.Checksum,
                });
            }
        }

        var footer = new PackFooter
        {
            EntryCount = (ulong)diff.Entries.Count,
            DataChecksum = dataCrc,
        };

        if (options.WriteDirectory)
        {
            footer.DirectoryOffset = (ulong)patchOutput.Position;
            footer.DirectoryChecksum = await PackBinaryWriter.WriteDirectoryAsync(patchOutput, patchDirectoryEntries, ct);
        }

        await PackBinaryWriter.WriteFooterAsync(patchOutput, footer, ct);

        logger.LogInformation("Created patch with {Count} entries", diff.Entries.Count);
    }

    /// <summary>
    /// Applies a patch pack to an existing installation directory.
    /// This is identical to UnpackAsync since the entry operations drive the behavior.
    /// </summary>
    public async Task<ExtractionResult> ApplyPatchAsync(
        Stream patchStream,
        string installDirectory,
        PackExtractionOptions options,
        CancellationToken ct = default)
    {
        return await UnpackAsync(patchStream, installDirectory, options, ct);
    }

    #endregion

    #region Helpers

    private static async Task<uint> ComputeFileCrc32Async(string filePath, CancellationToken ct = default)
    {
        uint crc = 0;
        var buffer = new byte[BufferSize];

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
        {
            crc = Crc32Algorithm.Append(crc, buffer, 0, bytesRead);
        }

        return crc;
    }

    private static async Task SkipBytesAsync(Stream stream, long count, CancellationToken ct = default)
    {
        if (stream.CanSeek)
        {
            stream.Seek(count, SeekOrigin.Current);
            return;
        }

        var buffer = new byte[BufferSize];
        var remaining = count;

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(remaining, buffer.Length);
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, toRead), ct);

            if (bytesRead == 0)
                break;

            remaining -= bytesRead;
        }
    }

    /// <summary>
    /// Reads a range of bytes from a seekable stream. Used to compute CRC of written data.
    /// </summary>
    private static async Task<byte[]> ReadStreamRangeAsync(Stream stream, long start, long end, CancellationToken ct = default)
    {
        if (!stream.CanSeek)
            return [];

        var currentPos = stream.Position;
        stream.Seek(start, SeekOrigin.Begin);

        var length = (int)(end - start);
        var buffer = new byte[length];
        var totalRead = 0;

        while (totalRead < length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);

            if (bytesRead == 0)
                break;

            totalRead += bytesRead;
        }

        stream.Seek(currentPos, SeekOrigin.Begin);
        return buffer;
    }

    #endregion
}
