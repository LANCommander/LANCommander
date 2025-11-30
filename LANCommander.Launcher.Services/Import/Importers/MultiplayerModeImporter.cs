using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class MultiplayerModeImporter(
    MultiplayerModeService multiplayerModeService,
    ILogger<MultiplayerModeImporter> logger) : BaseImporter<MultiplayerMode, Data.Models.MultiplayerMode>
{
    public override async Task<ImportItemInfo<MultiplayerMode>> GetImportInfoAsync(MultiplayerMode record)
    {
        return new ImportItemInfo<MultiplayerMode>
        {
            Key  = GetKey(record),
            Name = $"{record.NetworkProtocol} - {record.Type}",
            Type = nameof(MultiplayerMode),
            Record = record,
        };
    }
    
    public override string GetKey(MultiplayerMode record) => $"{nameof(Collection)}/{record.NetworkProtocol}:{record.Type}";

    public override async Task<bool> CanImportAsync(MultiplayerMode record)
    {
        var existing = await multiplayerModeService.FirstOrDefaultAsync(mm => mm.Type == record.Type && mm.NetworkProtocol == record.NetworkProtocol);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<Data.Models.MultiplayerMode> AddAsync(MultiplayerMode record)
    {
        var multiplayerMode = new Data.Models.MultiplayerMode
        {
            Type = record.Type,
            Description = record.Description,
            MaxPlayers = record.MaxPlayers,
            MinPlayers = record.MinPlayers,
            NetworkProtocol = record.NetworkProtocol,
            Spectators = record.Spectators,
            CreatedOn = record.CreatedOn,
            UpdatedOn = record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            return await multiplayerModeService.AddAsync(multiplayerMode);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<MultiplayerMode>(record, "An unknown error occurred while trying to add multiplayer mode", ex);
        }
    }

    public override async Task<Data.Models.MultiplayerMode> UpdateAsync(MultiplayerMode record)
    {
        var existing = await multiplayerModeService.FirstOrDefaultAsync(mm => mm.Type == record.Type && mm.NetworkProtocol == record.NetworkProtocol);

        try
        {
            existing.Type = record.Type;
            existing.Description = record.Description;
            existing.MaxPlayers = record.MaxPlayers;
            existing.MinPlayers = record.MinPlayers;
            existing.NetworkProtocol = record.NetworkProtocol;
            existing.Spectators = record.Spectators;
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedOn = record.UpdatedOn;
            existing.ImportedOn = DateTime.UtcNow;

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<MultiplayerMode>(record, "An unknown error occurred while trying to update multiplayer mode", ex);
        }
    }

    public override async Task<bool> ExistsAsync(MultiplayerMode record) => await multiplayerModeService.ExistsAsync(mm => mm.Type == record.Type && mm.NetworkProtocol == record.NetworkProtocol);
}