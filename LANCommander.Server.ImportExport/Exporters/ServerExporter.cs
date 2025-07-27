using AutoMapper;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class ServerExporter(ServerService serverService) : BaseExporter<SDK.Models.Manifest.Server, Data.Models.Server>
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
        var entity = await serverService.GetAsync<SDK.Models.Manifest.Server>(id);
        
        var files = Directory.GetFiles(entity.WorkingDirectory, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);

                if (fileInfo.Exists)
                {
                    var fileEntry =
                        ExportContext.Archive.CreateEntry(
                            $"Files/{fileInfo.Name.Replace(Path.DirectorySeparatorChar, '/')}");

                    using (var fileEntryStream = fileEntry.Open())
                    using (var fileStream = new FileStream(fileInfo.FullName, FileMode.Open))
                    {
                        await fileStream.CopyToAsync(fileEntryStream);
                    }
                }
            }
            catch (Exception ex)
            {
                // File could not be added to archive
            }
        }
        
        return entity;
    }
} 