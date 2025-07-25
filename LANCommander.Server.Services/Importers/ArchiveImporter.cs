using System.Web.Services.Description;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services.Importers;

/// <summary>
/// Implements importing for archive records
/// </summary>
/// <param name="serviceProvider">Valid service provider for injecting the services we need</param>
/// <param name="ImportContext">The context (archive, parent record> of the import</param>
public class ArchiveImporter(
    IMapper mapper,
    ArchiveService archiveService) : BaseImporter<Archive, Data.Models.Archive>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Archive record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Archives,
            Name = record.Version,
            Size = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Archives/{record.Id}")?.Size ?? 0,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Archive record)
    {
        var archivePath = await archiveService.GetArchiveFileLocationAsync(record.ObjectKey);

        var info = new ExportItemInfo
        {
            Id = record.Id,
            Flag = ImportRecordFlags.Archives,
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

    public override bool CanImport(Archive record) => ImportContext.DataRecord is Data.Models.Game || ImportContext.DataRecord is Data.Models.Redistributable;
    public override bool CanExport(Archive record) => ImportContext.DataRecord is Data.Models.Game || ImportContext.DataRecord is Data.Models.Redistributable;

    public override async Task<Data.Models.Archive> AddAsync(Archive record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Archives/{record.Id}");

        if (archiveEntry == null)
            throw new ImportSkippedException<Archive>(record, "Matching archive file does not exist in import archive");

        Data.Models.Archive archive = null;
        string path = "";

        try
        {
            var newArchive = new Data.Models.Archive()
            {
                CreatedOn = record.CreatedOn,
                StorageLocation = ImportContext.ArchiveStorageLocation,
                Version = record.Version,
                Changelog = record.Changelog,
            };

            if (ImportContext.DataRecord is Data.Models.Game game)
                newArchive.Game = game;
            else if (ImportContext.DataRecord is Data.Models.Redistributable redistributable)
                newArchive.Redistributable = redistributable;
            else
                throw new ImportSkippedException<Archive>(record,
                    $"Cannot import an archive for a {record.GetType().Name}");
            
            archive = await archiveService.AddAsync(newArchive);
            archive = await archiveService.WriteToFileAsync(archive, archiveEntry.OpenEntryStream());

            return archive;
        }
        catch (Exception ex)
        {
            if (archive != null)
                await archiveService.DeleteAsync(archive);
            
            throw new ImportSkippedException<Archive>(record, "An unknown error occured while importing archive file", ex);
        }
    }

    public override async Task<Data.Models.Archive> UpdateAsync(Archive archive)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Archives/{archive.Id}");
        var existing = await archiveService.Include(a => a.StorageLocation).FirstOrDefaultAsync(a => archive.Id == a.Id);
        var existingPath = await archiveService.GetArchiveFileLocationAsync(existing);
        
        if (archiveEntry == null)
            throw new ImportSkippedException<Archive>(archive, "Matching archive file does not exist in import archive");
        
        try
        {
            existing.Version = archive.Version;
            existing.Changelog = archive.Changelog;
            existing.StorageLocation = ImportContext.ArchiveStorageLocation;
            
            existing = await archiveService.UpdateAsync(existing);
            existing = await archiveService.WriteToFileAsync(existing, archiveEntry.OpenEntryStream());
            
            if (File.Exists(existingPath))
                File.Delete(existingPath);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Archive>(archive, "An unknown error occured while importing archive file", ex);
        }
    }

    public override async Task<Archive> ExportAsync(Guid id)
    {
        var entity = await archiveService.GetAsync(id);
        var path = await archiveService.GetArchiveFileLocationAsync(entity);
        var fileInfo = new FileInfo(path);

        if (fileInfo.Exists)
        {
            using (var fs = fileInfo.OpenRead())
            {
                ImportContext.Archive.AddEntry($"Archives/{id}", fs, fileInfo.Length, fileInfo.LastWriteTimeUtc);
            }
        }
        
        return mapper.Map<Archive>(entity);
    }

    public override async Task<bool> ExistsAsync(Archive archive)
    {
        if (ImportContext.DataRecord is Data.Models.Game game)
            return await archiveService.ExistsAsync(a => a.Version == archive.Version && a.GameId == game.Id);
        
        if (ImportContext.DataRecord is Data.Models.Redistributable redistributable)
            return await archiveService.ExistsAsync(a => a.Version == archive.Version && a.RedistributableId == redistributable.Id);
        
        throw new ImportSkippedException<Archive>(archive, $"Cannot import an archive for a {ImportContext.DataRecord.GetType().Name}");
    }
}