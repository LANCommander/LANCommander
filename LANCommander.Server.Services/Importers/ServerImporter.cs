using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LANCommander.Server.Services.Importers;

public class ServerImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<SDK.Models.Manifest.Server, Data.Models.Server>
{
    private readonly ServerService _serverService = serviceProvider.GetService<ServerService>();
    private readonly GameService _gameService = serviceProvider.GetService<GameService>();
    private readonly UserService _userService = serviceProvider.GetService<UserService>();
    private readonly IMapper _mapper = serviceProvider.GetService<IMapper>();
    
    public async Task<Data.Models.Server> AddAsync(SDK.Models.Manifest.Server record)
    {
        var server = _mapper.Map<Data.Models.Server>(record);

        try
        {
            await ExtractFiles(server);
            
            return await _serverService.AddAsync(server);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<SDK.Models.Manifest.Server>(record, "An unknown error occured while trying to add server", ex);
        }
    }

    public async Task<Data.Models.Server> UpdateAsync(SDK.Models.Manifest.Server record)
    {
        var existing = await _serverService.FirstOrDefaultAsync(s => s.Id == record.Id || s.Name == record.Name);

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
            existing.Game = await _gameService.FirstOrDefaultAsync(g =>
                g.Id.ToString() == record.Game || g.Title == record.Game);
            existing.CreatedBy = await _userService.GetAsync(record.CreatedBy);
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedBy = await _userService.GetAsync(record.UpdatedBy);
            existing.UpdatedOn = DateTime.Now;
            existing.ProcessTerminationMethod = record.ProcessTerminationMethod;
            
            existing = await _serverService.UpdateAsync(existing);

            await ExtractFiles(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<SDK.Models.Manifest.Server>(record, "An unknown error occurred while trying to update server", ex);
        }
    }

    public async Task<bool> ExistsAsync(SDK.Models.Manifest.Server record)
    {
        return await _serverService.ExistsAsync(s => s.Id == record.Id || s.Name == record.Name);
    }
    
    private async Task ExtractFiles(Data.Models.Server server)
    {
        
        foreach (var entry in importContext.Archive.Entries.Where(e => e.Key.StartsWith("Files/")))
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