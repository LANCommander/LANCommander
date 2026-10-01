using System.IO.Compression;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Packaging.LCX;

/// <summary>Writes LCX packages from caller-provided archive and script streams.</summary>
public static class LCXPackageWriter
{
    public const string ManifestVersion = "1.0.0";

    public static async Task WriteAsync(
        string outputPath,
        Game manifest,
        IEnumerable<LCXArchiveContent> archives,
        IEnumerable<LCXScriptContent>? scripts,
        string createdBy,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(archives);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var archiveContents = archives.ToList();
        var scriptContents = scripts?.ToList() ?? [];

        ValidatePackage(archiveContents, scriptContents);

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
                await WriteAsync(
                    output,
                    manifest,
                    archiveContents,
                    scriptContents,
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

    public static async Task WriteAsync(
        Stream output,
        Game manifest,
        IEnumerable<LCXArchiveContent> archives,
        IEnumerable<LCXScriptContent>? scripts,
        string createdBy,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(archives);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (!output.CanWrite)
            throw new ArgumentException("The output stream must be writable.", nameof(output));

        var archiveContents = archives.ToList();
        var scriptContents = scripts?.ToList() ?? [];

        ValidatePackage(archiveContents, scriptContents);

        using var package = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        manifest.Id = manifest.Id == Guid.Empty ? Guid.NewGuid() : manifest.Id;
        manifest.ManifestVersion = ManifestVersion;
        manifest.CreatedBy = string.IsNullOrWhiteSpace(manifest.CreatedBy) ? createdBy : manifest.CreatedBy;
        manifest.CreatedOn = manifest.CreatedOn == default ? DateTime.UtcNow : manifest.CreatedOn;
        manifest.UpdatedBy = createdBy;
        manifest.UpdatedOn = DateTime.UtcNow;
        manifest.Archives ??= [];
        manifest.Scripts ??= [];

        manifest.Archives.Clear();
        manifest.Scripts.Clear();

        foreach (var content in archiveContents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Writing archive {content.Manifest.Version}...");

            content.Manifest.ObjectKey = content.Manifest.Id.ToString();

            var entry = package.CreateEntry(
                $"Archives/{content.Manifest.Id}",
                CompressionLevel.NoCompression);

            await using var entryStream = entry.Open();
            content.Manifest.CompressedSize = await CopyAndCountAsync(
                content.Content,
                entryStream,
                cancellationToken);

            manifest.Archives.Add(content.Manifest);
        }

        foreach (var content in scriptContents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Writing script {content.Manifest.Name}...");

            var entry = package.CreateEntry(
                $"Scripts/{content.Manifest.Id}",
                CompressionLevel.NoCompression);

            await using var entryStream = entry.Open();
            await content.Content.CopyToAsync(entryStream, cancellationToken);

            manifest.Scripts.Add(content.Manifest);
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Writing manifest...");

        var manifestEntry = package.CreateEntry(
            ManifestHelper.ManifestFilename,
            CompressionLevel.NoCompression);

        await using (var manifestStream = manifestEntry.Open())
        await using (var writer = new StreamWriter(manifestStream))
        {
            var yaml = ManifestHelper.Serialize(manifest);
            await writer.WriteAsync(yaml.AsMemory(), cancellationToken);
        }

        progress?.Report("Done!");
    }

    private static void ValidatePackage(
        IEnumerable<LCXArchiveContent> archives,
        IEnumerable<LCXScriptContent> scripts)
    {
        if (!archives.Any())
            throw new InvalidOperationException("At least one game archive is required.");

        var ids = new HashSet<Guid>();

        foreach (var content in archives)
        {
            ArgumentNullException.ThrowIfNull(content.Manifest);
            ArgumentNullException.ThrowIfNull(content.Content);

            if (content.Manifest.Id == Guid.Empty)
                throw new InvalidOperationException("Archive ids must be assigned by the caller.");

            if (!content.Content.CanRead)
                throw new InvalidOperationException(
                    $"Archive {content.Manifest.Id} has an unreadable content stream.");

            if (!ids.Add(content.Manifest.Id))
                throw new InvalidOperationException($"Duplicate package entry id {content.Manifest.Id}.");
        }

        foreach (var content in scripts)
        {
            ArgumentNullException.ThrowIfNull(content.Manifest);
            ArgumentNullException.ThrowIfNull(content.Content);

            if (content.Manifest.Id == Guid.Empty)
                throw new InvalidOperationException("Script ids must be assigned by the caller.");

            if (!content.Content.CanRead)
                throw new InvalidOperationException(
                    $"Script {content.Manifest.Id} has an unreadable content stream.");

            if (!ids.Add(content.Manifest.Id))
                throw new InvalidOperationException($"Duplicate package entry id {content.Manifest.Id}.");
        }
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
}
