using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

/// <summary>
/// Implements importing for archive records
/// </summary>
/// <param name="serviceProvider">Valid service provider for injecting the services we need</param>
/// <param name="ImportContext">The context (archive, parent record> of the import</param>
public class ArchiveImporter(
    ILogger<ArchiveImporter> logger,
    ArchiveService archiveService,
    GameService gameService,
    RedistributableService redistributableService) : BaseImporter<Archive>
{
    public override string GetKey(Archive record)
        => $"{nameof(Archive)}/{record.Id}";

    public override async Task<ImportItemInfo<Archive>> GetImportInfoAsync(Archive record) 
        => new()
        {
            Type = ImportExportRecordType.Archive,
            Name = record.Version,
            Size = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Archives/{record.Id}")?.Size ?? 0,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Archive record) => ImportContext.Manifest is Game || ImportContext.Manifest is Redistributable;

    public override async Task<bool> AddAsync(Archive record)
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
                UpdatedOn = record.UpdatedOn,
                StorageLocation = ImportContext.ArchiveStorageLocation,
                Version = record.Version,
                Changelog = record.Changelog,
                ObjectKey = record.ObjectKey,
                CompressedSize = record.CompressedSize,
                UncompressedSize = record.UncompressedSize,
            };

            if (ImportContext.Manifest is Game game)
                newArchive.Game = await gameService.GetAsync(game.Id);
            else if (ImportContext.Manifest is Redistributable redistributable)
                newArchive.Redistributable = await redistributableService.GetAsync(redistributable.Id);
            else
                throw new ImportSkippedException<Archive>(record,
                    $"Cannot import an archive for a {record.GetType().Name}");
            
            archive = await archiveService.AddAsync(newArchive);
            archive = await archiveService.WriteToFileAsync(archive, archiveEntry.OpenEntryStream());

            return true;
        }
        catch (Exception ex)
        {
            if (archive != null)
                await archiveService.DeleteAsync(archive);
            
            logger.LogError(ex, "Could not add archive | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Archive archive)
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
            existing.CreatedOn = archive.CreatedOn;
            existing.UpdatedOn = archive.UpdatedOn;
            
            existing = await archiveService.UpdateAsync(existing);
            
            await archiveService.WriteToFileAsync(existing, archiveEntry.OpenEntryStream());
            
            if (File.Exists(existingPath))
                File.Delete(existingPath);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update archive | {Key}", GetKey(archive));
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(Archive archive)
    {
        if (ImportContext.Manifest is Game game)
            return await archiveService.ExistsAsync(a => a.Version == archive.Version && a.GameId == game.Id);
        
        if (ImportContext.Manifest is Redistributable redistributable)
            return await archiveService.ExistsAsync(a => a.Version == archive.Version && a.RedistributableId == redistributable.Id);
        
        throw new ImportSkippedException<Archive>(archive, $"Cannot import archive, incompatible manifest");
    }
}