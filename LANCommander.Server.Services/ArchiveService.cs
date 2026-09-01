using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using System.IO.Compression;
using System.Linq.Expressions;
using AutoMapper;
using LANCommander.SDK.Services;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.Exceptions;
using YamlDotNet.Serialization;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PascalCaseNamingConvention = YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention;
using LANCommander.SDK;

namespace LANCommander.Server.Services
{
    public sealed class ArchiveService(
        ILogger<ArchiveService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        StorageLocationService storageLocationService) : BaseDatabaseService<Archive>(logger, settingsProvider, cache, mapper, httpContextAccessor, dbContextFactory), IArchiveClient
    {
        public async Task<Archive> GetLatestArchiveAsync(Expression<Func<Archive, bool>> predicate)
        {
            return await Query(q =>
            {
                return q.OrderByDescending(a => a.CreatedOn);
            }).FirstOrDefaultAsync(predicate);
        }
        
        public string GetArchiveFileLocation(Archive archive, StorageLocation storageLocation)
        {
            return AppPaths.ResolveStorageLocationPath(storageLocation.Path, archive.ObjectKey);
        }

        public async Task<string> GetArchiveFileLocationAsync(Archive archive)
        {
            string storageLocationPath;

            if (archive.StorageLocation != null)
                storageLocationPath = archive.StorageLocation.Path;
            else
            {
                var storageLocation = await storageLocationService.GetAsync(archive.StorageLocationId);

                storageLocationPath = storageLocation.Path;
            }

            return AppPaths.ResolveStorageLocationPath(storageLocationPath, archive.ObjectKey);
        }

        public async Task<string> GetArchiveFileLocationAsync(string objectKey)
        {
            var archive = await Include(a => a.StorageLocation).FirstOrDefaultAsync(a => a.ObjectKey == objectKey);

            return await GetArchiveFileLocationAsync(archive);
        }

        public override async Task<Archive> AddAsync(Archive entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Game);
                await context.UpdateRelationshipAsync(a => a.Redistributable);
                await context.UpdateRelationshipAsync(a => a.Tool);
                await context.UpdateRelationshipAsync(a => a.StorageLocation);
            });
        }

        public override async Task<ExistingEntityResult<Archive>> AddMissingAsync(Expression<Func<Archive, bool>> predicate, Archive entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);

            return await base.AddMissingAsync(predicate, entity);
        }

        public override async Task<Archive> UpdateAsync(Archive updatedArchive)
        {
            await cache.ExpireGameCacheAsync(updatedArchive.GameId);
            await cache.ExpireArchiveCacheAsync(updatedArchive.Id);
            
            return await base.UpdateAsync(updatedArchive, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Game);
                await context.UpdateRelationshipAsync(a => a.Redistributable);
                await context.UpdateRelationshipAsync(a => a.Tool);
                await context.UpdateRelationshipAsync(a => a.StorageLocation);
            });
        }
        
        public override async Task DeleteAsync(Archive archive)
        {
            await EnsureNotExplicitDefaultAsync(archive);
            await DeleteRelatedPatchesAsync(archive.Id);

            FileHelpers.DeleteIfExists(await GetArchiveFileLocationAsync(archive));

            await cache.ExpireGameCacheAsync(archive.GameId);
            await cache.ExpireArchiveCacheAsync(archive.Id);

            await base.DeleteAsync(archive);
        }

        public async Task DeleteAsync(Archive archive, StorageLocation storageLocation = null)
        {
            await EnsureNotExplicitDefaultAsync(archive);
            await DeleteRelatedPatchesAsync(archive.Id);

            if (storageLocation == null)
                FileHelpers.DeleteIfExists(await GetArchiveFileLocationAsync(archive));
            else
                FileHelpers.DeleteIfExists(GetArchiveFileLocation(archive, storageLocation));

            await cache.ExpireGameCacheAsync(archive.GameId);

            await base.DeleteAsync(archive);
        }

        /// <summary>
        /// Service-level backstop for the "clear or reassign the default before deleting it" UX
        /// rule: rejects deleting an archive that is still a game's explicit
        /// <see cref="Game.DefaultArchiveId"/>, so a direct API/service call that bypasses the
        /// admin UI cannot silently leave the game's default pointer to be papered over by the
        /// database's ON DELETE SET NULL behavior. Always re-reads the current value from the
        /// database rather than trusting <c>archive.Game</c>, which may be a stale/detached
        /// navigation property on the entity passed in.
        /// </summary>
        private async Task EnsureNotExplicitDefaultAsync(Archive archive)
        {
            if (!archive.GameId.HasValue)
                return;

            await using var context = await dbContextFactory.CreateDbContextAsync();

            var defaultArchiveId = await context.Set<Game>()
                .Where(g => g.Id == archive.GameId.Value)
                .Select(g => g.DefaultArchiveId)
                .FirstOrDefaultAsync();

            if (defaultArchiveId.HasValue && defaultArchiveId.Value == archive.Id)
                throw new CannotDeleteDefaultArchiveException(archive.GameId.Value, archive.Id);
        }

        /// <summary>
        /// Deletes any <see cref="ArchivePatch"/> artifacts (and their backing files) that reference
        /// the given archive as either endpoint. Patches are derived data with no independent meaning
        /// once one of the two full archives they link is gone, so they are cleaned up alongside the
        /// archive itself rather than left as orphaned rows/files.
        /// </summary>
        private async Task DeleteRelatedPatchesAsync(Guid archiveId)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();

            var patches = await context.Set<ArchivePatch>()
                .Include(p => p.StorageLocation)
                .Where(p => p.FromArchiveId == archiveId || p.ToArchiveId == archiveId)
                .ToListAsync();

            if (patches.Count == 0)
                return;

            foreach (var patch in patches)
            {
                try
                {
                    var path = AppPaths.ResolveStorageLocationPath(patch.StorageLocation.Path, patch.ObjectKey);

                    FileHelpers.DeleteIfExists(path);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Could not delete archive patch file for patch {ArchivePatchId}", patch.Id);
                }
            }

            context.Set<ArchivePatch>().RemoveRange(patches);

            await context.SaveChangesAsync();
        }

        public async Task<SDK.Models.Manifest.Game> ReadManifestAsync(string objectKey)
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

            var manifest = deserializer.Deserialize<SDK.Models.Manifest.Game>(manifestContents);

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

        public async Task<bool> FileExistsAsync(Guid archiveId)
        {
            var archive = await Include(a => a.StorageLocation).GetAsync(archiveId);

            if (archive == null)
                return false;

            var path = await GetArchiveFileLocationAsync(archive);

            return File.Exists(path);
        }

        public async Task<Guid> CopyFromLocalFileAsync(string path, Guid storageLocationId, bool move = false)
        {
            var storageLocation = await storageLocationService.GetAsync(storageLocationId);

            var archive = new Archive
            {
                ObjectKey = Guid.NewGuid().ToString(),
                StorageLocationId = storageLocation.Id,
                Version = ""
            };

            archive = await AddAsync(archive);

            var archivePath = await GetArchiveFileLocationAsync(archive);

            var archiveDirectory = Path.GetDirectoryName(archivePath);

            if (!string.IsNullOrEmpty(archiveDirectory) && !Directory.Exists(archiveDirectory))
                Directory.CreateDirectory(archiveDirectory);

            if (move)
            {
                try
                {
                    File.Move(path, archivePath, true);
                }
                catch (IOException ex)
                {
                    _logger.LogInformation(ex, "Could not move local file from {SourcePath} to {DestinationPath}, falling back to copy", path, archivePath);

                    File.Copy(path, archivePath, true);
                }
            }
            else
            {
                File.Copy(path, archivePath, true);
            }

            return Guid.Parse(archive.ObjectKey);
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

        /// <summary>
        /// Inspects an archive for entries the launcher's streaming extractor cannot read reliably
        /// (stored uncompressed entries written with a streaming data descriptor).
        /// </summary>
        public async Task<ArchiveStreamingReport> InspectStreamingCompatibilityAsync(Guid archiveId)
        {
            var archive = await Include(a => a.StorageLocation).GetAsync(archiveId);
            var path = await GetArchiveFileLocationAsync(archive);

            var report = new ArchiveStreamingReport();

            if (!File.Exists(path))
            {
                _logger?.LogWarning("Cannot inspect archive {ArchiveId}; file not found at {Path}", archiveId, path);
                return report;
            }

            var entries = ZipStreamingInspector.ReadCentralDirectory(path);
            report.TotalEntries = entries.Count;

            foreach (var entry in entries.Where(e => e.IsStreamingUnsafe))
            {
                report.ProblemEntries.Add(new ArchiveStreamingProblemEntry
                {
                    Name = entry.Name,
                    UncompressedSize = entry.UncompressedSize,
                    Reason = "Stored (uncompressed) entry written with a streaming data descriptor",
                });
            }

            return report;
        }

        /// <summary>
        /// Rewrites an archive into a streaming-safe layout. Writing through System.IO.Compression
        /// to a seekable file back-patches real sizes into the local file headers and omits the
        /// streaming data descriptors, so the launcher's forward-only reader can extract every
        /// entry without scanning. Original compression is preserved per entry (stored entries stay
        /// stored) to avoid needlessly re-deflating already-compressed payloads.
        /// </summary>
        public async Task RepackArchiveAsync(Guid archiveId)
        {
            var archive = await Include(a => a.StorageLocation).GetAsync(archiveId);
            var path = await GetArchiveFileLocationAsync(archive);

            if (!File.Exists(path))
                throw new FileNotFoundException("Archive file not found", path);

            var storedEntryNames = ZipStreamingInspector.ReadCentralDirectory(path)
                .Where(e => e.IsStored)
                .Select(e => e.Name)
                .ToHashSet(StringComparer.Ordinal);

            var tempPath = path + ".repack.tmp";

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            try
            {
                using (var source = ZipFile.OpenRead(path))
                using (var destStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite))
                using (var dest = new ZipArchive(destStream, ZipArchiveMode.Create))
                {
                    foreach (var sourceEntry in source.Entries)
                    {
                        var level = storedEntryNames.Contains(sourceEntry.FullName)
                            ? CompressionLevel.NoCompression
                            : CompressionLevel.Optimal;

                        var destEntry = dest.CreateEntry(sourceEntry.FullName, level);
                        destEntry.LastWriteTime = sourceEntry.LastWriteTime;

                        using var sourceEntryStream = sourceEntry.Open();
                        using var destEntryStream = destEntry.Open();
                        await sourceEntryStream.CopyToAsync(destEntryStream);
                    }
                }

                File.Delete(path);
                File.Move(tempPath, path);

                _logger?.LogInformation("Repacked archive {ArchiveId} into a streaming-safe layout", archiveId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not repack archive {ArchiveId}", archiveId);

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                throw;
            }

            await RecalculateFileSizeArchiveAsync(archive);
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

        public async Task<bool> RecalculateFileSizeArchiveAsync(Archive archive)
        {
            try
            {
                var path = await GetArchiveFileLocationAsync(archive);

                if (File.Exists(path))
                {
                    archive.CompressedSize = await GetCompressedSizeAsync(archive);
                    archive.UncompressedSize = await GetUncompressedSizeAsync(archive);

                    await UpdateAsync(archive);
                    return true;
                }
                else
                {
                    _logger?.LogWarning("No local file found for archive {ArchiveId} on path: {path}", archive.Id, path);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not recalculate file size for archive {ArchiveId}", archive.Id);
            }

            return false;
        }
        
        public async Task<Archive> WriteToFileAsync(Archive archive, Stream stream, bool overwrite = true)
        {
            if (archive.StorageLocation == null)
                archive = await Include(a => a.StorageLocation).GetAsync(archive.Id);
            
            if (archive.StorageLocation == null)
                throw new ArgumentException("Archive has no storage location", nameof(archive));
            
            var path = GetArchiveFileLocation(archive, archive.StorageLocation);
            
            if (overwrite && File.Exists(path))
                File.Delete(path);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fs);
            }

            archive.CompressedSize = await GetCompressedSizeAsync(archive);
            archive.UncompressedSize = await GetUncompressedSizeAsync(archive);

            await UpdateAsync(archive);

            return archive;
        }
    }
}
