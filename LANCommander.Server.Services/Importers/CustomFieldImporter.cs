using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class CustomFieldImporter(
    GameService gameService,
    ImportContext importContext) : IImporter<GameCustomField, Data.Models.GameCustomField>
{
    public async Task<ImportItemInfo> InfoAsync(GameCustomField record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.CustomFields,
            Name = record.Name,
        };
    }

    public bool CanImport(GameCustomField record) => importContext.DataRecord is Data.Models.Game;
    
    public async Task<Data.Models.GameCustomField> AddAsync(GameCustomField record)
    {
        try
        {
            var customField = await gameService.SetCustomFieldAsync(importContext.DataRecord.Id, record.Name, record.Value);

            return customField;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<GameCustomField>(record, "An unknown error occured while importing customField", ex);
        }
    }

    public async Task<Data.Models.GameCustomField> UpdateAsync(GameCustomField record)
    {
        var existing = await gameService.GetCustomFieldAsync(importContext.DataRecord.Id, record.Name);

        try
        {
            if (existing.Value != record.Value)
                existing = await gameService.SetCustomFieldAsync(importContext.DataRecord.Id, record.Name, record.Value);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<GameCustomField>(record, "An unknown error occured while importing customField", ex);
        }
    }

    public async Task<bool> ExistsAsync(GameCustomField record)
    {
        return (await gameService.GetCustomFieldAsync(importContext.DataRecord.Id, record.Name)) == null;
    }
}