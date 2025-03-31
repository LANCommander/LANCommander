using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class ServerHttpPathImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<ServerHttpPath, Data.Models.ServerHttpPath>
{
    ServerHttpPathService _serverHttpPathService = serviceProvider.GetRequiredService<ServerHttpPathService>();
    
    public async Task<Data.Models.ServerHttpPath> AddAsync(ServerHttpPath record)
    {
        if (importContext.Record is not Data.Models.Server server)
            throw new ImportSkippedException<ServerHttpPath>(record, $"Cannot import server console for a {typeof(TParentRecord).Name}");

        try
        {
            var serverHttpPath = new Data.Models.ServerHttpPath
            {
                LocalPath = record.LocalPath,
                Path = record.Path,
                Server = server,
            };

            serverHttpPath = await _serverHttpPathService.AddAsync(serverHttpPath);

            return serverHttpPath;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerHttpPath>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public async Task<Data.Models.ServerHttpPath> UpdateAsync(ServerHttpPath record)
    {
        if (importContext.Record is not Data.Models.Server server)
            throw new ImportSkippedException<ServerHttpPath>(record, $"Cannot import server consoles for a {typeof(TParentRecord).Name}");

        var existing = await _serverHttpPathService.FirstOrDefaultAsync(p => p.Path == record.Path);

        try
        {
            existing.LocalPath = record.LocalPath;
            existing.Path = record.Path;
            existing.Server = server;
            
            existing = await _serverHttpPathService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerHttpPath>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public async Task<bool> ExistsAsync(ServerHttpPath record)
    {
        if (importContext.Record is not Data.Models.Server server)
            throw new ImportSkippedException<ServerHttpPath>(record, $"Cannot import serverHttpPaths for a {typeof(TParentRecord).Name}");
        
        return await _serverHttpPathService.ExistsAsync(p => p.Path == record.Path);
    }
}