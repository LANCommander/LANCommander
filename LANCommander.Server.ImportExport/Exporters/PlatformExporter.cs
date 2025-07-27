using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class PlatformExporter(PlatformService platformService) : BaseExporter<Platform, Data.Models.Platform>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Platform record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ExportRecordFlags.Platforms,
            Name = record.Name,
        };
    }

    public override bool CanExport(Platform record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Platform> ExportAsync(Guid id)
    {
        return await platformService.GetAsync<Platform>(id);
    }
} 