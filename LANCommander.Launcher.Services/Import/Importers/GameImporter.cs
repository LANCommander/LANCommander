using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class GameImporter(
    GameService gameService,
    LibraryService libraryService,
    MediaService mediaService,
    MediaClient mediaClient,
    ILogger<GameImporter> logger) : BaseImporter<Game, Data.Models.Game>
{
    public override async Task<ImportItemInfo<Game>> GetImportInfoAsync(Game record)
    {
        return new ImportItemInfo<Game>
        {
            Key = GetKey(record),
            Name = record.Title,
            Type = nameof(Game),
            Record = record,
        };
    }
    
    public override string GetKey(Game record) => $"{nameof(Game)}/{record.Id}";

    public override async Task<bool> CanImportAsync(Game record)
    {
        var existing = await gameService.GetAsync(record.Id);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<Data.Models.Game> AddAsync(Game record)
    {
        try
        {
            var game = new Data.Models.Game
            {
                Id = record.Id,
                Title = record.Title,
                SortTitle = record.SortTitle,
                Description = record.Description,
                Notes = record.Notes,
                ReleasedOn = record.ReleasedOn,
                Singleplayer = record.Singleplayer,
                Type = record.Type,
                IGDBId = record.IGDBId,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            };
            
            game = await gameService.AddAsync(game);

            await UpdateRelationships(game, record);
            
            await libraryService.AddToLibraryAsync(game);

            return game;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to add game", ex);
        }
    }

    public override async Task<Data.Models.Game> UpdateAsync(Game record)
    {
        var existing = await gameService.GetAsync(record.Id);

        try
        {
            existing.Title = record.Title;
            existing.SortTitle = record.SortTitle;
            existing.Description = record.Description;
            existing.Notes = record.Notes;
            existing.ReleasedOn = record.ReleasedOn;
            existing.Singleplayer = record.Singleplayer;
            existing.Type = record.Type;
            existing.IGDBId = record.IGDBId;
            existing.CreatedOn = record.CreatedOn;
            existing.ImportedOn = DateTime.UtcNow;
            
            await gameService.UpdateAsync(existing);
            
            await UpdateRelationships(existing, record);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to update game", ex);
        }
    }

    private async Task UpdateRelationships(Data.Models.Game game, Game record)
    {
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Collections,
            record.Collections,
            r => c => c.Name == r.Name,
            (c, rc) =>
            {
                c.Name = rc.Name;
                c.CreatedOn = rc.CreatedOn;
                c.UpdatedOn = rc.UpdatedOn;
                c.ImportedOn = DateTime.UtcNow;
            });

        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Developers,
            record.Developers,
            r => c => c.Name == r.Name,
            (d, rd) =>
            {
                d.Name = rd.Name;
                d.CreatedOn = rd.CreatedOn;
                d.UpdatedOn = rd.UpdatedOn;
                d.ImportedOn = DateTime.UtcNow;
            });

        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Genres,
            record.Genres,
            r => g => g.Name == r.Name,
            (g, gr) =>
            {
                g.Name = gr.Name;
                g.CreatedOn = gr.CreatedOn;
                g.UpdatedOn = gr.UpdatedOn;
                g.ImportedOn = DateTime.UtcNow;
            });

        await gameService.SyncRelatedCollectionAsync(
            game, g => g.MultiplayerModes,
            record.MultiplayerModes,
            r => mm => mm.NetworkProtocol == r.NetworkProtocol && mm.Type == r.Type,
            (mm, rmm) =>
            {
                mm.Description = rmm.Description;
                mm.MinPlayers = rmm.MinPlayers;
                mm.MaxPlayers = rmm.MaxPlayers;
                mm.NetworkProtocol = rmm.NetworkProtocol;
                mm.Spectators = rmm.Spectators;
                mm.CreatedOn = rmm.CreatedOn;
                mm.UpdatedOn = rmm.UpdatedOn;
                mm.ImportedOn = DateTime.UtcNow;
            });

        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Platforms,
            record.Platforms,
            r => p => p.Name == r.Name,
            (p, pr) =>
            {
                p.Name = pr.Name;
                p.CreatedOn = pr.CreatedOn;
                p.UpdatedOn = pr.UpdatedOn;
                p.ImportedOn = DateTime.UtcNow;
            });
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Publishers,
            record.Publishers,
            r => p => p.Name == r.Name,
            (p, pr) =>
            {
                p.Name = pr.Name;
                p.CreatedOn = pr.CreatedOn;
                p.UpdatedOn = pr.UpdatedOn;
                p.ImportedOn = DateTime.UtcNow;
            });
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Tags,
            record.Tags,
            r => t => t.Name == r.Name,
            (t, tr) =>
            {
                t.Name = tr.Name;
                t.CreatedOn = tr.CreatedOn;
                t.UpdatedOn = tr.UpdatedOn;
                t.ImportedOn = DateTime.UtcNow;
            });

        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Media,
            record.Media,
            r => m => m.Id == r.Id, async (m, mr) =>
            {
                m.Name = mr.Name;
                m.Type = m.Type;
                m.FileId = mr.FileId;
                m.Crc32 = mr.Crc32;
                m.MimeType = mr.MimeType;
                m.SourceUrl = mr.SourceUrl;
                m.Id = mr.Id;

                var path = mediaService.GetImagePath(m);

                if (!File.Exists(path))
                    await mediaService.DownloadAsync(m);
            });
    }

    public override async Task<bool> ExistsAsync(Game record) => await gameService.ExistsAsync(record.Id);
}