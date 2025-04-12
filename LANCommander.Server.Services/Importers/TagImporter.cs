using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.Services.Importers;

public class TagImporter(
    IMapper mapper,
    TagService tagService,
    ImportContext importContext) : IImporter<Tag, Data.Models.Tag>
{
    public async Task<ImportItemInfo> GetImportInfoAsync(Tag record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Tags,
            Name = record.Name,
        };
    }

    public async Task<ImportItemInfo> GetExportInfoAsync(Tag record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Tags,
            Name = record.Name,
        };
    }

    public bool CanImport(Tag record) => importContext.DataRecord is Data.Models.Game;
    public bool CanExport(Tag record) => importContext.DataRecord is Data.Models.Game;

    public async Task<Data.Models.Tag> AddAsync(Tag record)
    {
        try
        {
            var tag = new Data.Models.Tag
            {
                Games = new List<Data.Models.Game>() { importContext.DataRecord as Data.Models.Game },
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
            
            existing.Games.Add(importContext.DataRecord as Data.Models.Game);
            
            existing = await tagService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tag>(record, "An unknown error occured while importing tag", ex);
        }
    }

    public async Task<Tag> ExportAsync(Data.Models.Tag entity)
    {
        return mapper.Map<Tag>(entity);
    }

    public async Task<bool> ExistsAsync(Tag record)
    {
        return await tagService.ExistsAsync(c => c.Name == record.Name);
    }
}