using System.IO.Compression;
using LANCommander.Packaging.Models;
using LANCommander.SDK.Helpers;

namespace LANCommander.Packaging.Lcx;

/// <summary>
/// Writes an .lcx package.
/// </summary>
/// <remarks>
/// An .lcx is a zip containing <c>Manifest.yml</c>, one <c>Archives/{id}</c> entry per archive
/// (each itself a zip of install-directory-relative paths), and one <c>Scripts/{id}</c> entry
/// per script. This mirrors what the server's exporter produces so packages round-trip.
/// </remarks>
public static class LcxBuilder
{
    public const string CreatedBy = "LANCommander.Launcher";
    public const string ManifestVersion = "1.0.0";

    public static async Task BuildAsync(
        PackageDefinition package,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (string.IsNullOrWhiteSpace(package.OutputPath))
            throw new InvalidOperationException("No output path was set for the package.");

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(package.OutputPath));

        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        await using var outputStream = File.Create(package.OutputPath);
        using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create);

        progress?.Report("Creating game files archive...");

        var archiveId = Guid.NewGuid();
        long uncompressedSize = 0;

        var archiveEntry = archive.CreateEntry($"Archives/{archiveId}", package.CompressionLevel);

        // Measure the inner archive by counting what we write through a passthrough stream.
        // ZipArchiveEntry's Length properties throw while an archive is being created, and the
        // outer stream's position is not a substitute either: it includes zip headers and would
        // be outright wrong for any package containing more than one archive.
        long compressedSize;

        await using (var archiveEntryStream = archiveEntry.Open())
        {
            var countingStream = new CountingStream(archiveEntryStream);

            using (var innerArchive = new ZipArchive(countingStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var filePath in package.SelectedFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!File.Exists(filePath))
                        continue;

                    var relativePath = Path.GetRelativePath(package.InstallDirectory, filePath);
                    var entry = innerArchive.CreateEntry(relativePath, package.CompressionLevel);

                    await using var entryStream = entry.Open();
                    await using var fileStream = File.OpenRead(filePath);

                    uncompressedSize += fileStream.Length;

                    await fileStream.CopyToAsync(entryStream, cancellationToken);
                }
            }

            // Size of the inner archive as the server will store it: the importer streams this
            // entry straight to disk, so these are the bytes it ends up with.
            compressedSize = countingStream.BytesWritten;
        }

        progress?.Report("Generating scripts...");

        var scripts = ScriptGenerator.Generate(package);

        foreach (var script in scripts)
        {
            var scriptEntry = archive.CreateEntry($"Scripts/{script.Id}", CompressionLevel.NoCompression);

            await using var scriptStream = scriptEntry.Open();
            await using var writer = new StreamWriter(scriptStream);

            await writer.WriteAsync(script.Contents.AsMemory(), cancellationToken);
        }

        progress?.Report("Writing manifest...");

        var manifest = package.Manifest;
        var now = DateTime.UtcNow;

        manifest.Id = manifest.Id == Guid.Empty ? Guid.NewGuid() : manifest.Id;
        manifest.ManifestVersion = ManifestVersion;
        manifest.CreatedOn = now;
        manifest.CreatedBy = CreatedBy;
        manifest.UpdatedOn = now;
        manifest.UpdatedBy = CreatedBy;

        manifest.Archives ??= [];
        manifest.Scripts ??= [];

        manifest.Archives.Add(new SDK.Models.Manifest.Archive
        {
            Id = archiveId,
            ObjectKey = archiveId.ToString(),
            Version = manifest.Version ?? "1.0",
            CompressedSize = compressedSize,
            UncompressedSize = uncompressedSize,
            CreatedOn = now,
            CreatedBy = CreatedBy,
        });

        foreach (var script in scripts)
        {
            manifest.Scripts.Add(new SDK.Models.Manifest.Script
            {
                Id = script.Id,
                Type = script.Type,
                Name = script.Type.ToString(),
                RequiresAdmin = script.RequiresAdmin,
                CreatedOn = now,
                CreatedBy = CreatedBy,
            });
        }

        var yaml = ManifestHelper.Serialize(manifest);
        var manifestEntry = archive.CreateEntry(ManifestHelper.ManifestFilename, CompressionLevel.NoCompression);

        await using (var manifestStream = manifestEntry.Open())
        await using (var writer = new StreamWriter(manifestStream))
        {
            await writer.WriteAsync(yaml.AsMemory(), cancellationToken);
        }

        progress?.Report("Done!");
    }

    /// <summary>
    /// Write-only passthrough that records how many bytes went through it, so the inner
    /// archive's compressed size can be measured as it is written. Does not own
    /// <paramref name="inner"/> and holds no resources of its own, so it needs no disposal.
    /// </summary>
    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesWritten { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;

        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            BytesWritten += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            inner.Write(buffer);
            BytesWritten += buffer.Length;
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken);
            BytesWritten += buffer.Length;
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
