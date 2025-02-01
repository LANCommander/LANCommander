using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using AutoMapper;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class RedistributableService(
        ILogger<SDK.Services.RedistributableService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory,
        ArchiveService archiveService) : BaseDatabaseService<Redistributable>(logger, cache, mapper, contextFactory)
    {
        public override Task<Redistributable> UpdateAsync(Redistributable entity)
        {
            throw new NotImplementedException();
        }

        public async Task<Redistributable> ImportAsync(Guid objectKey)
        {
            var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
            var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);

            using (var importZip = ZipFile.OpenRead(importArchivePath))
            {
                var manifest = ManifestHelper.Deserialize<Redistributable>(await importZip.ReadAllTextAsync(ManifestHelper.ManifestFilename));

                var redistributable = await GetAsync(manifest.Id);

                var exists = redistributable != null;

                if (!exists)
                    redistributable = new Redistributable();

                redistributable.Id = manifest.Id;
                redistributable.Name = manifest.Name;
                redistributable.Description = manifest.Description;
                redistributable.Notes = manifest.Notes;

                #region Scripts
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
                #endregion

                #region Archives
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
                #endregion

                if (exists)
                    redistributable = await UpdateAsync(redistributable);
                else
                    redistributable = await AddAsync(redistributable);

                await archiveService.DeleteAsync(importArchive);

                return redistributable;
            }
        }
    }
}
