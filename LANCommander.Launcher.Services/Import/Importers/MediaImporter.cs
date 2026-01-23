using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class MediaImporter(
    ILogger<MediaImporter> logger,
    MediaService mediaService,
    GameService gameService,
    GameImporter gameImporter) : BaseImporter<Media>
{
    public override string GetKey(Media record)
        => $"{nameof(Media)}/{record.Id}";

    public override async Task<ImportItemInfo<Media>> GetImportInfoAsync(Media record, BaseManifest manifest) =>
        new()
        {
            Type = nameof(Media),
            Name = string.IsNullOrWhiteSpace(record.Name) ? record.Type.ToString() : $"{record.Type} - {record.Name}",
            Record = record,
            Manifest = manifest,
        };

    public override async Task<bool> CanImportAsync(Media record)
    {
        var existing = await mediaService.GetAsync(record.Id);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<bool> AddAsync(ImportItemInfo<Media> importItemInfo)
    {
        try
        {
            if (importItemInfo.Manifest is not Game game)
                return false;

            if (ImportContext is null)
            {
                throw new InvalidOperationException("The ImportContext has not been set. Ensure that the UseContext method is called before importing.");
            }

            if (ImportContext.InQueue(game, gameImporter))
                return false;

            var media = new Data.Models.Media
            {
                Id = importItemInfo.Record.Id,
                FileId = importItemInfo.Record.FileId,
                Game = await gameService.GetAsync(game.Id),
                CreatedOn = importItemInfo.Record.CreatedOn,
                Type = importItemInfo.Record.Type,
                UpdatedOn = importItemInfo.Record.UpdatedOn,
                SourceUrl = importItemInfo.Record.SourceUrl,
                MimeType = importItemInfo.Record.MimeType,
                Crc32 = importItemInfo.Record.Crc32 ?? string.Empty,
            };

            media = await mediaService.AddAsync(media);

            await mediaService.DownloadAsync(media);

            return true;
        }
        catch(InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to add media due to invalid operation | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add media | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<Media> importItemInfo)
    {
        var game = importItemInfo.Manifest as Game;

        if (game == null)
            return false;

        if (ImportContext.InQueue(game, gameImporter))
            return false;
        
        var existing = await mediaService.GetAsync(importItemInfo.Record.Id);
        
        existing.FileId = importItemInfo.Record.FileId;
        existing.Game = await gameService.GetAsync(game.Id);
        existing.CreatedOn = importItemInfo.Record.CreatedOn;
        existing.Type = importItemInfo.Record.Type;
        existing.UpdatedOn = importItemInfo.Record.UpdatedOn;
        existing.SourceUrl = importItemInfo.Record.SourceUrl;
        existing.MimeType = importItemInfo.Record.MimeType;
        existing.Crc32 = importItemInfo.Record.Crc32 ?? string.Empty;

        await mediaService.UpdateAsync(existing);
        await mediaService.DownloadAsync(existing);

        return true;
    }

    public override async Task<bool> ExistsAsync(ImportItemInfo<Media> importItemInfo) =>
        await mediaService.GetAsync(importItemInfo.Record.Id) != null;
}