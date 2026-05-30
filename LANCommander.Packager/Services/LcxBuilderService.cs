using System.IO.Compression;
using LANCommander.Packager.Models;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Enums;

namespace LANCommander.Packager.Services;

public static class LcxBuilderService
{
    public static async Task BuildAsync(PackageContext context, IProgress<string>? progress = null)
    {
        using var outputStream = File.Create(context.OutputPath);
        using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create);

        // 1. Create inner game files archive
        progress?.Report("Creating game files archive...");
        
        var archiveId = Guid.NewGuid();
        
        long compressedSize = 0;
        long uncompressedSize = 0;

        var archiveZipEntry = archive.CreateEntry($"Archives/{archiveId}", context.CompressionLevel);

        using (var archiveEntryStream = archiveZipEntry.Open())
        using (var innerArchive = new ZipArchive(archiveEntryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var filePath in context.SelectedFiles)
            {
                if (!File.Exists(filePath))
                    continue;

                var relativePath = Path.GetRelativePath(context.InstallDirectory, filePath);
                var entry = innerArchive.CreateEntry(relativePath, context.CompressionLevel);

                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(filePath);
                
                uncompressedSize += fileStream.Length;
                
                await fileStream.CopyToAsync(entryStream);
            }
        }

        // Get the compressed size from the output stream position delta
        compressedSize = outputStream.Position;

        // 2. Generate and write scripts
        progress?.Report("Generating scripts...");
        
        var scripts = ScriptGeneratorService.Generate(context);

        foreach (var script in scripts)
        {
            var scriptEntry = archive.CreateEntry($"Scripts/{script.Id}", CompressionLevel.NoCompression);
            
            using var scriptStream = scriptEntry.Open();
            using var writer = new StreamWriter(scriptStream);
            
            await writer.WriteAsync(script.Contents);
        }

        // 3. Populate manifest
        progress?.Report("Writing manifest...");
        
        var manifest = context.Manifest;
        
        manifest.Id = manifest.Id == Guid.Empty ? Guid.NewGuid() : manifest.Id;
        manifest.ManifestVersion = "1.0.0";
        manifest.CreatedOn = DateTime.UtcNow;
        manifest.CreatedBy = "LANCommander.Packager";
        manifest.UpdatedOn = DateTime.UtcNow;
        manifest.UpdatedBy = "LANCommander.Packager";

        manifest.Archives.Add(new SDK.Models.Manifest.Archive
        {
            Id = archiveId,
            ObjectKey = archiveId.ToString(),
            Version = manifest.Version ?? "1.0",
            CompressedSize = compressedSize,
            UncompressedSize = uncompressedSize,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "LANCommander.Packager"
        });

        foreach (var script in scripts)
        {
            manifest.Scripts.Add(new SDK.Models.Manifest.Script
            {
                Id = script.Id,
                Type = script.Type,
                Name = script.Type.ToString(),
                RequiresAdmin = script.RequiresAdmin,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "LANCommander.Packager"
            });
        }

        // 4. Serialize manifest to YAML and write to archive
        var yaml = ManifestHelper.Serialize(manifest);

        var manifestEntry = archive.CreateEntry(ManifestHelper.ManifestFilename, CompressionLevel.NoCompression);
        
        using (var ms = new MemoryStream())
        using (var writer = new StreamWriter(ms))
        {
            await writer.WriteAsync(yaml);
            await writer.FlushAsync();

            using var entryStream = manifestEntry.Open();
            
            ms.Seek(0, SeekOrigin.Begin);
            await ms.CopyToAsync(entryStream);
        }

        progress?.Report("Done!");
    }
}
