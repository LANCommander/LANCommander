using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class MultiplayerModeImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<MultiplayerMode, Data.Models.MultiplayerMode>
{
    MultiplayerModeService _multiplayerModeService = serviceProvider.GetRequiredService<MultiplayerModeService>();
    
    public async Task<Data.Models.MultiplayerMode> AddAsync(MultiplayerMode record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<MultiplayerMode>(record, $"Cannot import multiplayer mode for a {typeof(TParentRecord).Name}");

        try
        {
            var multiplayerMode = new Data.Models.MultiplayerMode
            {
                Game = game,
                Description = record.Description,
                Type = record.Type,
                Spectators = record.Spectators,
                MinPlayers = record.MinPlayers,
                MaxPlayers = record.MaxPlayers,
                NetworkProtocol = record.NetworkProtocol,
            };

            multiplayerMode = await _multiplayerModeService.AddAsync(multiplayerMode);

            return multiplayerMode;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<MultiplayerMode>(record, "An unknown error occured while importing multiplayer mode", ex);
        } 
    }

    public async Task<Data.Models.MultiplayerMode> UpdateAsync(MultiplayerMode record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<MultiplayerMode>(record, $"Cannot import multiplayer modes for a {typeof(TParentRecord).Name}");

        var existing = await _multiplayerModeService.FirstOrDefaultAsync(m => m.GameId == game.Id && m.Type == record.Type);

        try
        {
            existing.Description = record.Description;
            existing.Type = record.Type;
            existing.Spectators = record.Spectators;
            existing.MinPlayers = record.MinPlayers;
            existing.MaxPlayers = record.MaxPlayers;
            existing.NetworkProtocol = record.NetworkProtocol;
            
            existing = await _multiplayerModeService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<MultiplayerMode>(record, "An unknown error occured while importing multiplayer mode", ex);
        }
    }

    public async Task<bool> ExistsAsync(MultiplayerMode record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<MultiplayerMode>(record, $"Cannot import multiplayer modes for a {typeof(TParentRecord).Name}");
        
        return await _multiplayerModeService.ExistsAsync(m => m.GameId == game.Id && m.Type == record.Type);
    }
}