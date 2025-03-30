using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LANCommander.Server.Services.Importers;

/// <summary>
/// Implements importing for archive records
/// </summary>
/// <param name="serviceProvider">Valid service provider for injecting the services we need</param>
/// <param name="importContext">The context (archive, parent record> of the import</param>
public class ArchiveImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Archive, Data.Models.Archive>
{
    ArchiveService _archiveService = serviceProvider.GetRequiredService<ArchiveService>();
    
    public async Task<Data.Models.Archive> AddAsync(Archive record)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Archives/{record.Id}");

        if (archiveEntry == null)
            throw new ImportSkippedException<Archive>(record, "Matching archive file does not exist in import archive");

        Data.Models.Archive archive = null;
        string path = "";

        try
        {
            var newArchive = new Data.Models.Archive()
            {
                CreatedOn = record.CreatedOn,
                StorageLocation = importContext.ArchiveStorageLocation,
                Version = record.Version,
                Changelog = record.Changelog,
            };

            if (importContext.Record is Data.Models.Game game)
                newArchive.Game = game;
            else if (importContext.Record is Data.Models.Redistributable redistributable)
                newArchive.Redistributable = redistributable;
            else
                throw new ImportSkippedException<Archive>(record,
                    $"Cannot import an archive for a {typeof(TParentRecord).Name}");
            
            archive = await _archiveService.AddAsync(newArchive);
            archive = await _archiveService.WriteToFileAsync(archive, archiveEntry.OpenEntryStream());

            return archive;
        }
        catch (Exception ex)
        {
            if (archive != null)
                await _archiveService.DeleteAsync(archive);
            
            throw new ImportSkippedException<Archive>(record, "An unknown error occured while importing archive file", ex);
        }
    }

    public async Task<Data.Models.Archive> UpdateAsync(Archive archive)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Archives/{archive.Id}");
        var existing = await _archiveService.Include(a => a.StorageLocation).FirstOrDefaultAsync(a => archive.Id == a.Id);
        var existingPath = await _archiveService.GetArchiveFileLocationAsync(existing);
        
        if (archiveEntry == null)
            throw new ImportSkippedException<Archive>(archive, "Matching archive file does not exist in import archive");
        
        try
        {
            existing.Version = archive.Version;
            existing.Changelog = archive.Changelog;
            existing.StorageLocation = importContext.ArchiveStorageLocation;
            
            existing = await _archiveService.UpdateAsync(existing);
            existing = await _archiveService.WriteToFileAsync(existing, archiveEntry.OpenEntryStream());
            
            if (File.Exists(existingPath))
                File.Delete(existingPath);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Archive>(archive, "An unknown error occured while importing archive file", ex);
        }
    }

    public async Task<bool> ExistsAsync(Archive archive)
    {
        if (importContext.Record is not Data.Models.Game game || importContext.Record is not Data.Models.Redistributable)
            throw new ImportSkippedException<Archive>(archive,
                $"Cannot import an archive for a {typeof(TParentRecord).Name}");
        
        return await _archiveService
            .ExistsAsync(a => a.Version == archive.Version && a.GameId == game.Id);
    }
}