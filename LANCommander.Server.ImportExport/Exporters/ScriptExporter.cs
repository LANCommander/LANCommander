using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class ScriptExporter(
    IMapper mapper,
    ScriptService scriptService) : BaseExporter<Script, Data.Models.Script>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Script record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Script,
            Name = record.Name,
            Size = record.Contents.Length,
        };
    }

    public override bool CanExport(Script record) =>
        ExportContext.DataRecord is Data.Models.Game
        ||
        ExportContext.DataRecord is Data.Models.Redistributable
        ||
        ExportContext.DataRecord is Data.Models.Server;

    public override async Task<Script> ExportAsync(Guid id)
    {
        var entity = await scriptService.GetAsync(id);

        try
        {
            var scriptEntry = ExportContext.Archive.CreateEntry($"Scripts/{id}");

            using (var ms = new MemoryStream())
            using (var writer = new StreamWriter(ms))
            {
                await writer.WriteAsync(entity.Contents);
                await writer.FlushAsync();

                await using (var entryStream = scriptEntry.Open())
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    await ms.CopyToAsync(entryStream);
                }
            }

            return mapper.Map<Script>(entity);
        }
        catch (Exception ex)
        {
            throw new ExportSkippedException<Data.Models.Script>(entity, "Could not add script to export file", ex);
        }
    }
} 