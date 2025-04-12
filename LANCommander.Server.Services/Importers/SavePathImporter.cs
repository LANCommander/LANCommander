using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class SavePathImporter(
    IMapper mapper,
    SavePathService savePathService,
    ImportContext importContext) : IImporter<SavePath, Data.Models.SavePath>
{
    public async Task<ImportItemInfo> GetImportInfoAsync(SavePath record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.SavePaths,
            Name = record.Path,
        };
    }

    public async Task<ImportItemInfo> GetExportInfoAsync(SavePath record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.SavePaths,
            Name = record.Path,
        };
    }

    public bool CanImport(SavePath record) => importContext.DataRecord is Data.Models.Game;
    public bool CanExport(SavePath record) => importContext.DataRecord is Data.Models.Game;

    public async Task<Data.Models.SavePath> AddAsync(SavePath record)
    {
        try
        {
            var savePath = new Data.Models.SavePath
            {
                Id = record.Id,
                Game = importContext.DataRecord as Data.Models.Game,
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

    public async Task<Data.Models.SavePath> UpdateAsync(SavePath record)
    {
        var existing = await savePathService.FirstOrDefaultAsync(p => p.Id == record.Id);

        try
        {
            existing.Game = importContext.DataRecord as Data.Models.Game;
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

    public async Task<SavePath> ExportAsync(Data.Models.SavePath entity)
    {
        return mapper.Map<SavePath>(entity);
    }

    public async Task<bool> ExistsAsync(SavePath record)
    {
        return await savePathService.ExistsAsync(p => p.Id == record.Id);
    }
}