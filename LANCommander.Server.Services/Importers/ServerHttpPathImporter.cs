using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class ServerHttpPathImporter(
    ServerHttpPathService serverHttpPathService,
    ServerService serverService,
    ImportContext importContext) : IImporter<ServerHttpPath, Data.Models.ServerHttpPath>
{
    public async Task<ImportItemInfo> InfoAsync(ServerHttpPath record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.ServerHttpPaths,
            Name = record.Path,
        };
    }

    public bool CanImport(ServerHttpPath record) => importContext.DataRecord is Data.Models.Server;

    public async Task<Data.Models.ServerHttpPath> AddAsync(ServerHttpPath record)
    {
        try
        {
            var serverHttpPath = new Data.Models.ServerHttpPath
            {
                LocalPath = record.LocalPath,
                Path = record.Path,
                Server = await serverService.FirstOrDefaultAsync(s => s.Name == (importContext.DataRecord as Data.Models.Server).Name),
            };

            serverHttpPath = await serverHttpPathService.AddAsync(serverHttpPath);

            return serverHttpPath;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerHttpPath>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public async Task<Data.Models.ServerHttpPath> UpdateAsync(ServerHttpPath record)
    {
        var existing = await serverHttpPathService.FirstOrDefaultAsync(p => p.Path == record.Path);

        try
        {
            existing.LocalPath = record.LocalPath;
            existing.Path = record.Path;
            existing.Server =
                await serverService.FirstOrDefaultAsync(
                    s => s.Name == (importContext.DataRecord as Data.Models.Server).Name);
            
            existing = await serverHttpPathService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerHttpPath>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public async Task<bool> ExistsAsync(ServerHttpPath record)
    {
        return await serverHttpPathService
            .Include(p => p.Server)
            .ExistsAsync(p => p.Path == record.Path && p.Server.Name == (importContext.DataRecord as Data.Models.Server).Name);
    }
}