using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.Services.Importers;

public class TagImporter(
    IMapper mapper,
    TagService tagService) : BaseImporter<Tag, Data.Models.Tag>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Tag record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Tags,
            Name = record.Name,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Tag record)
    {
        return new ExportItemInfo
        {
            Flag = ImportRecordFlags.Tags,
            Name = record.Name,
        };
    }

    public override bool CanImport(Tag record) => ImportContext.DataRecord is Data.Models.Game;
    public override bool CanExport(Tag record) => ImportContext.DataRecord is Data.Models.Game;

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

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(ImportContext.DataRecord as Data.Models.Game);
            
            existing = await tagService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Tag>(record, "An unknown error occured while importing tag", ex);
        }
    }

    public override async Task<Tag> ExportAsync(Data.Models.Tag entity)
    {
        return mapper.Map<Tag>(entity);
    }

    public override async Task<bool> ExistsAsync(Tag record)
    {
        return await tagService.ExistsAsync(c => c.Name == record.Name);
    }
}