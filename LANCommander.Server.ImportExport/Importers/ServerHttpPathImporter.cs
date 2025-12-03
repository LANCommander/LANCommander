using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class ServerHttpPathImporter(
    ILogger<ServerHttpPathImporter> logger,
    ServerHttpPathService serverHttpPathService,
    ServerService serverService,
    ServerImporter serverImporter) : BaseImporter<ServerHttpPath>
{
    public override string GetKey(ServerHttpPath record)
        => $"{nameof(ServerHttpPath)}/{record.LocalPath}";

    public override async Task<ImportItemInfo<ServerHttpPath>> GetImportInfoAsync(ServerHttpPath record) 
        => new()
        {
            Type = ImportExportRecordType.ServerHttpPath,
            Name = record.Path,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(ServerHttpPath record) => ImportContext.Manifest is SDK.Models.Manifest.Server;

    public override async Task<bool> AddAsync(ServerHttpPath record)
    {
        try
        {
            var server = ImportContext.Manifest as SDK.Models.Manifest.Server;

            if (server == null)
                return false;

            if (ImportContext.InQueue(server, serverImporter))
                return false;
            
            var serverHttpPath = new Data.Models.ServerHttpPath
            {
                LocalPath = record.LocalPath,
                Path = record.Path,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
                Server = await serverService.GetAsync(server.Id),
            };

            await serverHttpPathService.AddAsync(serverHttpPath);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add server HTTP path | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ServerHttpPath record)
    {
        var existing = await serverHttpPathService.FirstOrDefaultAsync(p => p.Path == record.Path);

        try
        {
            var server = ImportContext.Manifest as SDK.Models.Manifest.Server;

            if (server == null)
                return false;
            
            existing.LocalPath = record.LocalPath;
            existing.Path = record.Path;
            existing.Server = await serverService.GetAsync(server.Id);
            
            await serverHttpPathService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update server HTTP path | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(ServerHttpPath record)
    {
        if (ImportContext.Manifest is SDK.Models.Manifest.Server server)
            return await serverHttpPathService
                .Include(p => p.Server)
                .ExistsAsync(p => p.Path == record.Path && p.ServerId == server.Id);

        return false;
    }
}