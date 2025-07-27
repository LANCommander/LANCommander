using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class MediaExporter(
    MediaService mediaService) : BaseExporter<Media, Data.Models.Media>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Media record)
    {
        var mediaPath = await mediaService.GetMediaPathAsync(record.Id);
        
        var info = new ExportItemInfo
        {
            Id = record.Id,
            Flag = ExportRecordFlags.Media,
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
            {
                var mediaEntry = ExportContext.Archive.CreateEntry($"Media/{entity.Id}");

                using (var mediaEntryStream = mediaEntry.Open())
                using (var mediaFileStream = new FileStream(path, FileMode.Open))
                {
                    await mediaFileStream.CopyToAsync(mediaEntryStream);
                }
            }
        
            return await mediaService.GetAsync<Media>(id);
        }
        catch (Exception ex)
        {
            throw new ExportSkippedException<Data.Models.Media>(entity, "Could not add media to export file", ex);
        }
    }
} 