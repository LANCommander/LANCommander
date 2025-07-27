using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class ServerConsoleExporter(ServerConsoleService serverConsoleService) : BaseExporter<ServerConsole, Data.Models.ServerConsole>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.ServerConsole record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ExportRecordFlags.ServerConsoles,
            Name = record.Name,
        };
    }

    public override bool CanExport(ServerConsole record) => ExportContext.DataRecord is Data.Models.ServerConsole;

    public override async Task<ServerConsole> ExportAsync(Guid id)
    {
        return await serverConsoleService.GetAsync<ServerConsole>(id);
    }
} 