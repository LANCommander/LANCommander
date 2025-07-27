using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class TagImporter(
    IMapper mapper,
    TagService tagService,
    GameService gameService) : BaseImporter<Tag, Data.Models.Tag>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Tag record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Tags,
            Name = record.Name,
        };
    }

    public override bool CanImport(Tag record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.Tag> AddAsync(Tag record)
    {
        try
        {
            var tag = new Data.Models.Tag
            {
                Games = new List<Data.Models.Game>() { ImportContext.DataRecord as Data.Models.Game },
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

    public override async Task<Data.Models.Tag> UpdateAsync(Tag record)
    {
        var existing = await tagService.Include(t => t.Games).FirstOrDefaultAsync(c => c.Name == record.Name);
        var game = ImportContext.DataRecord as Data.Models.Game;
        
        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();

            if (!existing.Games.Any(g => g.Id == game.Id))
            {
                existing.Games.Add(await gameService.GetAsync(game.Id));
                
                existing = await tagService.UpdateAsync(existing);
            }

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tag>(record, "An unknown error occured while importing tag", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Tag record)
    {
        return await tagService.ExistsAsync(c => c.Name == record.Name);
    }
}