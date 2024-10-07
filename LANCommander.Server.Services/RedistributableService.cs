using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class RedistributableService : BaseDatabaseService<Redistributable>
    {
        public RedistributableService(
            ILogger<RedistributableService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }

        public async Task<Redistributable> Import(Guid objectKey)
        {
            var importArchivePath = ArchiveService.GetArchiveFileLocation(objectKey.ToString());

            using (var importArchive = ZipFile.OpenRead(importArchivePath))
            {
                var manifest = ManifestHelper.Deserialize<Redistributable>(await importArchive.ReadAllTextAsync(ManifestHelper.ManifestFilename));

                var redistributable = await Get(manifest.Id);

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
                        script.Contents = await importArchive.ReadAllTextAsync($"Scripts/{script.Id}");
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
                            Contents = await importArchive.ReadAllTextAsync($"Scripts/{manifestScript.Id}"),
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

                        importArchive.ExtractEntry($"Archives/{archive.ObjectKey}", ArchiveService.GetArchiveFileLocation(archive), true);
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
                        };

                        redistributable.Archives.Add(archive);

                        importArchive.ExtractEntry($"Archives/{archive.ObjectKey}", ArchiveService.GetArchiveFileLocation(archive), true);
                    }
                #endregion

                if (exists)
                    redistributable = await Update(redistributable);
                else
                    redistributable = await Add(redistributable);

                return redistributable;
            }
        }
    }
}
