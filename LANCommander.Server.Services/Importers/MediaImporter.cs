using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class MediaImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Media, Data.Models.Media>
{
    StorageLocationService _storageLocationService = serviceProvider.GetRequiredService<StorageLocationService>();
    MediaService _mediaService = serviceProvider.GetRequiredService<MediaService>();
    
    public async Task<Data.Models.Media> AddAsync(Media record)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}");
        
        var defaultMediaLocation =
            await _storageLocationService.FirstOrDefaultAsync(l => l.Type == StorageLocationType.Media && l.Default);
        
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Media>(record, $"Cannot import media for a {typeof(TParentRecord).Name}");

        Data.Models.Media media = null;
        
        try
        {
            media = new Data.Models.Media
            {
                Game = game,
                CreatedOn = record.CreatedOn,
                Type = record.Type,
                UpdatedOn = record.UpdatedOn,
                StorageLocation = defaultMediaLocation,
            };

            media = await _mediaService.AddAsync(media);
            media = await _mediaService.WriteToFileAsync(media, archiveEntry.OpenEntryStream());

            return media;
        }
        catch (Exception ex)
        {
            if (media?.Id != Guid.Empty)
                await _mediaService.DeleteAsync(media);

            throw new ImportSkippedException<Media>(record, "An unknown error occured while trying to import media file",
                ex);
        }
    }

    public async Task<Data.Models.Media> UpdateAsync(Media record)
    {
        if (importContext.Record is not Game)
            throw new ImportSkippedException<Media>(record, $"Cannot import media for a {typeof(TParentRecord).Name}");

        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}");
        var existing = await _mediaService.Include(m => m.StorageLocation).FirstOrDefaultAsync(m => m.Type == record.Type && m.Game.Id == record.Id);
        var existingPath = MediaService.GetMediaPath(existing);
        
        if (archiveEntry == null)
            throw new ImportSkippedException<Media>(record, "Matching media file does not exist in import archive");

        try
        {
            existing.Name = record.Name;
            existing.MimeType = record.MimeType;
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedOn = record.UpdatedOn;

            existing = await _mediaService.UpdateAsync(existing);
            existing = await _mediaService.WriteToFileAsync(existing, archiveEntry.OpenEntryStream());

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
        if (importContext.Record is not Game game)
            throw new ImportSkippedException<Media>(media, $"Cannot import media for a {typeof(TParentRecord).Name}");
                
        return _mediaService.ExistsAsync(m => m.Type == media.Type && m.Id == media.Id);
    }
}