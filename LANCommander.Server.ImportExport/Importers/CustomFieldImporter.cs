using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class CustomFieldImporter(
    IMapper mapper,
    GameCustomFieldService gameCustomFieldService,
    GameService gameService) : BaseImporter<GameCustomField, Data.Models.GameCustomField>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(GameCustomField record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.CustomFields,
            Name = record.Name,
        };
    }
    
    public override bool CanImport(GameCustomField record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.GameCustomField> AddAsync(GameCustomField record)
    {
        try
        {
            var customField = await gameService.SetCustomFieldAsync(ImportContext.DataRecord.Id, record.Name, record.Value);

            return customField;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<GameCustomField>(record, "An unknown error occured while importing customField", ex);
        }
    }

    public override async Task<Data.Models.GameCustomField> UpdateAsync(GameCustomField record)
    {
        var existing = await gameService.GetCustomFieldAsync(ImportContext.DataRecord.Id, record.Name);

        try
        {
            if (existing.Value != record.Value)
                existing = await gameService.SetCustomFieldAsync(ImportContext.DataRecord.Id, record.Name, record.Value);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<GameCustomField>(record, "An unknown error occured while importing customField", ex);
        }
    }

    public override async Task<bool> ExistsAsync(GameCustomField record)
    {
        return (await gameService.GetCustomFieldAsync(ImportContext.DataRecord.Id, record.Name)) == null;
    }
}