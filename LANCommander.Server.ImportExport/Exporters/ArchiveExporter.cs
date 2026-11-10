using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;

namespace LANCommander.Server.ImportExport.Exporters;

/// <summary>
/// Implements exporting for archive records
/// </summary>
/// <param name="manifestMapper">Mapper for converting between data and manifest models</param>
/// <param name="archiveService">Service for archive operations</param>
public class ArchiveExporter(
    ManifestMapper manifestMapper,
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
    
    public override bool CanExport(Archive record) =>
        ExportContext.DataRecord is Data.Models.Game
        || ExportContext.DataRecord is Data.Models.Redistributable
        || ExportContext.DataRecord is Data.Models.Tool;

    public override async Task<Archive> ExportAsync(Guid id)
    {
        var entity = await archiveService.Include(a => a.GameVersion!).GetAsync(id);

        try
        {
            var path = await archiveService.GetArchiveFileLocationAsync(entity);
            var fileInfo = new FileInfo(path);

            if (fileInfo.Exists)
                await WriteEntryFromFileAsync($"Archives/{entity.Id}", path);

            var archive = manifestMapper.ToManifest(entity);

            // Changelogs moved to the game version the archive belongs to; the manifest still carries
            // it on the archive, which is what the importer reads it from
            archive.Changelog ??= entity.GameVersion?.Changelog;

            return archive;
        }
        catch (Exception ex)
        {
            throw new ExportSkippedException<Data.Models.Archive>(entity, "Could not add archive to export file", ex);
        }
    }
} 