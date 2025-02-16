using LANCommander.Server.Data.Models;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Services.Extensions;

namespace LANCommander.Server.Services.Importers;

public class RedistributableImporter(
    ArchiveService archiveService,
    RedistributableService redistributableService) : IImporter<Redistributable>
{
    public async Task<Redistributable> ImportAsync(Guid objectKey, ZipArchive importZip)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);
        var manifest =
            ManifestHelper.Deserialize<SDK.Models.Redistributable>(
                await importZip.ReadAllTextAsync(ManifestHelper.ManifestFilename));

        var redistributable = await InitializeFromManifest(manifest);

        redistributable = await UpdateScripts(redistributable, manifest, importZip);
        redistributable = await UpdateArchives(redistributable, manifest, importZip, importArchive);

        if (await redistributableService.ExistsAsync(redistributable.Id))
            redistributable = await redistributableService.UpdateAsync(redistributable);
        else
            redistributable = await redistributableService.AddAsync(redistributable);

        await archiveService.DeleteAsync(importArchive);

        return redistributable;
    }

    private async Task<Redistributable> InitializeFromManifest(SDK.Models.Redistributable manifest)
    {
        var redistributable = await redistributableService.GetAsync(manifest.Id);
        var exists = redistributable != null;

        if (!exists)
            redistributable = new Redistributable();

        redistributable.Id = manifest.Id;
        redistributable.Name = manifest.Name;
        redistributable.Description = manifest.Description;
        redistributable.Notes = manifest.Notes;

        return redistributable;
    }

    private async Task<Redistributable> UpdateScripts(Redistributable redistributable, SDK.Models.Redistributable manifest, ZipArchive importZip)
    {
        if (redistributable.Scripts == null)
            redistributable.Scripts = new List<Script>();

        foreach (var script in redistributable.Scripts)
        {
            var manifestScript = manifest.Scripts.FirstOrDefault(s => s.Id == script.Id);

            if (manifestScript != null)
            {
                script.Contents = await importZip.ReadAllTextAsync($"Scripts/{script.Id}");
                script.Description = manifestScript.Description;
                script.Name = manifestScript.Name;
                script.RequiresAdmin = manifestScript.RequiresAdmin;
                script.Type = (ScriptType)(int)manifestScript.Type;
            }
            else
                redistributable.Scripts.Remove(script);
        }

        if (manifest.Scripts != null)
        {
            foreach (var manifestScript in manifest.Scripts.Where(ms => !redistributable.Scripts.Any(s => s.Id == ms.Id)))
            {
                redistributable.Scripts.Add(new Script()
                {
                    Id = manifestScript.Id,
                    Contents = await importZip.ReadAllTextAsync($"Scripts/{manifestScript.Id}"),
                    Description = manifestScript.Description,
                    Name = manifestScript.Name,
                    RequiresAdmin = manifestScript.RequiresAdmin,
                    Type = (ScriptType)(int)manifestScript.Type,
                    CreatedOn = manifestScript.CreatedOn,
                });
            }
        }

        return redistributable;
    }

    private async Task<Redistributable> UpdateArchives(Redistributable redistributable, SDK.Models.Redistributable manifest, ZipArchive importZip, Archive importArchive)
    {
        if (redistributable.Archives == null)
            redistributable.Archives = new List<Archive>();

        foreach (var archive in redistributable.Archives)
        {
            var manifestArchive = manifest.Archives.FirstOrDefault(a => a.Id == archive.Id);

            if (manifestArchive != null)
            {
                archive.Changelog = manifestArchive.Changelog;
                archive.ObjectKey = manifestArchive.ObjectKey;
                archive.Version = manifestArchive.Version;
                archive.CreatedOn = manifestArchive.CreatedOn;
                archive.StorageLocation = importArchive.StorageLocation;

                var extractionLocation = await archiveService.GetArchiveFileLocationAsync(archive);

                importZip.ExtractEntry($"Archives/{archive.ObjectKey}", extractionLocation, true);

                archive.CompressedSize = new FileInfo(extractionLocation).Length;
            }
        }

        if (manifest.Archives != null)
            foreach (var manifestArchive in manifest.Archives.Where(ma => !redistributable.Archives.Any(a => a.Id == ma.Id)))
            {
                var archive = new Archive()
                {
                    Id = manifestArchive.Id,
                    ObjectKey = manifestArchive.ObjectKey,
                    Changelog = manifestArchive.Changelog,
                    Version = manifestArchive.Version,
                    CreatedOn = manifestArchive.CreatedOn,
                    StorageLocation = importArchive.StorageLocation,
                };

                var extractionLocation = await archiveService.GetArchiveFileLocationAsync(archive);

                importZip.ExtractEntry($"Archives/{archive.ObjectKey}", extractionLocation, true);

                archive.CompressedSize = new FileInfo(extractionLocation).Length;

                redistributable.Archives.Add(archive);
            }

        return redistributable;
    }
} 