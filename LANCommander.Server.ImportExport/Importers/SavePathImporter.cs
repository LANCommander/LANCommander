using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class SavePathImporter(
    IMapper mapper,
    SavePathService savePathService) : BaseImporter<SavePath, Data.Models.SavePath>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(SavePath record)
    {
        return new ImportItemInfo
        {
            Type = ImportExportRecordType.SavePath,
            Name = record.Path,
        };
    }

    public override bool CanImport(SavePath record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.SavePath> AddAsync(SavePath record)
    {
        try
        {
            var savePath = new Data.Models.SavePath
            {
                Id = record.Id,
                Game = ImportContext.DataRecord as Data.Models.Game,
                Path = record.Path,
                WorkingDirectory = record.WorkingDirectory,
                IsRegex = record.IsRegex,
                Type = record.Type,
            };

            savePath = await savePathService.AddAsync(savePath);

            return savePath;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<SavePath>(record, "An unknown error occured while importing save path", ex);
        }
    }

    public override async Task<Data.Models.SavePath> UpdateAsync(SavePath record)
    {
        var existing = await savePathService.FirstOrDefaultAsync(p => p.Id == record.Id);

        try
        {
            existing.Game = ImportContext.DataRecord as Data.Models.Game;
            existing.Path = record.Path;
            existing.WorkingDirectory = record.WorkingDirectory;
            existing.IsRegex = record.IsRegex;
            existing.Type = record.Type;
            
            existing = await savePathService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<SavePath>(record, "An unknown error occured while importing save path", ex);
        }
    }

    public override async Task<bool> ExistsAsync(SavePath record)
    {
        return await savePathService.ExistsAsync(p => p.Id == record.Id);
    }
}