using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class ServerConsoleImporter(
    IMapper mapper,
    ServerConsoleService serverConsoleService) : BaseImporter<ServerConsole, Data.Models.ServerConsole>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(ServerConsole record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.ServerConsoles,
            Name = record.Name,
        };
    }

    public override bool CanImport(ServerConsole record) => ImportContext.DataRecord is Data.Models.ServerConsole;

    public override async Task<Data.Models.ServerConsole> AddAsync(ServerConsole record)
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
                Server = ImportContext.DataRecord as Data.Models.Server,
            };

            serverConsole = await serverConsoleService.AddAsync(serverConsole);

            return serverConsole;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<ServerConsole>(record, "An unknown error occured while importing server console", ex);
        }
    }

    public override async Task<Data.Models.ServerConsole> UpdateAsync(ServerConsole record)
    {
        var existing = await serverConsoleService
            .Include(c => c.Server)
            .FirstOrDefaultAsync(c => c.Name == record.Name && c.Server.Name == (ImportContext.DataRecord as Data.Models.Server).Name);

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

    public override async Task<bool> ExistsAsync(ServerConsole record)
    {
        return await serverConsoleService
            .Include(c => c.Server)
            .ExistsAsync(c => c.Name == record.Name && c.Server.Name == (ImportContext.DataRecord as Data.Models.Server).Name);
    }
}