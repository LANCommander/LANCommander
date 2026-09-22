using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;

namespace LANCommander.Server.ImportExport.Exporters;

public class MediaExporter(
    ManifestMapper manifestMapper,
    MediaService mediaService) : BaseExporter<Media, Data.Models.Media>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Media record)
    {
        var mediaPath = await mediaService.GetMediaPathAsync(record.Id);
        
        var info = new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Media,
            Name = String.IsNullOrWhiteSpace(record.Name) ? record.Type.ToString() : $"{record.Type} - {record.Name}",
        };

        if (File.Exists(mediaPath))
        {
            var fileInfo = new FileInfo(mediaPath);
        
            if (fileInfo.Exists)
                info.Size = fileInfo.Length;
        }

        return info;
    }

    public override bool CanExport(Media record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Media> ExportAsync(Guid id)
    {
        var entity = await mediaService.GetAsync(id);

        try
        {
            var path = await mediaService.GetMediaPathAsync(id);
            
            var fileInfo = new FileInfo(path);

            if (fileInfo.Exists)
                await WriteEntryFromFileAsync($"Media/{entity.Id}", path);
        
            return await mediaService.GetAsync(id, manifestMapper.ProjectToManifestMedia);
        }
        catch (Exception ex)
        {
            throw new ExportSkippedException<Data.Models.Media>(entity, "Could not add media to export file", ex);
        }
    }
} 