using LANCommander.SDK.Enums;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Action = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Server.ImportExport.Exporters;

public class ActionExporter(
    ManifestMapper manifestMapper,
    ActionService actionService) : BaseExporter<Action, Data.Models.Action>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Action record) =>
        await Task.Run(() => new ExportItemInfo { Id = record.Id, Name = record.Name, Type = ImportExportRecordType.Action });
    
    public override bool CanExport(Action record) => ExportContext.DataRecord is Data.Models.Game;
    
    public override async Task<Action> ExportAsync(Guid id)
    {
        return await actionService.GetAsync(id, manifestMapper.ProjectToManifestAction);
    }
}