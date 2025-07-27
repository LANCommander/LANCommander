using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class PlaySessionExporter(PlaySessionService playSessionService) : BaseExporter<PlaySession, Data.Models.PlaySession>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.PlaySession record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ExportRecordFlags.PlaySessions,
            Name = $"{record.User} - {record.Start}-{record.End}",
        };
    }

    public override bool CanExport(PlaySession record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<PlaySession> ExportAsync(Guid id)
    {
        return await playSessionService.GetAsync<PlaySession>(id);
    }
} 