using System.IO.Compression;
using LANCommander.Packaging.Models;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models.Manifest;
using ManifestArchive = LANCommander.SDK.Models.Manifest.Archive;

namespace LANCommander.Packaging.LCX;

/// <summary>
/// Writes an .lcx package.
/// </summary>
/// <remarks>
/// An .lcx is a zip containing <c>Manifest.yml</c>, one <c>Archives/{id}</c> entry per archive
/// (each itself a zip of install-directory-relative paths), and one <c>Scripts/{id}</c> entry
/// per script. This mirrors what the server's exporter produces so packages round-trip.
/// </remarks>
public static class LCXBuilder
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

                    // Entry names are forward slashed per the ZIP spec (APPNOTE 4.4.17.1);
                    // GetRelativePath hands back backslashes on Windows. Windows extractors
                    // tolerate those, but on Linux and macOS the whole tree arrives as single
                    // files with backslashes in their names instead of as directories.
                    var relativePath = Path
                        .GetRelativePath(package.InstallDirectory, filePath)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/');

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
    /// Writes an LCX from an already-normalized game archive.
    /// </summary>
    /// <remarks>
    /// This is intended for callers that already have the inner game ZIP and manifest metadata.
    /// The server's export pipeline is for records that have already been persisted; this overload
    /// supports creating a package before importing it.
    /// </remarks>
    public static async Task BuildFromArchiveAsync(
        string outputPath,
        Game manifest,
        ManifestArchive archiveManifest,
        Stream archiveContent,
        string createdBy,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ValidatePrebuiltArchive(manifest, archiveManifest, archiveContent, createdBy);

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var temporaryPath = $"{fullOutputPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await BuildFromArchiveAsync(
                    output,
                    manifest,
                    archiveManifest,
                    archiveContent,
                    createdBy,
                    progress,
                    cancellationToken);
            }

            File.Move(temporaryPath, fullOutputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Writes an LCX from an already-normalized game archive to a caller-owned stream.
    /// </summary>
    public static async Task BuildFromArchiveAsync(
        Stream output,
        Game manifest,
        ManifestArchive archiveManifest,
        Stream archiveContent,
        string createdBy,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ValidatePrebuiltArchive(manifest, archiveManifest, archiveContent, createdBy);

        if (!output.CanWrite)
            throw new ArgumentException("The output stream must be writable.", nameof(output));

        using var package = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var now = DateTime.UtcNow;

        manifest.Id = manifest.Id == Guid.Empty ? Guid.NewGuid() : manifest.Id;
        manifest.ManifestVersion = ManifestVersion;
        manifest.CreatedBy = string.IsNullOrWhiteSpace(manifest.CreatedBy)
            ? createdBy
            : manifest.CreatedBy;
        manifest.CreatedOn = manifest.CreatedOn == default ? now : manifest.CreatedOn;
        manifest.UpdatedBy = createdBy;
        manifest.UpdatedOn = now;
        manifest.Archives ??= [];
        manifest.Scripts ??= [];

        archiveManifest.ObjectKey = archiveManifest.Id.ToString();
        archiveManifest.CreatedBy = string.IsNullOrWhiteSpace(archiveManifest.CreatedBy)
            ? createdBy
            : archiveManifest.CreatedBy;
        archiveManifest.CreatedOn = archiveManifest.CreatedOn == default
            ? now
            : archiveManifest.CreatedOn;

        progress?.Report($"Writing archive {archiveManifest.Version}...");

        var archiveEntry = package.CreateEntry(
            $"Archives/{archiveManifest.Id}",
            CompressionLevel.NoCompression);

        await using (var entryStream = archiveEntry.Open())
        {
            archiveManifest.CompressedSize = await CopyAndCountAsync(
                archiveContent,
                entryStream,
                cancellationToken);
        }

        manifest.Archives.Add(archiveManifest);

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Writing manifest...");

        var yaml = ManifestHelper.Serialize(manifest);
        var manifestEntry = package.CreateEntry(
            ManifestHelper.ManifestFilename,
            CompressionLevel.NoCompression);

        await using (var manifestStream = manifestEntry.Open())
        await using (var writer = new StreamWriter(manifestStream))
        {
            await writer.WriteAsync(yaml.AsMemory(), cancellationToken);
        }

        progress?.Report("Done!");
    }

    private static void ValidatePrebuiltArchive(
        Game manifest,
        ManifestArchive archiveManifest,
        Stream archiveContent,
        string createdBy)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(archiveManifest);
        ArgumentNullException.ThrowIfNull(archiveContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (archiveManifest.Id == Guid.Empty)
            throw new InvalidOperationException("The archive id must be assigned by the caller.");

        if (!archiveContent.CanRead)
            throw new ArgumentException(
                "The archive content stream must be readable.",
                nameof(archiveContent));

        if (manifest.Archives?.Count > 0)
            throw new ArgumentException(
                "The manifest must not contain archives; pass the archive separately.",
                nameof(manifest));

        if (manifest.Scripts?.Count > 0)
            throw new ArgumentException(
                "The prebuilt archive overload does not write script content.",
                nameof(manifest));

        if (manifest.Media?.Count > 0)
            throw new ArgumentException(
                "The prebuilt archive overload does not write media content.",
                nameof(manifest));

        if (manifest.Saves?.Count > 0)
            throw new ArgumentException(
                "The prebuilt archive overload does not write save content.",
                nameof(manifest));
    }

    private static async Task<long> CopyAndCountAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 1024];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);

            if (read == 0)
                return total;

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            total += read;
        }
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
