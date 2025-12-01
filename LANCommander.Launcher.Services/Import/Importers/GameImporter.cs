using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class GameImporter(
    GameService gameService,
    LibraryService libraryService,
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
            (c, rc) => c.Name == rc.Name,
            (c, rc) =>
            {
                c.Name = rc.Name;
                c.CreatedOn = rc.CreatedOn;
                c.UpdatedOn = rc.UpdatedOn;
                c.ImportedOn = DateTime.UtcNow;
            }, rc => new Data.Models.Collection
            {
                Name = rc.Name,
                CreatedOn = rc.CreatedOn,
                UpdatedOn = rc.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            });

        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Developers,
            record.Developers,
            (d, rd) => d.Name == rd.Name,
            (d, rd) =>
            {
                d.Name = rd.Name;
                d.CreatedOn = rd.CreatedOn;
                d.UpdatedOn = rd.UpdatedOn;
                d.ImportedOn = DateTime.UtcNow;
            },
            rd => new Data.Models.Company
            {
                Name = rd.Name,
                CreatedOn = rd.CreatedOn,
                UpdatedOn = rd.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            });

        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Genres,
            record.Genres,
            (g, gr) => g.Name == g.Name,
            (g, gr) =>
            {
                g.Name = gr.Name;
                g.CreatedOn = gr.CreatedOn;
                g.UpdatedOn = gr.UpdatedOn;
                g.ImportedOn = DateTime.UtcNow;
            },
            gr => new Data.Models.Genre
            {
                Name = gr.Name,
                CreatedOn = gr.CreatedOn,
                UpdatedOn = gr.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            });

        await gameService.SyncRelatedCollectionAsync(
            game, g => g.MultiplayerModes,
            record.MultiplayerModes,
            (mm, rmm) => mm.NetworkProtocol == rmm.NetworkProtocol && mm.Type == rmm.Type,
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
            },
            rm => new Data.Models.MultiplayerMode
            {
                Type = rm.Type,
                Description = rm.Description,
                MinPlayers = rm.MinPlayers,
                MaxPlayers = rm.MaxPlayers,
                NetworkProtocol = rm.NetworkProtocol,
                Spectators = rm.Spectators,
                CreatedOn = rm.CreatedOn,
                UpdatedOn = rm.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            });

        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Platforms,
            record.Platforms,
            (p, pr) => p.Name == pr.Name,
            (p, pr) =>
            {
                p.Name = pr.Name;
                p.CreatedOn = pr.CreatedOn;
                p.UpdatedOn = pr.UpdatedOn;
                p.ImportedOn = DateTime.UtcNow;
            },
            pr => new Data.Models.Platform
            {
                Name = pr.Name,
                CreatedOn = pr.CreatedOn,
                UpdatedOn = pr.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            });
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Publishers,
            record.Publishers,
            (p, pr) => p.Name == pr.Name,
            (p, pr) =>
            {
                p.Name = pr.Name;
                p.CreatedOn = pr.CreatedOn;
                p.UpdatedOn = pr.UpdatedOn;
                p.ImportedOn = DateTime.UtcNow;
            },
            pr => new Data.Models.Company
            {
                Name = pr.Name,
                CreatedOn = pr.CreatedOn,
                UpdatedOn = pr.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            });
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Tags,
            record.Tags,
            (t, tr) => t.Name == tr.Name,
            (t, tr) =>
            {
                t.Name = tr.Name;
                t.CreatedOn = tr.CreatedOn;
                t.UpdatedOn = tr.UpdatedOn;
                t.ImportedOn = DateTime.UtcNow;
            },
            tr => new Data.Models.Tag
            {
                Name = tr.Name,
                CreatedOn = tr.CreatedOn,
                UpdatedOn = tr.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            });
    }

    public override async Task<bool> ExistsAsync(Game record) => await gameService.ExistsAsync(record.Id);
}