using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class MultiplayerModeImporter(
    ILogger<MultiplayerModeImporter> logger,
    MultiplayerModeService multiplayerModeService,
    GameService gameService,
    GameImporter gameImporter) : BaseImporter<MultiplayerMode>
{
    public override string GetKey(MultiplayerMode record)
        => $"{nameof(MultiplayerMode)}/{record.NetworkProtocol}:{record.Type}";

    public override async Task<ImportItemInfo<MultiplayerMode>> GetImportInfoAsync(MultiplayerMode record) 
        => new()
        {
            Type = ImportExportRecordType.MultiplayerMode,
            Name = String.IsNullOrWhiteSpace(record.Description) ? record.Type.ToString() : $"{record.Type} - {record.Description}",
            Record = record,
        };

    public override async Task<bool> CanImportAsync(MultiplayerMode record) => ImportContext.Manifest is Game;

    public override async Task<bool> AddAsync(MultiplayerMode record)
    {
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;
            
            var multiplayerMode = new Data.Models.MultiplayerMode
            {
                Description = record.Description,
                Type = record.Type,
                Spectators = record.Spectators,
                MinPlayers = record.MinPlayers,
                MaxPlayers = record.MaxPlayers,
                NetworkProtocol = record.NetworkProtocol,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
                Game = await gameService.GetAsync(game.Id),
            };

            await multiplayerModeService.AddAsync(multiplayerMode);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add multiplayer mode | {Key}", GetKey(record));
            return false;
        } 
    }

    public override async Task<bool> UpdateAsync(MultiplayerMode record)
    {
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;
            
            var existing = await multiplayerModeService.FirstOrDefaultAsync(m => m.GameId == game.Id && m.Type == record.Type);
            
            existing.Description = record.Description;
            existing.Type = record.Type;
            existing.Spectators = record.Spectators;
            existing.MinPlayers = record.MinPlayers;
            existing.MaxPlayers = record.MaxPlayers;
            existing.NetworkProtocol = record.NetworkProtocol;
            
            await multiplayerModeService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update multiplayer mode | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(MultiplayerMode record)
    {
        if (ImportContext.Manifest is Game game)
            return await multiplayerModeService.ExistsAsync(m => m.GameId == game.Id && m.Type == record.Type);

        return false;
    }
}