using AutoMapper;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LANCommander.Server.ImportExport.Importers;

public class ServerImporter(
    IMapper mapper,
    ServerService serverService,
    GameService gameService,
    UserService userService) : BaseImporter<SDK.Models.Manifest.Server, Data.Models.Server>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(SDK.Models.Manifest.Server record)
    {
        var fileEntries = ImportContext.Archive.Entries.Where(e => e.Key.StartsWith("Files/"));
        
        return new ImportItemInfo
        {
            Name = record.Name,
            Size = fileEntries.Sum(f => f.Size),
        };
    }

    public override bool CanImport(SDK.Models.Manifest.Server record) => true;

    public override async Task<Data.Models.Server> AddAsync(SDK.Models.Manifest.Server record)
    {
        var server = mapper.Map<Data.Models.Server>(record);

        try
        {
            await ExtractFiles(server);
            
            return await serverService.AddAsync(server);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<SDK.Models.Manifest.Server>(record, "An unknown error occured while trying to add server", ex);
        }
    }

    public override async Task<Data.Models.Server> UpdateAsync(SDK.Models.Manifest.Server record)
    {
        var existing = await serverService.FirstOrDefaultAsync(s => s.Id == record.Id || s.Name == record.Name);

        try
        {
            existing.Name = record.Name;
            existing.Path = record.Path;
            existing.WorkingDirectory = record.WorkingDirectory;
            existing.Arguments = record.Arguments;
            existing.Host = record.Host;
            existing.Port = record.Port;
            existing.Autostart = record.Autostart;
            existing.AutostartMethod = record.AutostartMethod;
            existing.AutostartDelay = record.AutostartDelay;
            existing.ContainerId = record.ContainerId;
            existing.UseShellExecute = record.UseShellExecute;
            existing.Game = await gameService.FirstOrDefaultAsync(g =>
                g.Id.ToString() == record.Game || g.Title == record.Game);
            existing.CreatedBy = await userService.GetAsync(record.CreatedBy);
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedBy = await userService.GetAsync(record.UpdatedBy);
            existing.UpdatedOn = DateTime.Now;
            existing.ProcessTerminationMethod = record.ProcessTerminationMethod;
            
            existing = await serverService.UpdateAsync(existing);

            await ExtractFiles(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<SDK.Models.Manifest.Server>(record, "An unknown error occurred while trying to update server", ex);
        }
    }

    public override async Task<bool> ExistsAsync(SDK.Models.Manifest.Server record)
    {
        return await serverService.ExistsAsync(s => s.Id == record.Id || s.Name == record.Name);
    }
    
    private async Task ExtractFiles(Data.Models.Server server)
    {
        
        foreach (var entry in ImportContext.Archive.Entries.Where(e => e.Key.StartsWith("Files/")))
        {
            var destination = entry.Key
                .Substring(6, entry.Key.Length - 6)
                .TrimEnd('/')
                .Replace('/', Path.DirectorySeparatorChar);

            destination = Path.Combine(server.WorkingDirectory, destination);
            
            var directory = Path.GetDirectoryName(destination);
            
            if (!String.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!entry.Key.EndsWith('/'))
                entry.WriteToFile(destination, new ExtractionOptions
                {
                    Overwrite = true,
                    PreserveAttributes = true,
                    PreserveFileTime = true,
                });
        }
    }
} 