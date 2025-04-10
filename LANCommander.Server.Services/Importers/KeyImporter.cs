using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.Services.Importers;

public class KeyImporter<TParentRecord>(
    KeyService keyService,
    ImportContext<TParentRecord> importContext) : IImporter<Key, Data.Models.Key>
    where TParentRecord : Data.Models.BaseModel
{
    public async Task<ImportItemInfo> InfoAsync(Key record)
    {
        return new ImportItemInfo
        {
            Name = new String('*', record.Value.Length),
        };
    }

    public bool CanImport(Key record) => importContext.Record is Data.Models.Game;
    
    public async Task<Data.Models.Key> AddAsync(Key record)
    {
        try
        {
            var key = new Data.Models.Key
            {
                Game = importContext.Record as Data.Models.Game,
                AllocationMethod = record.AllocationMethod,
                ClaimedByComputerName = record.ClaimedByComputerName,
                ClaimedByIpv4Address = record.ClaimedByIpv4Address,
                ClaimedByMacAddress = record.ClaimedByMacAddress,
            };

            key = await keyService.AddAsync(key);

            return key;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Key>(record, "An unknown error occured while importing key", ex);
        }
    }

    public async Task<Data.Models.Key> UpdateAsync(Key record)
    {
        var existing = await keyService.FirstOrDefaultAsync(k => k.Value == record.Value);

        try
        {
            existing.AllocationMethod = record.AllocationMethod;
            existing.ClaimedByComputerName = record.ClaimedByComputerName;
            existing.ClaimedByIpv4Address = record.ClaimedByIpv4Address;
            existing.ClaimedByMacAddress = record.ClaimedByMacAddress;
            
            existing = await keyService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Key>(record, "An unknown error occured while importing key", ex);
        }
    }

    public async Task<bool> ExistsAsync(Key record)
    {
        return await keyService.ExistsAsync(k => k.Value == record.Value);
    }
}