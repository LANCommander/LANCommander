using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class SaveExporter(
    IMapper mapper,
    GameSaveService gameSaveService) : BaseExporter<Save, Data.Models.GameSave>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.GameSave record)
    {
        var savePath = await gameSaveService.GetSavePathAsync(record.Id);

        var info = new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Save,
            Name = $"{record.User} - {record.CreatedOn}",
        };

        if (File.Exists(savePath))
        {
            var fileInfo = new FileInfo(savePath);
            
            if (fileInfo.Exists)
                info.Size = fileInfo.Length;
        }

        return info;
    }

    public override bool CanExport(Save record) => ExportContext.DataRecord is Data.Models.Game;

    public override async Task<Save> ExportAsync(Guid id)
    {
        var entity = await gameSaveService.GetAsync(id);

        try
        {
            var savePath = await gameSaveService.GetSavePathAsync(id);

            if (Path.Exists(savePath))
            {
                var saveEntry = ExportContext.Archive.CreateEntry($"Saves/{id}");

                using (var saveEntryStream = saveEntry.Open())
                using (var saveFileStream = new FileStream(savePath, FileMode.Open))
                {
                    await saveFileStream.CopyToAsync(saveEntryStream);
                }
            }
            
            return mapper.Map<Save>(entity);
        }
        catch (Exception ex)
        {
            throw new ExportSkippedException<Data.Models.GameSave>(entity, "Could not add save to export file", ex);
        }
    }
} 