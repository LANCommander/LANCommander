using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using LANCommander.Server.Models;
using LANCommander.SDK;
using System.IO.Compression;
using System.Linq.Expressions;
using LANCommander.Server.Services.Extensions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SharpCompress.Common;

namespace LANCommander.Server.Services
{
    public class ArchiveService : BaseDatabaseService<Archive>
    {
        private readonly StorageLocationService StorageLocationService;

        public ArchiveService(
            ILogger<ArchiveService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory,
            StorageLocationService storageLocationService) : base(logger, cache, repositoryFactory)
        {
            StorageLocationService = storageLocationService;
        }

        public string GetArchiveFileLocation(Archive archive, StorageLocation storageLocation)
        {
            return Path.Combine(storageLocation.Path, archive.ObjectKey);
        }

        public async Task<string> GetArchiveFileLocationAsync(Archive archive)
        {
            string storageLocationPath;
            
            if (archive.StorageLocation != null)
                storageLocationPath = archive.StorageLocation.Path;
            else
            {
                var storageLocation = await StorageLocationService.GetAsync(archive.StorageLocationId);
                
                storageLocationPath = storageLocation.Path;
            }
            
            return Path.Combine(storageLocationPath, archive.ObjectKey);
        }

        public async Task<string> GetArchiveFileLocationAsync(string objectKey)
        {
            var archive = await Include(a => a.StorageLocation).FirstOrDefaultAsync(a => a.ObjectKey == objectKey);

            return await GetArchiveFileLocationAsync(archive);
        }

        public override async Task<Archive> AddAsync(Archive entity)
        {
            await Cache.ExpireGameCacheAsync(entity.GameId);

            return await base.AddAsync(entity);
        }

        public override async Task<ExistingEntityResult<Archive>> AddMissingAsync(Expression<Func<Archive, bool>> predicate, Archive entity)
        {
            await Cache.ExpireGameCacheAsync(entity.GameId);

            return await base.AddMissingAsync(predicate, entity);
        }

        public override async Task<Archive> UpdateAsync(Archive entity)
        {
            await Cache.ExpireGameCacheAsync(entity.GameId);

            return await base.UpdateAsync(entity);
        }
        
        public override async Task DeleteAsync(Archive archive)
        {
            FileHelpers.DeleteIfExists(await GetArchiveFileLocationAsync(archive));

            await Cache.ExpireGameCacheAsync(archive.GameId);
            
            Repository.Context.ChangeTracker.Clear();

            archive = await Query(q =>
            {
                return q.IgnoreAutoIncludes();
            }).GetAsync(archive.Id);

            await base.DeleteAsync(archive);
        }

        public async Task DeleteAsync(Archive archive, StorageLocation storageLocation = null)
        {
            if (storageLocation == null)
                FileHelpers.DeleteIfExists(await GetArchiveFileLocationAsync(archive));
            else
                FileHelpers.DeleteIfExists(GetArchiveFileLocation(archive, storageLocation));

            await Cache.ExpireGameCacheAsync(archive.GameId);

            Repository.Context.ChangeTracker.Clear();
            
            archive = await Query(q =>
            {
                return q.IgnoreAutoIncludes();
            }).GetAsync(archive.Id);

            await base.DeleteAsync(archive);
        }

        public async Task<GameManifest> ReadManifestAsync(string objectKey)
        {
            var upload = await GetArchiveFileLocationAsync(objectKey);

            string manifestContents = String.Empty;

            if (!File.Exists(upload))
                throw new FileNotFoundException(upload);

            using (ZipArchive zip = ZipFile.OpenRead(upload))
            {
                var entry = zip.Entries.FirstOrDefault(e => e.FullName == "_manifest.yml");

                if (entry == null)
                    throw new FileNotFoundException("Manifest not found");

                using (StreamReader sr = new StreamReader(entry.Open()))
                {
                    manifestContents = sr.ReadToEnd();
                }
            }

            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            var manifest = deserializer.Deserialize<GameManifest>(manifestContents);

            return manifest;
        }

        public async Task<byte[]> ReadFileAsync(string objectKey, string path)
        {
            var upload = await GetArchiveFileLocationAsync(objectKey);

            if (!File.Exists(upload))
                throw new FileNotFoundException(upload);

            using (ZipArchive zip = ZipFile.OpenRead(upload))
            {
                var entry = zip.Entries.FirstOrDefault(e => e.FullName == path);

                if (entry == null)
                    throw new FileNotFoundException(path);

                using (var ms = new MemoryStream())
                {
                    entry.Open().CopyTo(ms);

                    return ms.ToArray();
                }
            }
        }

