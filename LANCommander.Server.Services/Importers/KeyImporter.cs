using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class KeyImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Key, Data.Models.Key>
{
    KeyService _keyService = serviceProvider.GetRequiredService<KeyService>();
    
    public async Task<Data.Models.Key> AddAsync(Key record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Key>(record, $"Cannot import keys for a {typeof(TParentRecord).Name}");

        try
        {
            var key = new Data.Models.Key
            {
                Game = game,
                AllocationMethod = record.AllocationMethod,
                ClaimedByComputerName = record.ClaimedByComputerName,
                ClaimedByIpv4Address = record.ClaimedByIpv4Address,
                ClaimedByMacAddress = record.ClaimedByMacAddress,
            };

            key = await _keyService.AddAsync(key);

            return key;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Key>(record, "An unknown error occured while importing key", ex);
        }
    }

    public async Task<Data.Models.Key> UpdateAsync(Key record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Key>(record, $"Cannot import keys for a {typeof(TParentRecord).Name}");

        var existing = await _keyService.FirstOrDefaultAsync(k => k.Value == record.Value);

        try
        {
            existing.AllocationMethod = record.AllocationMethod;
            existing.ClaimedByComputerName = record.ClaimedByComputerName;
            existing.ClaimedByIpv4Address = record.ClaimedByIpv4Address;
            existing.ClaimedByMacAddress = record.ClaimedByMacAddress;
            
            existing = await _keyService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Key>(record, "An unknown error occured while importing key", ex);
        }
    }

    public async Task<bool> ExistsAsync(Key record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Key>(record, $"Cannot import keys for a {typeof(TParentRecord).Name}");
        
        return await _keyService.ExistsAsync(k => k.Value == record.Value);
    }
}