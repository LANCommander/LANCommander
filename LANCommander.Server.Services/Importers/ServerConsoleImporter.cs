using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class ServerConsoleImporter(
    IMapper mapper,
    ServerConsoleService serverConsoleService,
    ImportContext importContext,
    ExportContext exportContext) : IImporter<ServerConsole, Data.Models.ServerConsole>
{
    public async Task<ImportItemInfo> InfoAsync(ServerConsole record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.ServerConsoles,
            Name = record.Name,
        };
    }

    public bool CanImport(ServerConsole record) => importContext.DataRecord is Data.Models.ServerConsole;
    public bool CanExport(ServerConsole record) => exportContext.DataRecord is Data.Models.ServerConsole;

    public async Task<Data.Models.ServerConsole> AddAsync(ServerConsole record)
    {
        try
        {
            var serverConsole = new Data.Models.ServerConsole
            {
                Name = record.Name,
                Type = record.Type,
                Path = record.Path,
                Host = record.Host,
                Port = record.Port,
                Server = importContext.DataRecord as Data.Models.Server,
            };

            serverConsole = await serverConsoleService.AddAsync(serverConsole);

            return serverConsole;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerConsole>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public async Task<Data.Models.ServerConsole> UpdateAsync(ServerConsole record)
    {
        var existing = await serverConsoleService
            .Include(c => c.Server)
            .FirstOrDefaultAsync(c => c.Name == record.Name && c.Server.Name == (importContext.DataRecord as Data.Models.Server).Name);

        try
        {
            existing.Name = record.Name;
            existing.Type = record.Type;
            existing.Path = record.Path;
            existing.Host = record.Host;
            existing.Port = record.Port;
            
            existing = await serverConsoleService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerConsole>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public async Task<ServerConsole> ExportAsync(Data.Models.ServerConsole entity)
    {
        return mapper.Map<ServerConsole>(entity);
    }

    public async Task<bool> ExistsAsync(ServerConsole record)
    {
        return await serverConsoleService
            .Include(c => c.Server)
            .ExistsAsync(c => c.Name == record.Name && c.Server.Name == (importContext.DataRecord as Data.Models.Server).Name);
    }
}