using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;

namespace LANCommander.Server.ImportExport.Exporters;

public class ServerConsoleExporter(ManifestMapper manifestMapper, ServerConsoleService serverConsoleService) : BaseExporter<ServerConsole, Data.Models.ServerConsole>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.ServerConsole record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.ServerConsole,
            Name = record.Name,
        };
    }

    public override bool CanExport(ServerConsole record) => ExportContext.DataRecord is Data.Models.ServerConsole;

    public override async Task<ServerConsole> ExportAsync(Guid id)
    {
        return await serverConsoleService.GetAsync(id, manifestMapper.ProjectToManifestServerConsole);
    }
} 