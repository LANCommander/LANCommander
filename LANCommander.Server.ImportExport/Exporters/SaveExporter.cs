using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;

namespace LANCommander.Server.ImportExport.Exporters;

public class SaveExporter(
    ManifestMapper manifestMapper,
    GameSaveService gameSaveService) : BaseExporter<Save, Data.Models.GameSave>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.GameSave record)
    {
        var savePath = await gameSaveService.GetSavePathAsync(record.Id);

        var info = new ExportItemInfo
        {
            Id = record.Id,
            Type = ImportExportRecordType.Save,
            Name = $"{record.User?.UserName} - {record.CreatedOn}",
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
        // The manifest names the save's owner, and the importer won't take a save without one
        var entity = await gameSaveService
            .Include(s => s.User, s => s.CreatedBy, s => s.UpdatedBy)
            .GetAsync(id);

        try
        {
            var savePath = await gameSaveService.GetSavePathAsync(id);

            if (File.Exists(savePath))
                await WriteEntryFromFileAsync($"Saves/{id}", savePath);
            
            return manifestMapper.ToManifest(entity);
        }
        catch (Exception ex)
        {
            throw new ExportSkippedException<Data.Models.GameSave>(entity, "Could not add save to export file", ex);
        }
    }
} 