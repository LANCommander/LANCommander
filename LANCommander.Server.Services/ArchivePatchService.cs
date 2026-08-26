using System.IO.Compression;
using AutoMapper;
using LANCommander.Helpers;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    /// <summary>
    /// Generates and manages <see cref="ArchivePatch"/> delta artifacts. Patches are always derived,
    /// read-only comparisons of two full, immutable <see cref="Archive"/> snapshots: generating one
    /// never rewrites either source archive, unlike the legacy in-place patching behavior it replaces.
    /// </summary>
    public sealed class ArchivePatchService(
        ILogger<ArchivePatchService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        ArchiveService archiveService,
        StorageLocationService storageLocationService) : BaseDatabaseService<ArchivePatch>(logger, settingsProvider, cache, mapper, httpContextAccessor, dbContextFactory)
    {
        public override async Task<ArchivePatch> AddAsync(ArchivePatch entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(p => p.FromArchive);
                await context.UpdateRelationshipAsync(p => p.ToArchive);
                await context.UpdateRelationshipAsync(p => p.StorageLocation);
            });
        }

        public override async Task<ArchivePatch> UpdateAsync(ArchivePatch entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(p => p.FromArchive);
                await context.UpdateRelationshipAsync(p => p.ToArchive);
                await context.UpdateRelationshipAsync(p => p.StorageLocation);
            });
        }

        public override async Task DeleteAsync(ArchivePatch patch)
        {
            FileHelpers.DeleteIfExists(await GetPatchFileLocationAsync(patch));

            await base.DeleteAsync(patch);
        }

        public string GetPatchFileLocation(ArchivePatch patch, StorageLocation storageLocation)
        {
            return AppPaths.ResolveStorageLocationPath(storageLocation.Path, patch.ObjectKey);
        }

        public async Task<string> GetPatchFileLocationAsync(ArchivePatch patch)
        {
            string storageLocationPath;

            if (patch.StorageLocation != null)
                storageLocationPath = patch.StorageLocation.Path;
            else
            {
                var storageLocation = await storageLocationService.GetAsync(patch.StorageLocationId);

                storageLocationPath = storageLocation.Path;
            }

            return AppPaths.ResolveStorageLocationPath(storageLocationPath, patch.ObjectKey);
        }

        /// <summary>
        /// Generates a delta artifact containing the files that are new or changed (by CRC32) between
        /// two full, immutable archives, without modifying either source archive. This mirrors the
        /// inclusion algorithm of the legacy (destructive) patcher -- only changed/new files are
        /// captured, deletions between versions are not tracked -- but the result is written as a
        /// standalone <see cref="ArchivePatch"/> artifact instead of being merged into the base archive.
        /// </summary>
        /// <param name="fromArchiveId">The older/base full archive.</param>
        /// <param name="toArchiveId">The newer/target full archive.</param>
        /// <param name="storageLocationId">
        /// Optional storage location override for the generated patch. Defaults to the target archive's
        /// storage location (falling back to the default archive storage location).
        /// </param>
        /// <param name="compressionLevel">Compression level used when writing patch entries.</param>
        /// <exception cref="ArgumentException">Thrown when both archive IDs are the same.</exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown when either archive record or its backing file cannot be found.
        /// </exception>
        public async Task<ArchivePatch> GeneratePatchAsync(
            Guid fromArchiveId,
            Guid toArchiveId,
            Guid? storageLocationId = null,
            CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (fromArchiveId == toArchiveId)
                throw new ArgumentException("An archive patch requires two distinct archives", nameof(toArchiveId));

            var fromArchive = await archiveService.Include(a => a.StorageLocation).GetAsync(fromArchiveId);
            var toArchive = await archiveService.Include(a => a.StorageLocation).GetAsync(toArchiveId);

            if (fromArchive == null)
                throw new FileNotFoundException($"Archive {fromArchiveId} could not be found");

            if (toArchive == null)
                throw new FileNotFoundException($"Archive {toArchiveId} could not be found");

            var fromPath = await archiveService.GetArchiveFileLocationAsync(fromArchive);
            var toPath = await archiveService.GetArchiveFileLocationAsync(toArchive);

            if (!File.Exists(fromPath))
                throw new FileNotFoundException("Source archive file not found", fromPath);

            if (!File.Exists(toPath))
                throw new FileNotFoundException("Target archive file not found", toPath);

            var storageLocation = await storageLocationService.GetOrDefaultAsync(
                storageLocationId ?? toArchive.StorageLocationId,
                StorageLocationType.Archive);

            if (storageLocation == null)
                throw new InvalidOperationException("No storage location is available for archive patches");

            var objectKey = Guid.NewGuid().ToString();
            var patchPath = AppPaths.ResolveStorageLocationPath(storageLocation.Path, objectKey);
            var patchDirectory = Path.GetDirectoryName(patchPath);

            if (!string.IsNullOrEmpty(patchDirectory) && !Directory.Exists(patchDirectory))
                Directory.CreateDirectory(patchDirectory);

            var tempPath = patchPath + ".tmp";

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            long uncompressedSize = 0;
            int entryCount = 0;

            try
            {
                using (var fromZip = ZipFile.OpenRead(fromPath))
                using (var toZip = ZipFile.OpenRead(toPath))
                using (var patchZip = ZipFile.Open(tempPath, ZipArchiveMode.Create))
                {
                    foreach (var entry in toZip.Entries)
                    {
                        var fromEntry = fromZip.GetEntry(entry.FullName);

                        // Only new or changed entries are included in the delta -- this matches the
                        // legacy patcher's inclusion rule. Entries removed between versions are not
                        // tracked; applying a patch always requires the full "from" archive as a base.
                        if (fromEntry != null && fromEntry.Crc32 == entry.Crc32)
                            continue;

                        var patchEntry = patchZip.CreateEntry(entry.FullName, compressionLevel);
                        patchEntry.LastWriteTime = entry.LastWriteTime;

                        using (var sourceStream = entry.Open())
                        using (var patchStream = patchEntry.Open())
                        {
                            await sourceStream.CopyToAsync(patchStream);
                        }

                        uncompressedSize += entry.Length;
                        entryCount++;

                        _logger?.LogInformation(
                            "Added {EntryFullName} to patch from archive {FromArchiveId} to archive {ToArchiveId}",
                            entry.FullName, fromArchive.Id, toArchive.Id);
                    }
                }

                if (File.Exists(patchPath))
                    File.Delete(patchPath);

                File.Move(tempPath, patchPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Could not generate patch from archive {FromArchiveId} to archive {ToArchiveId}",
                    fromArchive.Id, toArchive.Id);

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                throw;
            }

            _logger?.LogInformation(
                "Generated patch with {EntryCount} changed/new entries from archive {FromArchiveId} to archive {ToArchiveId}",
                entryCount, fromArchive.Id, toArchive.Id);

            var patch = new ArchivePatch
            {
                FromArchiveId = fromArchive.Id,
                ToArchiveId = toArchive.Id,
                ObjectKey = objectKey,
                StorageLocationId = storageLocation.Id,
                CompressedSize = new FileInfo(patchPath).Length,
                UncompressedSize = uncompressedSize,
            };

            return await AddAsync(patch);
        }
    }
}
