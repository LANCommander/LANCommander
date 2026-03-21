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
    RedistributableService redistributableService,
    ToolService toolService,
    GameImporter gameImporter,
    RedistributableImporter redistributableImporter,
    ToolImporter toolImporter) : BaseImporter<Archive>
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

    public override async Task<bool> CanImportAsync(Archive record) => ImportContext.Manifest is Game || ImportContext.Manifest is Redistributable || ImportContext.Manifest is Tool;

    public override async Task<bool> AddAsync(Archive record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Archives/{record.Id}");
        
        if (archiveEntry != null)
            AddAsset(new ImportAssetArchiveEntry
            {
                RecordId = record.Id,
                Name = $"{record.Version}",
                Path = archiveEntry.Key!,
            });

        Data.Models.Archive archive = null;
        string path = "";

        try
        {
            var newArchive = new Data.Models.Archive()
            {
                Id = record.Id,
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
            {
                if (ImportContext.InQueue(game, gameImporter))
                    return false;
                
                newArchive.Game = await gameService.GetAsync(game.Id);
            }
            else if (ImportContext.Manifest is Redistributable redistributable)
            {
                if (ImportContext.InQueue(redistributable, redistributableImporter))
                    return false;
                
                newArchive.Redistributable = await redistributableService.GetAsync(redistributable.Id);
            }
            else if (ImportContext.Manifest is Tool tool)
            {
                if (ImportContext.InQueue(tool, toolImporter))
                    return false;
                
                newArchive.Tool = await toolService.GetAsync(tool.Id);
            }
                
            else
                throw new ImportSkippedException<Archive>(record,
                    $"Cannot import an archive for a {record.GetType().Name}");
            
            archive = await archiveService.AddAsync(newArchive);

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

        if (archiveEntry != null)
            AddAsset(new ImportAssetArchiveEntry
            {
                RecordId = archive.Id,
                Name = $"{archive.Version}",
                Path = archiveEntry.Key!,
            });
        
        try
        {
            existing.Version = archive.Version;
            existing.Changelog = archive.Changelog;
            existing.StorageLocation = ImportContext.ArchiveStorageLocation;
            existing.CreatedOn = archive.CreatedOn;
            existing.UpdatedOn = archive.UpdatedOn;
            
            if (ImportContext.Manifest is Game game)
            {
                if (ImportContext.InQueue(game, gameImporter))
                    return false;
                
                existing.Game = await gameService.GetAsync(game.Id);
            }
            else if (ImportContext.Manifest is Redistributable redistributable)
            {
                if (ImportContext.InQueue(redistributable, redistributableImporter))
                    return false;
                
                existing.Redistributable = await redistributableService.GetAsync(redistributable.Id);
            }
            else if (ImportContext.Manifest is Tool tool)
            {
                if (ImportContext.InQueue(tool, toolImporter))
                    return false;
                
                existing.Tool = await toolService.GetAsync(tool.Id);
            }
            
            await archiveService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update archive | {Key}", GetKey(archive));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        try
        {
            if (asset is ImportAssetArchiveEntry archiveEntryAsset)
            {
                var archive = await archiveService.GetAsync(archiveEntryAsset.RecordId);
                var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == archiveEntryAsset.Path);

                if (archiveEntry == null)
                    return false;
                
                await archiveService.WriteToFileAsync(archive, archiveEntry.OpenEntryStream());

                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not ingest archive entry");
        }

        return false;
    }

    public override async Task<bool> ExistsAsync(Archive archive)
        => await archiveService.ExistsAsync(archive.Id);
}