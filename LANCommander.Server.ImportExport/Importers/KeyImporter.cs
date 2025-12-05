using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class KeyImporter(
    ILogger<KeyImporter> logger,
    KeyService keyService,
    GameImporter gameImporter,
    GameService gameService) : BaseImporter<Key>
{
    public override string GetKey(Key record)
        => $"{nameof(Key)}/{record.Value}";

    public override async Task<ImportItemInfo<Key>> GetImportInfoAsync(Key record)
    {
        return new ImportItemInfo<Key>
        {
            Type = ImportExportRecordType.Key,
            Name = new String('*', record.Value.Length),
            Record = record,
        };
    }

    public override async Task<bool> CanImportAsync(Key record) => ImportContext.Manifest is Game;

    public override async Task<bool> AddAsync(Key record)
    {
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;
            
            var key = new Data.Models.Key
            {
                Game = await gameService.GetAsync(game.Id),
                AllocationMethod = record.AllocationMethod,
                ClaimedByComputerName = record.ClaimedByComputerName,
                ClaimedByIpv4Address = record.ClaimedByIpv4Address,
                ClaimedByMacAddress = record.ClaimedByMacAddress,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
            };

            await keyService.AddAsync(key);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add key | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Key record)
    {
        var existing = await keyService.FirstOrDefaultAsync(k => k.Value == record.Value);

        try
        {
            existing.AllocationMethod = record.AllocationMethod;
            existing.ClaimedByComputerName = record.ClaimedByComputerName;
            existing.ClaimedByIpv4Address = record.ClaimedByIpv4Address;
            existing.ClaimedByMacAddress = record.ClaimedByMacAddress;
            
            await keyService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update key | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> ExistsAsync(Key record)
    {
        if (ImportContext.Manifest is Game game)
            return await keyService.ExistsAsync(k => k.Value == record.Value && k.GameId == game.Id);
        
        return false;
    }
}