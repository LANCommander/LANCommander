using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class MediaImporter(
    IMapper mapper,
    StorageLocationService storageLocationService,
    MediaService mediaService) : BaseImporter<Media, Data.Models.Media>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Media record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Media,
            Name = String.IsNullOrWhiteSpace(record.Name) ? record.Type.ToString() : $"{record.Type} - {record.Name}",
            Size = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}")?.Size ?? 0,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Media record)
    {
        var mediaPath = await mediaService.GetMediaPathAsync(record.Id);
        
        var info = new ExportItemInfo
        {
            Id = record.Id,
            Flag = ImportRecordFlags.Media,
            Name = String.IsNullOrWhiteSpace(record.Name) ? record.Type.ToString() : $"{record.Type} - {record.Name}",
        };
        
        var fileInfo = new FileInfo(mediaPath);
        
        if (fileInfo.Exists)
            info.Size = fileInfo.Length;

        return info;
    }

    public override bool CanImport(Media record) => ImportContext.DataRecord is Data.Models.Game;
    public override bool CanExport(Media record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.Media> AddAsync(Media record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}");
        
        var defaultMediaLocation =
            await storageLocationService.FirstOrDefaultAsync(l => l.Type == StorageLocationType.Media && l.Default);

        Data.Models.Media media = null;
        
        try
        {
            media = new Data.Models.Media
            {
                Game = ImportContext.DataRecord as Data.Models.Game,
                CreatedOn = record.CreatedOn,
                Type = record.Type,
                UpdatedOn = record.UpdatedOn,
                StorageLocation = defaultMediaLocation,
            };

            media = await mediaService.AddAsync(media);
            media = await mediaService.WriteToFileAsync(media, archiveEntry.OpenEntryStream());

            return media;
        }
        catch (Exception ex)
        {
            if (media?.Id != Guid.Empty)
                await mediaService.DeleteAsync(media);

            throw new ImportSkippedException<Media>(record, "An unknown error occured while trying to import media file",
                ex);
        }
    }

    public override async Task<Data.Models.Media> UpdateAsync(Media record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Media/{record.Id}");
        var existing = await mediaService.Include(m => m.StorageLocation).FirstOrDefaultAsync(m => m.Type == record.Type && m.Game.Id == record.Id);
        var existingPath = MediaService.GetMediaPath(existing);
        
        if (archiveEntry == null)
            throw new ImportSkippedException<Media>(record, "Matching media file does not exist in import archive");

        try
        {
            existing.Game = ImportContext.DataRecord as Data.Models.Game;
            existing.Name = record.Name;
            existing.MimeType = record.MimeType;
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedOn = record.UpdatedOn;

            existing = await mediaService.UpdateAsync(existing);
            existing = await mediaService.WriteToFileAsync(existing, archiveEntry.OpenEntryStream());

            if (File.Exists(existingPath))
                File.Delete(existingPath);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Media>(record, "An unknown error occured while importing media file", ex);
        }
    }

    public override async Task<Media> ExportAsync(Guid id)
    {
        var path = await mediaService.GetMediaPathAsync(id);
        var fileInfo = new FileInfo(path);

        using (var fs = fileInfo.OpenRead())
        {
            ImportContext.Archive.AddEntry($"Media/{id}", fs);
        }
        
        return await mediaService.GetAsync<Media>(id);
    }

    public override Task<bool> ExistsAsync(Media media)
    {
        return mediaService.ExistsAsync(m => m.Type == media.Type && m.Id == media.Id);
    }
}