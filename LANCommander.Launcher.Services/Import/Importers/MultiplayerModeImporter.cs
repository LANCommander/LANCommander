using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class MultiplayerModeImporter(
    MultiplayerModeService multiplayerModeService,
    ILogger<MultiplayerModeImporter> logger) : BaseImporter<MultiplayerMode>
{
    public override async Task<ImportItemInfo<MultiplayerMode>> GetImportInfoAsync(MultiplayerMode record, BaseManifest manifest) =>
        new()
        {
            Key  = GetKey(record),
            Name = $"{record.NetworkProtocol} - {record.Type}",
            Type = nameof(MultiplayerMode),
            Record = record,
        };

    public override string GetKey(MultiplayerMode record) => $"{nameof(Collection)}/{record.NetworkProtocol}:{record.Type}";

    public override async Task<bool> CanImportAsync(MultiplayerMode record)
    {
        var existing = await multiplayerModeService.FirstOrDefaultAsync(mm => mm.Type == record.Type && mm.NetworkProtocol == record.NetworkProtocol);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<bool> AddAsync(ImportItemInfo<MultiplayerMode> importItemInfo)
    {
        var multiplayerMode = new Data.Models.MultiplayerMode
        {
            Type = importItemInfo.Record.Type,
            Description = importItemInfo.Record.Description,
            MaxPlayers = importItemInfo.Record.MaxPlayers,
            MinPlayers = importItemInfo.Record.MinPlayers,
            NetworkProtocol = importItemInfo.Record.NetworkProtocol,
            Spectators = importItemInfo.Record.Spectators,
            CreatedOn = importItemInfo.Record.CreatedOn,
            UpdatedOn = importItemInfo.Record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            await multiplayerModeService.AddAsync(multiplayerMode);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add multiplayer mode | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<MultiplayerMode> importItemInfo)
    {
        var existing = await multiplayerModeService.FirstOrDefaultAsync(mm => mm.Type == importItemInfo.Record.Type && mm.NetworkProtocol == importItemInfo.Record.NetworkProtocol);

        try
        {
            existing.Type = importItemInfo.Record.Type;
            existing.Description = importItemInfo.Record.Description;
            existing.MaxPlayers = importItemInfo.Record.MaxPlayers;
            existing.MinPlayers = importItemInfo.Record.MinPlayers;
            existing.NetworkProtocol = importItemInfo.Record.NetworkProtocol;
            existing.Spectators = importItemInfo.Record.Spectators;
            existing.CreatedOn = importItemInfo.Record.CreatedOn;
            existing.UpdatedOn = importItemInfo.Record.UpdatedOn;
            existing.ImportedOn = DateTime.UtcNow;

            await multiplayerModeService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update multiplayer mode | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(ImportItemInfo<MultiplayerMode> importItemInfo)
        => await multiplayerModeService.ExistsAsync(mm => mm.Type == importItemInfo.Record.Type && mm.NetworkProtocol == importItemInfo.Record.NetworkProtocol);
}