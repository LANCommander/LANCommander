using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class MultiplayerModeImporter(
    MultiplayerModeService multiplayerModeService,
    ImportContext importContext) : IImporter<MultiplayerMode, Data.Models.MultiplayerMode>
{
    public async Task<ImportItemInfo> InfoAsync(MultiplayerMode record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.MultiplayerModes,
            Name = String.IsNullOrWhiteSpace(record.Description) ? record.Type.ToString() : $"{record.Type} - {record.Description}",
        };
    }

    public bool CanImport(MultiplayerMode record) => importContext.DataRecord is Data.Models.Game;

    public async Task<Data.Models.MultiplayerMode> AddAsync(MultiplayerMode record)
    {
        try
        {
            var multiplayerMode = new Data.Models.MultiplayerMode
            {
                Game = importContext.DataRecord as Data.Models.Game,
                Description = record.Description,
                Type = record.Type,
                Spectators = record.Spectators,
                MinPlayers = record.MinPlayers,
                MaxPlayers = record.MaxPlayers,
                NetworkProtocol = record.NetworkProtocol,
            };

            multiplayerMode = await multiplayerModeService.AddAsync(multiplayerMode);

            return multiplayerMode;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<MultiplayerMode>(record, "An unknown error occured while importing multiplayer mode", ex);
        } 
    }

    public async Task<Data.Models.MultiplayerMode> UpdateAsync(MultiplayerMode record)
    {
        var game = importContext.DataRecord as Data.Models.Game;
        
        var existing = await multiplayerModeService.FirstOrDefaultAsync(m => m.GameId == game.Id && m.Type == record.Type);

        try
        {
            existing.Description = record.Description;
            existing.Type = record.Type;
            existing.Spectators = record.Spectators;
            existing.MinPlayers = record.MinPlayers;
            existing.MaxPlayers = record.MaxPlayers;
            existing.NetworkProtocol = record.NetworkProtocol;
            
            existing = await multiplayerModeService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<MultiplayerMode>(record, "An unknown error occured while importing multiplayer mode", ex);
        }
    }

    public async Task<bool> ExistsAsync(MultiplayerMode record)
    {
        var game = importContext.DataRecord as Data.Models.Game;
        
        return await multiplayerModeService.ExistsAsync(m => m.GameId == game.Id && m.Type == record.Type);
    }
}