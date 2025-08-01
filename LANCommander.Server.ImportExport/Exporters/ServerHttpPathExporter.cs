using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class ServerHttpPathExporter(ServerHttpPathService serverHttpPathService) : BaseExporter<ServerHttpPath, Data.Models.ServerHttpPath>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.ServerHttpPath record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.ServerHttpPath,
            Name = record.Path,
        };
    }

    public override bool CanExport(ServerHttpPath record) => ExportContext.DataRecord is Data.Models.Server;

    public override async Task<ServerHttpPath> ExportAsync(Guid id)
    {
        return await serverHttpPathService.GetAsync<ServerHttpPath>(id);
    }
} 