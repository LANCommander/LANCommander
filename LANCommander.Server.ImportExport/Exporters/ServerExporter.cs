using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;
using Action = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Server.ImportExport.Exporters;

public class ServerExporter(
    ILogger<ServerExporter> logger,
    ServerService serverService) : BaseExporter<SDK.Models.Manifest.Server, Data.Models.Server>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Server record)
    {
        var files = Directory.GetFiles(record.WorkingDirectory, "*", SearchOption.AllDirectories);

        long size = 0;
        
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            
            size += fileInfo.Length;
        }
        
        return new ExportItemInfo
        {
            Id = record.Id,
            Name = record.Name,
            Size = size
        };
    }

    public override bool CanExport(SDK.Models.Manifest.Server record) => true;

    public override async Task<SDK.Models.Manifest.Server> ExportAsync(Guid id)
    {
        var entity = await serverService.GetManifestAsync(id);
        
        var files = Directory.GetFiles(entity.WorkingDirectory, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);

                if (fileInfo.Exists)
                    await WriteEntryFromFileAsync(
                        $"Files/{fileInfo.Name.Replace(Path.DirectorySeparatorChar, '/')}",
                        fileInfo.FullName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not add {File} to the server export file, it will be missing from the export", file);
            }
        }

        // The queue fills these from the records the user actually selected, so anything the
        // manifest already carries has to be cleared first or every child is written out twice.
        entity.Scripts = new List<Script>();
        entity.Actions = new List<Action>();
        entity.HttpPaths = new List<ServerHttpPath>();
        entity.ServerConsoles = new List<ServerConsole>();
        
        return entity;
    }
} 