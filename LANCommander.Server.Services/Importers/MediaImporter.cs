using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class MediaImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Media>
{
    StorageLocationService _storageLocationService = serviceProvider.GetRequiredService<StorageLocationService>();
    MediaService _mediaService = serviceProvider.GetRequiredService<MediaService>();
    
    public async Task<Media> AddAsync(Media media)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{media.Id}");
        
        var defaultMediaLocation =
            await _storageLocationService.FirstOrDefaultAsync(l => l.Type == StorageLocationType.Media && l.Default);
        
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Media>(media, $"Cannot import media for a {typeof(TParentRecord).Name}");

        Data.Models.Media mediaModel = null;
        
        try
        {
            mediaModel = new Data.Models.Media
            {
                Game = game,
                CreatedOn = media.CreatedOn,
                Type = media.Type,
                UpdatedOn = media.UpdatedOn,
                StorageLocation = defaultMediaLocation,
            };

            mediaModel = await _mediaService.AddAsync(mediaModel);
            mediaModel = await _mediaService.WriteToFileAsync(mediaModel, archiveEntry.OpenEntryStream());

            return media;
        }
        catch (Exception ex)
        {
            if (mediaModel?.Id != Guid.Empty)
                await _mediaService.DeleteAsync(mediaModel);

            throw new ImportSkippedException<Media>(media, "An unknown error occured while trying to import media file",
                ex);
        }
    }

    public async Task<Media> UpdateAsync(Media media)
    {
        if (importContext.Record is not Game)
            throw new ImportSkippedException<Media>(media, $"Cannot import media for a {typeof(TParentRecord).Name}");

        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{media.Id}");
        var existing = await _mediaService.Include(m => m.StorageLocation).FirstOrDefaultAsync(m => m.Type == media.Type && m.Game.Id == media.Id);
        var existingPath = MediaService.GetMediaPath(existing);
        
        if (archiveEntry == null)
            throw new ImportSkippedException<Media>(media, "Matching media file does not exist in import archive");

        try
        {
            existing.Name = media.Name;
            existing.MimeType = media.MimeType;
            existing.CreatedOn = media.CreatedOn;
            existing.UpdatedOn = media.UpdatedOn;

            existing = await _mediaService.UpdateAsync(existing);
            existing = await _mediaService.WriteToFileAsync(existing, archiveEntry.OpenEntryStream());

            if (File.Exists(existingPath))
                File.Delete(existingPath);

            return media;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Media>(media, "An unknown error occured while importing media file", ex);
        }
    }

    public Task<bool> ExistsAsync(Media media)
    {
        if (importContext.Record is not Game)
            throw new ImportSkippedException<Media>(media, $"Cannot import media for a {typeof(TParentRecord).Name}");
                
        return _mediaService.ExistsAsync(m => m.Type == media.Type && m.Game.Id == media.Id);
    }
}