        public async Task<bool> ExistsAsync(Guid archiveId)
        {
            var archive = await Include(a => a.StorageLocation).GetAsync(archiveId);

            var path = await GetArchiveFileLocationAsync(archive);

            return File.Exists(path);
        }

        public async Task<Guid> CopyFromLocalFileAsync(string path)
        {
            Guid objectKey = Guid.NewGuid();

            var importArchivePath = await GetArchiveFileLocationAsync(objectKey.ToString());

            File.Copy(path, importArchivePath, true);

            return objectKey;
        }

        public async Task<IEnumerable<ZipArchiveEntry>> GetContentsAsync(Guid archiveId)
        {
            var archive = await Include(a => a.StorageLocation).GetAsync(archiveId);

            var upload = await GetArchiveFileLocationAsync(archive);

            using (ZipArchive zip = ZipFile.OpenRead(upload))
            {
                return zip.Entries;
            }
        }

        public async Task<long> GetCompressedSizeAsync(Archive archive)
        {
            long size = 0;

            try
            {
                size = new FileInfo(await GetArchiveFileLocationAsync(archive)).Length;
            }
            catch { }

            return size;
        }

        public async Task<long> GetUncompressedSizeAsync(Archive archive)
        {
            long size = 0;

            using (ZipArchive zip = ZipFile.OpenRead(await GetArchiveFileLocationAsync(archive)))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    size += entry.Length;
                }
            }

            return size;
        }

        public async Task PatchArchiveAsync(Archive originalArchive, Archive alteredArchive, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            var alteredZipPath = await GetArchiveFileLocationAsync(alteredArchive);
            var patchZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            ZipArchive originalZip = ZipFile.Open(await GetArchiveFileLocationAsync(originalArchive), ZipArchiveMode.Update);
            ZipArchive alteredZip = ZipFile.OpenRead(alteredZipPath);
            ZipArchive patchZip = ZipFile.Open(patchZipPath, ZipArchiveMode.Create);

            int i = 0;

            foreach (var entry in alteredZip.Entries)
            {
                var originalEntry = originalZip.GetEntry(entry.FullName);

                if (originalEntry == null || originalEntry.Crc32 != entry.Crc32)
                {
                    originalEntry?.Delete();

                    var updatedEntry = originalZip.CreateEntry(entry.FullName, compressionLevel);
                    var patchEntry = patchZip.CreateEntry(entry.FullName, compressionLevel);

                    // Copy the contents of the entry from the altered archive to the original archive
                    using (var updatedStream = updatedEntry.Open())
                    using (var alteredStream = entry.Open())
                    {
                        await alteredStream.CopyToAsync(updatedStream);

                        Logger?.LogInformation("Added {EntryFullName} to base archive {ArchiveId} and new patch archive", entry.FullName, originalArchive.Id.ToString());
                    }

                    // Copy the contents of the entry from the altered archive to the patch archive
                    using (var patchStream = patchEntry.Open())
                    using (var alteredStream = entry.Open())
                    {
                        await alteredStream.CopyToAsync(patchStream);

                        Logger?.LogInformation("Updated {EntryFullName} in base archive {ArchiveId} and added to new patch archive", entry.FullName, originalArchive.Id.ToString());
                    }
                }

                i++;

                Logger?.LogInformation("Finished processing entry {EntryIndex}/{TotalEntries} for original archive {ArchiveId}", i.ToString(), originalZip.Entries.Count.ToString(), originalArchive.Id.ToString());
            }

            originalZip.Dispose();
            alteredZip.Dispose();
            patchZip.Dispose();

            // Replace the uploaded altered ZIP with the new patch ZIP
            if (File.Exists(alteredZipPath))
                File.Delete(alteredZipPath);

            File.Move(patchZipPath, alteredZipPath);

            alteredArchive.CompressedSize = new FileInfo(await GetArchiveFileLocationAsync(alteredArchive)).Length;
            originalArchive.CompressedSize = new FileInfo(await GetArchiveFileLocationAsync(originalArchive)).Length;

            await UpdateAsync(alteredArchive);
            await UpdateAsync(originalArchive);

            Logger?.LogInformation("Finished merging original archive {ArchiveId} and rebuilt patch archive {PatchArchivePath}", originalArchive.Id.ToString(), alteredZipPath);
        }
    }
}
