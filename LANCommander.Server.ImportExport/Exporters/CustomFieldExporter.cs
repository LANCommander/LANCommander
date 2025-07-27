using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class CustomFieldExporter(
    GameCustomFieldService gameCustomFieldService) : BaseExporter<GameCustomField, Data.Models.GameCustomField>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.GameCustomField record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ExportRecordFlags.CustomFields,
            Name = record.Name,
        };
    }

    public override bool CanExport(GameCustomField record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<GameCustomField> ExportAsync(Guid id)
    {
        return await gameCustomFieldService.GetAsync<GameCustomField>(id);
    }
} 