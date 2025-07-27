using AutoMapper;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class RedistributableExporter(RedistributableService redistributableService) : BaseExporter<Redistributable, Data.Models.Redistributable>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Redistributable record)
    {
        return new ExportItemInfo()
        {
            Id = record.Id,
            Name = record.Name,
        };
    }

    public override bool CanExport(Redistributable record) => true;

    public override async Task<Redistributable> ExportAsync(Guid id)
    {
        return await redistributableService.GetAsync<Redistributable>(id);
    }
} 