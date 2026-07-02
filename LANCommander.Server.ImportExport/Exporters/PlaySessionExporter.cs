using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;

namespace LANCommander.Server.ImportExport.Exporters;

public class PlaySessionExporter(ManifestMapper manifestMapper, PlaySessionService playSessionService) : BaseExporter<PlaySession, Data.Models.PlaySession>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.PlaySession record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.PlaySession,
            Name = $"{record.User} - {record.Start}-{record.End}",
        };
    }

    public override bool CanExport(PlaySession record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<PlaySession> ExportAsync(Guid id)
    {
        return await playSessionService.GetAsync(id, manifestMapper.ProjectToManifestPlaySession);
    }
} 