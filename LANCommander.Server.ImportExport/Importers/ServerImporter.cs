using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LANCommander.Server.ImportExport.Importers;

public class ServerImporter(
    ILogger<ServerImporter> logger,
    IMapper mapper,
    ServerService serverService,
    GameService gameService,
    UserService userService) : BaseImporter<SDK.Models.Manifest.Server>
{
    public override string GetKey(SDK.Models.Manifest.Server record)
        => $"{nameof(SDK.Models.Manifest.Server)}/{record.Id}";

    public override async Task<ImportItemInfo<SDK.Models.Manifest.Server>> GetImportInfoAsync(SDK.Models.Manifest.Server record)
    {
        var fileEntries = ImportContext.Archive.Entries.Where(e => e.Key.StartsWith("Files/"));
        
        return new ImportItemInfo<SDK.Models.Manifest.Server>
        {
            Type = ImportExportRecordType.Server,
            Name = record.Name,
            Size = fileEntries.Sum(f => f.Size),
            Record = record,
        };
    }

    public override async Task<bool> CanImportAsync(SDK.Models.Manifest.Server record) => true;

    public override async Task<bool> AddAsync(SDK.Models.Manifest.Server record)
    {
        var server = mapper.Map<Data.Models.Server>(record);

        try
        {
            await serverService.AddAsync(server);
            
            AddAsset(new ImportAssetArchiveEntry
            {
                RecordId = record.Id,
                Name = "Files",
                Path = "Files/",
            });

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add server");
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(SDK.Models.Manifest.Server record)
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
            
            await serverService.UpdateAsync(existing);

            AddAsset(new ImportAssetArchiveEntry
            {
                RecordId = record.Id,
                Name = "Files",
                Path = "Files/",
            });

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update server");
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        if (asset is ImportAssetArchiveEntry assetArchiveEntry &&
            assetArchiveEntry.Path.EndsWith("/"))
        {
            var server = await serverService.GetAsync(assetArchiveEntry.RecordId);
            
            foreach (var entry in ImportContext.Archive.Entries.Where(e =>
                         e.Key.StartsWith(assetArchiveEntry.Path)))
            {
                try
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
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not import server file {ArchivePath}", entry.Key);
                }
            }

            return true;
        }

        return false;
    }

    public override async Task<bool> ExistsAsync(SDK.Models.Manifest.Server record)
    {
        return await serverService.ExistsAsync(s => s.Id == record.Id || s.Name == record.Name);
    }
} 