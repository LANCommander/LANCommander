using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class CustomFieldImporter(
    IMapper mapper,
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

    public override async Task<ExportItemInfo> GetExportInfoAsync(GameCustomField record)
    {
        return new ExportItemInfo
        {
            Flag = ImportRecordFlags.CustomFields,
            Name = record.Name,
        };
    }

    public override bool CanImport(GameCustomField record) => ImportContext.DataRecord is Data.Models.Game;
    public override bool CanExport(GameCustomField record) => ImportContext.DataRecord is Data.Models.Game;

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

    public override async Task<GameCustomField> ExportAsync(Data.Models.GameCustomField entity)
    {
        return mapper.Map<GameCustomField>(entity);
    }

    public override async Task<bool> ExistsAsync(GameCustomField record)
    {
        return (await gameService.GetCustomFieldAsync(ImportContext.DataRecord.Id, record.Name)) == null;
    }
}