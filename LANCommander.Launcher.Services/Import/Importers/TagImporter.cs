using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class TagImporter(
    TagService tagService,
    ILogger<TagImporter> logger) : BaseImporter<Tag>
{
    public override async Task<ImportItemInfo<Tag>> GetImportInfoAsync(Tag record, BaseManifest manifest) =>
        new()
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Tag),
            Record = record,
        };

    public override string GetKey(Tag record) => $"{nameof(Tag)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Tag record)
    {
        var existing = await tagService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<bool> AddAsync(ImportItemInfo<Tag> importItemInfo)
    {
        var tag = new Data.Models.Tag
        {
            Name = importItemInfo.Record.Name,
            CreatedOn = importItemInfo.Record.CreatedOn,
            UpdatedOn = importItemInfo.Record.UpdatedOn,
            ImportedOn = DateTime.UtcNow
        };

        try
        {
            await tagService.AddAsync(tag);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add tag | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<Tag> importItemInfo) => true;
    public override async Task<bool> ExistsAsync(ImportItemInfo<Tag> importItemInfo)
        => await tagService.ExistsAsync(c => c.Name == importItemInfo.Record.Name);
}