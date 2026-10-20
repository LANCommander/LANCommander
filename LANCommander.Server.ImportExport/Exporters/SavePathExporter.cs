using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;

namespace LANCommander.Server.ImportExport.Exporters;

public class SavePathExporter(ManifestMapper manifestMapper, SavePathService savePathService) : BaseExporter<SavePath, Data.Models.SavePath>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.SavePath record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.SavePath,
            Name = record.Path,
        };
    }

    public override bool CanExport(SavePath record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<SavePath> ExportAsync(Guid id)
    {
        return await savePathService.GetAsync(id, manifestMapper.ProjectToManifestSavePath);
    }
} 