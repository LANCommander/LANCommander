using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class CustomFieldImporter<TParentRecord>(
    GameService gameService,
    ImportContext<TParentRecord> importContext) : IImporter<GameCustomField, Data.Models.GameCustomField>
    where TParentRecord : Data.Models.BaseModel
{
    public async Task<ImportItemInfo> InfoAsync(GameCustomField record)
    {
        return new ImportItemInfo
        {
            Name = record.Name,
        };
    }

    public bool CanImport(GameCustomField record) => importContext.Record is Data.Models.Game;
    
    public async Task<Data.Models.GameCustomField> AddAsync(GameCustomField record)
    {
        try
        {
            var customField = await gameService.SetCustomFieldAsync(importContext.Record.Id, record.Name, record.Value);

            return customField;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<GameCustomField>(record, "An unknown error occured while importing customField", ex);
        }
    }

    public async Task<Data.Models.GameCustomField> UpdateAsync(GameCustomField record)
    {
        var existing = await gameService.GetCustomFieldAsync(importContext.Record.Id, record.Name);

        try
        {
            if (existing.Value != record.Value)
                existing = await gameService.SetCustomFieldAsync(importContext.Record.Id, record.Name, record.Value);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<GameCustomField>(record, "An unknown error occured while importing customField", ex);
        }
    }

    public async Task<bool> ExistsAsync(GameCustomField record)
    {
        return (await gameService.GetCustomFieldAsync(importContext.Record.Id, record.Name)) == null;
    }
}