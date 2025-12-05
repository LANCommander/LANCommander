using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class MediaImporter(
    ILogger<MediaImporter> logger,
    StorageLocationService storageLocationService,
    MediaService mediaService,
    GameService gameService,
    GameImporter gameImporter) : BaseImporter<Media>
{
    public override string GetKey(Media record)
        => $"{nameof(Media)}/{record.Id}";

    public override async Task<ImportItemInfo<Media>> GetImportInfoAsync(Media record)
    {
        return new ImportItemInfo<Media>
        {
            Type = ImportExportRecordType.Media,
            Name = String.IsNullOrWhiteSpace(record.Name) ? record.Type.ToString() : $"{record.Type} - {record.Name}",
            Size = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}")?.Size ?? 0,
            Record = record,
        };
    }

    public override async Task<bool> CanImportAsync(Media record) => ImportContext.Manifest is Game;

    public override async Task<bool> AddAsync(Media record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}");
        
        var defaultMediaLocation =
            await storageLocationService.DefaultAsync(StorageLocationType.Media);

        Data.Models.Media media = null;
        
        if (archiveEntry != null)
            AddAsset(new ImportAssetArchiveEntry
            {
                RecordId = record.Id,
                Name = record.Type.ToString(),
                Path = archiveEntry.Key!,
            });
        
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;
            
            media = new Data.Models.Media
            {
                Id = record.Id,
                FileId = record.FileId,
                Game = await gameService.GetAsync(game.Id),
                CreatedOn = record.CreatedOn,
                Type = record.Type,
                UpdatedOn = record.UpdatedOn,
                StorageLocation = defaultMediaLocation,
                SourceUrl = record.SourceUrl,
                MimeType = record.MimeType,
                Crc32 = record.Crc32,
            };

            media = await mediaService.AddAsync(media);

            return true;
        }
        catch (Exception ex)
        {
            if (media?.Id != Guid.Empty)
                await mediaService.DeleteAsync(media);
            
            logger.LogError(ex, "An unknown error occured while trying to import media file | {Key}", GetKey(record));

            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Media record)
    {
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;
            
            var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}");
            var existing = await mediaService.Include(m => m.StorageLocation).FirstOrDefaultAsync(m => m.Type == record.Type && m.Id == record.Id);
            
            if (archiveEntry != null)
                AddAsset(new ImportAssetArchiveEntry
                {
                    RecordId = record.Id,
                    Name = record.Type.ToString(),
                    Path = archiveEntry.Key!,
                });

            existing.FileId = record.FileId;
            existing.Game = await gameService.GetAsync(game.Id);
            existing.Name = record.Name;
            existing.MimeType = record.MimeType;
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedOn = record.UpdatedOn;
            existing.Crc32 = record.Crc32;
            existing.SourceUrl = record.SourceUrl;
            
            if (existing.StorageLocation == null)
                existing.StorageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Media);

            existing = await mediaService.UpdateAsync(existing);
            
            await mediaService.WriteToFileAsync(existing, archiveEntry.OpenEntryStream(), true);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update media | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        var media = await mediaService.Include(m => m.StorageLocation).GetAsync(asset.RecordId);
        
        if (media.StorageLocation == null)
            media.StorageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Media);
        
        await mediaService.UpdateAsync(media);
        
        if (asset is ImportAssetArchiveEntry archiveEntryAsset)
        {
            var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == archiveEntryAsset.Path);
            
            await mediaService.WriteToFileAsync(media, archiveEntry.OpenEntryStream(), true);

            return true;
        }
        
        if (asset is ImportAssetExternalDownload externalDownloadAsset)
        {
            await mediaService.DownloadMediaAsync(externalDownloadAsset.SourceUrl, media);
            
            return true;
        }

        return false;
    }

    public override Task<bool> ExistsAsync(Media media)
        => mediaService.ExistsAsync(m => m.Type == media.Type && m.Id == media.Id);
}