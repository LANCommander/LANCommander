using System.Web.Services.Description;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services.Importers;

/// <summary>
/// Implements importing for archive records
/// </summary>
/// <param name="serviceProvider">Valid service provider for injecting the services we need</param>
/// <param name="importContext">The context (archive, parent record> of the import</param>
public class ArchiveImporter<TParentRecord>(
    ArchiveService archiveService,
    ImportContext<TParentRecord> importContext) : IImporter<Archive, Data.Models.Archive> where TParentRecord : Data.Models.BaseModel
{
    public async Task<ImportItemInfo> InfoAsync(Archive record)
    {
        return new ImportItemInfo
        {
            Name = record.Version,
            Size = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Archives/{record.Id}")?.Size ?? 0,
        };
    }

    public bool CanImport(Archive record) => importContext.Record is Data.Models.Game || importContext.Record is Data.Models.Redistributable;
    
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

    public async Task<Data.Models.Archive> UpdateAsync(Archive archive)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Archives/{archive.Id}");
        var existing = await archiveService.Include(a => a.StorageLocation).FirstOrDefaultAsync(a => archive.Id == a.Id);
        var existingPath = await archiveService.GetArchiveFileLocationAsync(existing);
        
        if (archiveEntry == null)
            throw new ImportSkippedException<Archive>(archive, "Matching archive file does not exist in import archive");
        
        try
        {
            existing.Version = archive.Version;
            existing.Changelog = archive.Changelog;
            existing.StorageLocation = importContext.ArchiveStorageLocation;
            
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

    public async Task<bool> ExistsAsync(Archive archive)
    {
        if (importContext.Record is Data.Models.Game game)
            return await archiveService.ExistsAsync(a => a.Version == archive.Version && a.GameId == game.Id);
        
        if (importContext.Record is Data.Models.Redistributable redistributable)
            return await archiveService.ExistsAsync(a => a.Version == archive.Version && a.RedistributableId == redistributable.Id);
        
        throw new ImportSkippedException<Archive>(archive, $"Cannot import an archive for a {typeof(TParentRecord).Name}");
    }
}