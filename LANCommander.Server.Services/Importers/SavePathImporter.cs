using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class SavePathImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<SavePath, Data.Models.SavePath>
{
    SavePathService _savePathService = serviceProvider.GetRequiredService<SavePathService>();
    
    public async Task<Data.Models.SavePath> AddAsync(SavePath record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<SavePath>(record, $"Cannot import save path for a {typeof(TParentRecord).Name}");

        try
        {
            var savePath = new Data.Models.SavePath
            {
                Id = record.Id,
                Game = game,
                Path = record.Path,
                WorkingDirectory = record.WorkingDirectory,
                IsRegex = record.IsRegex,
                Type = record.Type,
            };

            savePath = await _savePathService.AddAsync(savePath);

            return savePath;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<SavePath>(record, "An unknown error occured while importing save path", ex);
        }
    }

    public async Task<Data.Models.SavePath> UpdateAsync(SavePath record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<SavePath>(record, $"Cannot import savePaths for a {typeof(TParentRecord).Name}");

        var existing = await _savePathService.FirstOrDefaultAsync(p => p.Id == record.Id);

        try
        {
            existing.Path = record.Path;
            existing.WorkingDirectory = record.WorkingDirectory;
            existing.IsRegex = record.IsRegex;
            existing.Type = record.Type;
            
            existing = await _savePathService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<SavePath>(record, "An unknown error occured while importing save path", ex);
        }
    }

    public async Task<bool> ExistsAsync(SavePath record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<SavePath>(record, $"Cannot import savePaths for a {typeof(TParentRecord).Name}");
        
        return await _savePathService.ExistsAsync(p => p.Id == record.Id);
    }
}