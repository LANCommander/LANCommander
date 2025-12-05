using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class ServerConsoleImporter(
    ILogger<ServerConsoleImporter> logger,
    ServerConsoleService serverConsoleService,
    ServerService serverService,
    ServerImporter serverImporter) : BaseImporter<ServerConsole>
{
    public override string GetKey(ServerConsole record)
        => $"{nameof(ServerConsole)}/{record.Name}";

    public override async Task<ImportItemInfo<ServerConsole>> GetImportInfoAsync(ServerConsole record)
        => new()
        {
            Type = ImportExportRecordType.ServerConsole,
            Name = record.Name,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(ServerConsole record) => ImportContext.Manifest is SDK.Models.Manifest.Server;

    public override async Task<bool> AddAsync(ServerConsole record)
    {
        try
        {
            var server = ImportContext.Manifest as SDK.Models.Manifest.Server;

            if (server == null)
                return false;

            if (ImportContext.InQueue(server, serverImporter))
                return false;
            
            var serverConsole = new Data.Models.ServerConsole
            {
                Name = record.Name,
                Type = record.Type,
                Path = record.Path,
                Host = record.Host,
                Port = record.Port,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
                Server = await serverService.GetAsync(server.Id),
            };

            await serverConsoleService.AddAsync(serverConsole);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add server console | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ServerConsole record)
    {
        try
        {
            var server = ImportContext.Manifest as SDK.Models.Manifest.Server;

            if (server == null)
                return false;
            
            var existing = await serverConsoleService.FirstOrDefaultAsync(c => c.Name == record.Name && c.ServerId == server.Id);
            
            existing.Name = record.Name;
            existing.Type = record.Type;
            existing.Path = record.Path;
            existing.Host = record.Host;
            existing.Port = record.Port;
            
            await serverConsoleService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update server console | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> ExistsAsync(ServerConsole record)
    {
        if (ImportContext.Manifest is SDK.Models.Manifest.Server server)
            return await serverConsoleService
                .Include(c => c.Server)
                .ExistsAsync(c => c.Name == record.Name && c.ServerId == server.Id);

        return false;
    }
}