using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class ServerConsoleImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<ServerConsole, Data.Models.ServerConsole>
{
    ServerConsoleService _serverConsoleService = serviceProvider.GetRequiredService<ServerConsoleService>();
    
    public async Task<Data.Models.ServerConsole> AddAsync(ServerConsole record)
    {
        if (importContext.Record is not Data.Models.Server server)
            throw new ImportSkippedException<ServerConsole>(record, $"Cannot import server console for a {typeof(TParentRecord).Name}");

        try
        {
            var serverConsole = new Data.Models.ServerConsole
            {
                Name = record.Name,
                Type = record.Type,
                Path = record.Path,
                Host = record.Host,
                Port = record.Port,
                Server = server,
            };

            serverConsole = await _serverConsoleService.AddAsync(serverConsole);

            return serverConsole;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerConsole>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public async Task<Data.Models.ServerConsole> UpdateAsync(ServerConsole record)
    {
        if (importContext.Record is not Data.Models.Server server)
            throw new ImportSkippedException<ServerConsole>(record, $"Cannot import server consoles for a {typeof(TParentRecord).Name}");

        var existing = await _serverConsoleService.FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            existing.Name = record.Name;
            existing.Type = record.Type;
            existing.Path = record.Path;
            existing.Host = record.Host;
            existing.Port = record.Port;
            
            existing = await _serverConsoleService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerConsole>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public async Task<bool> ExistsAsync(ServerConsole record)
    {
        if (importContext.Record is not Data.Models.Server server)
            throw new ImportSkippedException<ServerConsole>(record, $"Cannot import serverConsoles for a {typeof(TParentRecord).Name}");
        
        return await _serverConsoleService.ExistsAsync(c => c.Name == record.Name);
    }
}