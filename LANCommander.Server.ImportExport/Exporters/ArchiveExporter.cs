using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

/// <summary>
/// Implements exporting for archive records
/// </summary>
/// <param name="mapper">AutoMapper instance for mapping between models</param>
/// <param name="archiveService">Service for archive operations</param>
public class ArchiveExporter(
    IMapper mapper,
    ArchiveService archiveService) : BaseExporter<Archive, Data.Models.Archive>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Archive record)
    {
        var archivePath = await archiveService.GetArchiveFileLocationAsync(record.ObjectKey);

        var info = new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Archive,
            Name = record.Version,
        };

        if (File.Exists(archivePath))
        {
            var fileInfo = new FileInfo(archivePath);
        
            if (fileInfo.Exists)
                info.Size = fileInfo.Length;
        }

        return info;
    }
    
    public override bool CanExport(Archive record) => ExportContext.DataRecord is Data.Models.Game || ExportContext.DataRecord is Data.Models.Redistributable;

    public override async Task<Archive> ExportAsync(Guid id)
    {
        var entity = await archiveService.GetAsync(id);
        
        try
        {
            var path = await archiveService.GetArchiveFileLocationAsync(entity);
            var fileInfo = new FileInfo(path);

            if (fileInfo.Exists)
            {
                var archiveEntry = ExportContext.Archive.CreateEntry($"Archives/{entity.Id}");
            
                using (var archiveEntryStream = archiveEntry.Open())
                using (var archiveFileStream = new FileStream(path, FileMode.Open))
                {
                    await archiveFileStream.CopyToAsync(archiveEntryStream);
                }
            }
        
            return mapper.Map<Archive>(entity);
        }
        catch (Exception ex)
        {
            throw new ExportSkippedException<Data.Models.Archive>(entity, "Could not add archive to export file", ex);
        }
    }
} 