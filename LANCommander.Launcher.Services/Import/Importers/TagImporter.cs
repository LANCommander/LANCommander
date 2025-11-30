using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class TagImporter(
    TagService tagService,
    ILogger<TagImporter> logger) : BaseImporter<Tag, Data.Models.Tag>
{
    public override async Task<ImportItemInfo<Tag>> GetImportInfoAsync(Tag record)
    {
        return new ImportItemInfo<Tag>
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Tag),
            Record = record,
        };
    }
    
    public override string GetKey(Tag record) => $"{nameof(Tag)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Tag record)
    {
        var existing = await tagService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<Data.Models.Tag> AddAsync(Tag record)
    {
        var tag = new Data.Models.Tag
        {
            Name = record.Name,
            CreatedOn = record.CreatedOn,
            UpdatedOn = record.UpdatedOn,
            ImportedOn = DateTime.UtcNow
        };

        try
        {
            return await tagService.AddAsync(tag);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tag>(record, "An unknown error occurred while trying to add tag", ex);
        }
    }

    public override async Task<Data.Models.Tag> UpdateAsync(Tag record)
    {
        var existing = await tagService.FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            existing.Name = record.Name;
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedOn = record.UpdatedOn;
            existing.ImportedOn = DateTime.UtcNow;

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tag>(record, "An unknown error occurred while trying to update tag", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Tag record) => await tagService.ExistsAsync(c => c.Name == record.Name);
}