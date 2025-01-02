﻿using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Helpers;
using LANCommander.Server.Models;
using LANCommander.SDK;
using System.IO.Compression;
using System.Linq.Expressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class ArchiveService(
        ILogger<ArchiveService> logger,
        DatabaseContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IFusionCache cache) : BaseDatabaseService<Archive>(logger, dbContext, httpContextAccessor)
    {
        public static string GetArchiveFileLocation(Archive archive)
        {
            return GetArchiveFileLocation(archive.ObjectKey);
        }

        public static string GetArchiveFileLocation(string objectKey)
        {
            var settings = SettingService.GetSettings();

            return Path.Combine(settings.Archives.StoragePath, objectKey);
        }

        public override async Task<Archive> Add(Archive entity)
        {
            await cache.ExpireAsync("MappedGames");

            return await base.Add(entity);
        }

        public override async Task<ExistingEntityResult<Archive>> AddMissing(Expression<Func<Archive, bool>> predicate, Archive entity)
        {
            await cache.ExpireAsync("MappedGames");

            return await base.AddMissing(predicate, entity);
        }

        public override async Task<Archive> Update(Archive entity)
        {
            await cache.ExpireAsync("MappedGames");

            return await base.Update(entity);
        }

        public override async Task Delete(Archive archive)
        {
            FileHelpers.DeleteIfExists(GetArchiveFileLocation(archive));

            await cache.ExpireAsync("MappedGames");

            await base.Delete(archive);
        }

        public static GameManifest ReadManifest(string objectKey)
        {
            var upload = GetArchiveFileLocation(objectKey);

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

        public static byte[] ReadFile(string objectKey, string path)
        {
            var upload = GetArchiveFileLocation(objectKey);

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

        public async Task<bool> Exists(Guid archiveId)
        {
            var archive = await Get(archiveId);

            var path = GetArchiveFileLocation(archive);

            return File.Exists(path);
        }

        public async Task<Guid> CopyFromLocalFile(string path)
        {
            Guid objectKey = Guid.NewGuid();

            var importArchivePath = ArchiveService.GetArchiveFileLocation(objectKey.ToString());

            File.Copy(path, importArchivePath, true);

            return objectKey;
        }

        public async Task<IEnumerable<ZipArchiveEntry>> GetContents(Guid archiveId)
        {
            var archive = await Get(archiveId);

            var upload = GetArchiveFileLocation(archive);

            using (ZipArchive zip = ZipFile.OpenRead(upload))
            {
                return zip.Entries;
            }
        }

        public async Task PatchArchive(Archive originalArchive, Archive alteredArchive, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            var alteredZipPath = GetArchiveFileLocation(alteredArchive);
            var patchZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            ZipArchive originalZip = ZipFile.Open(GetArchiveFileLocation(originalArchive), ZipArchiveMode.Update);
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

            alteredArchive.CompressedSize = new FileInfo(GetArchiveFileLocation(alteredArchive)).Length;
            originalArchive.CompressedSize = new FileInfo(GetArchiveFileLocation(originalArchive)).Length;

            await Update(alteredArchive);
            await Update(originalArchive);

            Logger?.LogInformation("Finished merging original archive {ArchiveId} and rebuilt patch archive {PatchArchivePath}", originalArchive.Id.ToString(), alteredZipPath);
        }
    }
}
