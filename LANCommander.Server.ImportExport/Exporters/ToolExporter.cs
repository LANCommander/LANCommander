using AutoMapper;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class ToolExporter(ToolService toolService) : BaseExporter<Tool, Data.Models.Tool>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Tool record)
    {
        return new ExportItemInfo()
        {
            Id = record.Id,
            Name = record.Name,
        };
    }

    public override bool CanExport(Tool record) => true;

    public override async Task<Tool> ExportAsync(Guid id)
    {
        return await toolService.GetAsync<Tool>(id);
    }
} 