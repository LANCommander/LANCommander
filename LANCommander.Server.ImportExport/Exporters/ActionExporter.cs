using LANCommander.SDK.Enums;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Action = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Server.ImportExport.Exporters;

public class ActionExporter(
    ActionService actionService) : BaseExporter<Action, Data.Models.Action>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Action record) =>
        await Task.Run(() => new ExportItemInfo { Id = record.Id, Name = record.Name, Type = ImportExportRecordType.Action });
    
    public override bool CanExport(Action record) => ExportContext.DataRecord is Data.Models.Game;
    
    public override async Task<Action> ExportAsync(Guid id)
    {
        return await actionService.GetAsync<Action>(id);
    }
}