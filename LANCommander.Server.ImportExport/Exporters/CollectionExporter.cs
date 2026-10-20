using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;

namespace LANCommander.Server.ImportExport.Exporters;

public class CollectionExporter(
    ManifestMapper manifestMapper,
    CollectionService collectionService) : BaseExporter<Collection, Data.Models.Collection>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Collection record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Collection,
            Name = record.Name,
        };
    }

    public override bool CanExport(Collection record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Collection> ExportAsync(Guid id)
    {
        return await collectionService.GetAsync(id, manifestMapper.ProjectToManifestCollection);
    }
} 