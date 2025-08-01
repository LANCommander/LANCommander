using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class ServerHttpPathImporter(
    IMapper mapper,
    ServerHttpPathService serverHttpPathService,
    ServerService serverService) : BaseImporter<ServerHttpPath, Data.Models.ServerHttpPath>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(ServerHttpPath record)
    {
        return new ImportItemInfo
        {
            Type = ImportExportRecordType.ServerHttpPath,
            Name = record.Path,
        };
    }

    public override bool CanImport(ServerHttpPath record) => ImportContext.DataRecord is Data.Models.Server;

    public override async Task<Data.Models.ServerHttpPath> AddAsync(ServerHttpPath record)
    {
        try
        {
            var serverHttpPath = new Data.Models.ServerHttpPath
            {
                LocalPath = record.LocalPath,
                Path = record.Path,
                Server = await serverService.FirstOrDefaultAsync(s => s.Name == (ImportContext.DataRecord as Data.Models.Server).Name),
            };

            serverHttpPath = await serverHttpPathService.AddAsync(serverHttpPath);

            return serverHttpPath;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerHttpPath>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public override async Task<Data.Models.ServerHttpPath> UpdateAsync(ServerHttpPath record)
    {
        var existing = await serverHttpPathService.FirstOrDefaultAsync(p => p.Path == record.Path);

        try
        {
            existing.LocalPath = record.LocalPath;
            existing.Path = record.Path;
            existing.Server =
                await serverService.FirstOrDefaultAsync(
                    s => s.Name == (ImportContext.DataRecord as Data.Models.Server).Name);
            
            existing = await serverHttpPathService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerHttpPath>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public override async Task<bool> ExistsAsync(ServerHttpPath record)
    {
        return await serverHttpPathService
            .Include(p => p.Server)
            .ExistsAsync(p => p.Path == record.Path && p.Server.Name == (ImportContext.DataRecord as Data.Models.Server).Name);
    }
}