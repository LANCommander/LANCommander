using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class KeyExporter(
    KeyService keyService) : BaseExporter<Key, Data.Models.Key>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Key record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ExportRecordFlags.Keys,
            Name = new String('*', record.Value.Length),
        };
    }

    public override bool CanExport(Key record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Key> ExportAsync(Guid id)
    {
        return await keyService.GetAsync<Key>(id);
    }
} 