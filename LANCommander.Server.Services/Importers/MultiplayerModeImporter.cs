using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class MultiplayerModeImporter(
    IMapper mapper,
    MultiplayerModeService multiplayerModeService) : BaseImporter<MultiplayerMode, Data.Models.MultiplayerMode>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(MultiplayerMode record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.MultiplayerModes,
            Name = String.IsNullOrWhiteSpace(record.Description) ? record.Type.ToString() : $"{record.Type} - {record.Description}",
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(MultiplayerMode record)
    {
        return new ExportItemInfo
        {
            Flag = ImportRecordFlags.MultiplayerModes,
            Name = String.IsNullOrWhiteSpace(record.Description) ? record.Type.ToString() : $"{record.Type} - {record.Description}",
        };
    }

    public override bool CanImport(MultiplayerMode record) => ImportContext.DataRecord is Data.Models.Game;
    public override bool CanExport(MultiplayerMode record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.MultiplayerMode> AddAsync(MultiplayerMode record)
    {
        try
        {
            var multiplayerMode = new Data.Models.MultiplayerMode
            {
                Game = ImportContext.DataRecord as Data.Models.Game,
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

    public override async Task<Data.Models.MultiplayerMode> UpdateAsync(MultiplayerMode record)
    {
        var game = ImportContext.DataRecord as Data.Models.Game;
        
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

    public override async Task<MultiplayerMode> ExportAsync(Data.Models.MultiplayerMode entity)
    {
        return mapper.Map<MultiplayerMode>(entity);
    }

    public override async Task<bool> ExistsAsync(MultiplayerMode record)
    {
        var game = ImportContext.DataRecord as Data.Models.Game;
        
        return await multiplayerModeService.ExistsAsync(m => m.GameId == game.Id && m.Type == record.Type);
    }
}