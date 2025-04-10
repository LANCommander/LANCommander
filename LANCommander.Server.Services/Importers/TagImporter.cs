using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class TagImporter<TParentRecord>(
    TagService tagService,
    ImportContext<TParentRecord> importContext) : IImporter<Tag, Data.Models.Tag>
    where TParentRecord : Data.Models.BaseModel
{
    public async Task<ImportItemInfo> InfoAsync(Tag record)
    {
        return new ImportItemInfo
        {
            Name = record.Name,
        };
    }

    public bool CanImport(Tag record) => importContext.Record is Data.Models.Game;

    public async Task<Data.Models.Tag> AddAsync(Tag record)
    {
        try
        {
            var tag = new Data.Models.Tag
            {
                Games = new List<Data.Models.Game>() { importContext.Record as Data.Models.Game },
                Name = record.Name,
            };

            tag = await tagService.AddAsync(tag);

            return tag;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tag>(record, "An unknown error occured while importing tag", ex);
        }
    }

    public async Task<Data.Models.Tag> UpdateAsync(Tag record)
    {
        var existing = await tagService.Include(t => t.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(importContext.Record as Data.Models.Game);
            
            existing = await tagService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tag>(record, "An unknown error occured while importing tag", ex);
        }
    }

    public async Task<bool> ExistsAsync(Tag record)
    {
        return await tagService.ExistsAsync(c => c.Name == record.Name);
    }
}