using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class TagImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Tag, Data.Models.Tag>
{
    TagService _tagService = serviceProvider.GetRequiredService<TagService>();
    
    public async Task<Data.Models.Tag> AddAsync(Tag record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Tag>(record, $"Cannot import tags for a {typeof(TParentRecord).Name}");

        try
        {
            var tag = new Data.Models.Tag
            {
                Games = new List<Data.Models.Game>() { game },
                Name = record.Name,
            };

            tag = await _tagService.AddAsync(tag);

            return tag;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tag>(record, "An unknown error occured while importing tag", ex);
        }
    }

    public async Task<Data.Models.Tag> UpdateAsync(Tag record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Tag>(record, $"Cannot import tags for a {typeof(TParentRecord).Name}");

        var existing = await _tagService.Include(t => t.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(game);
            
            existing = await _tagService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tag>(record, "An unknown error occured while importing tag", ex);
        }
    }

    public async Task<bool> ExistsAsync(Tag record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Tag>(record, $"Cannot import tags for a {typeof(TParentRecord).Name}");
        
        return await _tagService.ExistsAsync(c => c.Name == record.Name);
    }
}