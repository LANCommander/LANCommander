using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class MediaImporter(
    StorageLocationService storageLocationService,
    MediaService mediaService,
    ImportContext importContext) : IImporter<Media, Data.Models.Media>
{
    public async Task<ImportItemInfo> InfoAsync(Media record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Media,
            Name = String.IsNullOrWhiteSpace(record.Name) ? record.Type.ToString() : $"{record.Type} - {record.Name}",
            Size = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}")?.Size ?? 0,
        };
    }

    public bool CanImport(Media record) => importContext.DataRecord is Data.Models.Game;
    
    public async Task<Data.Models.Media> AddAsync(Media record)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}");
        
        var defaultMediaLocation =
            await storageLocationService.FirstOrDefaultAsync(l => l.Type == StorageLocationType.Media && l.Default);

        Data.Models.Media media = null;
        
        try
        {
            media = new Data.Models.Media
            {
                Game = importContext.DataRecord as Data.Models.Game,
                CreatedOn = record.CreatedOn,
                Type = record.Type,
                UpdatedOn = record.UpdatedOn,
                StorageLocation = defaultMediaLocation,
            };

            media = await mediaService.AddAsync(media);
            media = await mediaService.WriteToFileAsync(media, archiveEntry.OpenEntryStream());

            return media;
        }
        catch (Exception ex)
        {
            if (media?.Id != Guid.Empty)
                await mediaService.DeleteAsync(media);

            throw new ImportSkippedException<Media>(record, "An unknown error occured while trying to import media file",
                ex);
        }
    }

    public async Task<Data.Models.Media> UpdateAsync(Media record)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}");
        var existing = await mediaService.Include(m => m.StorageLocation).FirstOrDefaultAsync(m => m.Type == record.Type && m.Game.Id == record.Id);
        var existingPath = MediaService.GetMediaPath(existing);
        
        if (archiveEntry == null)
            throw new ImportSkippedException<Media>(record, "Matching media file does not exist in import archive");

        try
        {
            existing.Game = importContext.DataRecord as Data.Models.Game;
            existing.Name = record.Name;
            existing.MimeType = record.MimeType;
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedOn = record.UpdatedOn;

            existing = await mediaService.UpdateAsync(existing);
            existing = await mediaService.WriteToFileAsync(existing, archiveEntry.OpenEntryStream());

            if (File.Exists(existingPath))
                File.Delete(existingPath);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Media>(record, "An unknown error occured while importing media file", ex);
        }
    }

    public Task<bool> ExistsAsync(Media media)
    {
        return mediaService.ExistsAsync(m => m.Type == media.Type && m.Id == media.Id);
    }
}