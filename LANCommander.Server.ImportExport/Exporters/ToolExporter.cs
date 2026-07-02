using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Action = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Server.ImportExport.Exporters;

public class ToolExporter(ManifestMapper manifestMapper, ToolService toolService) : BaseExporter<Tool, Data.Models.Tool>
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
        var tool = await toolService.GetAsync(id, manifestMapper.ProjectToManifestTool);

        if (tool.Archives is null)
            tool.Archives = new List<Archive>();

        if (tool.Scripts is null)
            tool.Scripts = new List<Script>();

        if (tool.Actions is null)
            tool.Actions = new List<Action>();

        return tool;
    }
} 