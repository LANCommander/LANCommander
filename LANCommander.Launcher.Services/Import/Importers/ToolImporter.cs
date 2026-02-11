using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models.Manifest;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class ToolImporter(
    ToolService toolService,
    LibraryService libraryService,
    ILogger<ToolImporter> logger) : BaseImporter<Tool>
{
    public override async Task<ImportItemInfo<Tool>> GetImportInfoAsync(Tool record, BaseManifest manifest) =>
        new()
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Game),
            Record = record,
        };

    public override string GetKey(Tool record) => $"{nameof(Tool)}/{record.Id}";

    public override async Task<bool> CanImportAsync(Tool record)
    {
        var existing = await toolService.GetAsync(record.Id);
        
        if (existing == null)
            return true;

        return
            record.UpdatedOn > existing.ImportedOn
            ||
            record.Actions.Any(a => a.UpdatedOn > existing.ImportedOn || a.CreatedOn > existing.ImportedOn);
    }

    public override async Task<bool> AddAsync(ImportItemInfo<Tool> importItemInfo)
    {
        try
        {
            var tool = new Data.Models.Tool
            {
                Id = importItemInfo.Record.Id,
                Name = importItemInfo.Record.Name,
                Description = importItemInfo.Record.Description,
                Notes = importItemInfo.Record.Notes,
                CreatedOn = importItemInfo.Record.CreatedOn,
                UpdatedOn = importItemInfo.Record.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            };
            
            await toolService.AddAsync(tool);
            await UpdateRelationships(importItemInfo.Record);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add tool | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<Tool> importItemInfo)
    {
        var existing = await toolService.GetAsync(importItemInfo.Record.Id);

        try
        {
            existing.Name = importItemInfo.Record.Name;
            existing.Description = importItemInfo.Record.Description;
            existing.Notes = importItemInfo.Record.Notes;
            existing.CreatedOn = importItemInfo.Record.CreatedOn;
            existing.ImportedOn = DateTime.UtcNow;
            // existing.LatestVersion = importItemInfo.Record.Version;
            
            await toolService.UpdateAsync(existing);
            await UpdateRelationships(importItemInfo.Record);
            
            if (await libraryService.IsInstalledAsync(existing.Id) && existing.LatestVersion == existing.InstalledVersion)
                await ManifestHelper.WriteAsync(importItemInfo.Record, existing.InstallDirectory);

            return true;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tool>(importItemInfo.Record, "An unknown error occurred while trying to update tool", ex);
        }
    }

    private async Task UpdateRelationships(Tool manifest)
    {
        var tool = await toolService.GetAsync(manifest.Id);

        await toolService.SyncRelatedCollectionAsync(
            tool,
            t => t.Games,
            manifest.Games,
            r => c => c.Title == r.Title);
    }

    public override async Task<bool> ExistsAsync(ImportItemInfo<Tool> importItemInfo) => await toolService.ExistsAsync(importItemInfo.Record.Id);
}