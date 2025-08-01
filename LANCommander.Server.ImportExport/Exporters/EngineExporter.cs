using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class EngineExporter(
    EngineService engineService) : BaseExporter<Engine, Data.Models.Engine>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Engine record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Engine,
            Name = record.Name,
        };
    }

    public override bool CanExport(Engine record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Engine> ExportAsync(Guid id)
    {
        return await engineService.GetAsync<Engine>(id);
    }
} 