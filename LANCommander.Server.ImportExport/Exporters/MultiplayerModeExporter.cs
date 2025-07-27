using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class MultiplayerModeExporter(
    MultiplayerModeService multiplayerModeService) : BaseExporter<MultiplayerMode, Data.Models.MultiplayerMode>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.MultiplayerMode record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ExportRecordFlags.MultiplayerModes,
            Name = String.IsNullOrWhiteSpace(record.Description) ? record.Type.ToString() : $"{record.Type} - {record.Description}",
        };
    }

    public override bool CanExport(MultiplayerMode record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<MultiplayerMode> ExportAsync(Guid id)
    {
        return await multiplayerModeService.GetAsync<MultiplayerMode>(id);
    }
} 