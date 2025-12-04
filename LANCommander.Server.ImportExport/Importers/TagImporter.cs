using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class TagImporter(
    ILogger<TagImporter> logger,
    TagService tagService) : BaseImporter<Tag>
{
    public override string GetKey(Tag record)
        => $"{nameof(Tag)}/{record.Name}";

    public override async Task<ImportItemInfo<Tag>> GetImportInfoAsync(Tag record)
        => new()
        {
            Type = ImportExportRecordType.Tag,
            Name = record.Name,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Tag record)
        => !await tagService.ExistsAsync(t => t.Name == record.Name);

    public override async Task<bool> AddAsync(Tag record)
    {
        try
        {
            var tag = new Data.Models.Tag
            {
                Name = record.Name,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
            };

            await tagService.AddAsync(tag);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add tag | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Tag record) => true;

    public override async Task<bool> ExistsAsync(Tag record) 
        => await tagService.ExistsAsync(c => c.Name == record.Name);
}