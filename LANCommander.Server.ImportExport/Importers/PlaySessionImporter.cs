using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class PlaySessionImporter(
    ILogger<PlaySessionImporter> logger,
    PlaySessionService playSessionService,
    UserService userService,
    GameService gameService,
    GameImporter gameImporter) : BaseImporter<PlaySession>
{
    public override string GetKey(PlaySession record)
        => $"{nameof(PlaySession)}/{record.User}:{record.Start}:{record.End}";

    public override async Task<ImportItemInfo<PlaySession>> GetImportInfoAsync(PlaySession record)
        => new()
        {
            Type = ImportExportRecordType.PlaySession,
            Name = $"{record.User} - {record.Start}-{record.End}",
            Record = record,
        };

    public override async Task<bool> CanImportAsync(PlaySession record) => ImportContext.Manifest is Game;

    public override async Task<bool> AddAsync(PlaySession record)
    {
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;
            
            var playSession = new Data.Models.PlaySession
            {
                Start = record.Start,
                End = record.End,
                User = await userService.GetAsync(record.User),
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
                Game = await gameService.GetAsync(game.Id),
            };

            await playSessionService.AddAsync(playSession);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add play session | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(PlaySession record)
    {
        try
        {
            var user = await userService.GetAsync(record.User);
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;
            
            var existing = await playSessionService.FirstOrDefaultAsync(ps => ps.GameId == game.Id && ps.Start == record.Start && ps.UserId == user.Id);
            
            existing.Start = record.Start;
            existing.End = record.End;
            
            await playSessionService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update play session | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> ExistsAsync(PlaySession record)
    {
        if (ImportContext.Manifest is Game game)
        {
            var user = await userService.GetAsync(record.User);
            
            return await playSessionService.ExistsAsync(ps => (ps.Game.Id == game.Id || ps.Game.Title == game.Title) && ps.Start == record.Start && ps.UserId == user.Id); 
        }

        return false;
    }
}