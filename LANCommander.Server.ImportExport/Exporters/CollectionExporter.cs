using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class CollectionExporter(
    CollectionService collectionService) : BaseExporter<Collection, Data.Models.Collection>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Collection record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ExportRecordFlags.Collections,
            Name = record.Name,
        };
    }

    public override bool CanExport(Collection record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Collection> ExportAsync(Guid id)
    {
        return await collectionService.GetAsync<Collection>(id);
    }
} 