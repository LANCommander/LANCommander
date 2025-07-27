using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class KeyImporter(
    IMapper mapper,
    KeyService keyService) : BaseImporter<Key, Data.Models.Key>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Key record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Keys,
            Name = new String('*', record.Value.Length),
        };
    }

    public override bool CanImport(Key record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.Key> AddAsync(Key record)
    {
        try
        {
            var key = new Data.Models.Key
            {
                Game = ImportContext.DataRecord as Data.Models.Game,
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

    public override async Task<Data.Models.Key> UpdateAsync(Key record)
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

    public override async Task<bool> ExistsAsync(Key record)
    {
        return await keyService.ExistsAsync(k => k.Value == record.Value);
    }
}