using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;

namespace LANCommander.Server.ImportExport.Exporters;

public class TagExporter(ManifestMapper manifestMapper, TagService tagService) : BaseExporter<Tag, Data.Models.Tag>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Tag record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Tag,
            Name = record.Name,
        };
    }

    public override bool CanExport(Tag record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Tag> ExportAsync(Guid id)
    {
        return await tagService.GetAsync(id, manifestMapper.ProjectToManifestTag);
    }
} 