using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class ScriptImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Script, Data.Models.Script>
{
    ScriptService _scriptService = serviceProvider.GetRequiredService<ScriptService>();
    
    public async Task<Data.Models.Script> AddAsync(Script record)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}");
        
        if (archiveEntry == null)
            throw new ImportSkippedException<Script>(record, "Matching script file does not exist in import archive");

        Data.Models.Script script = null;
        string path = "";

        try
        {
            var newScript = new Data.Models.Script
            {
                CreatedOn = record.CreatedOn,
                Name = record.Name,
                Description = record.Description,
                RequiresAdmin = record.RequiresAdmin,
                Type = record.Type,
            };

            if (importContext.Record is Data.Models.Game game)
                newScript.Game = game;
            else if (importContext.Record is Data.Models.Redistributable redistributable)
                newScript.Redistributable = redistributable;
            else if (importContext.Record is Data.Models.Server server)
                newScript.Server = server;
            else
                throw new ImportSkippedException<Script>(record,
                    $"Cannot import a script for a {typeof(TParentRecord).Name}");

            using (var streamReader = new StreamReader(archiveEntry.OpenEntryStream()))
            {
                newScript.Contents = await streamReader.ReadToEndAsync();
            }

            script = await _scriptService.AddAsync(newScript);

            return script;
        }
        catch (Exception ex)
        {
            if (script != null)
                await _scriptService.DeleteAsync(script);
            
            throw new ImportSkippedException<Script>(record, "An unknown error occured while importing script", ex);
        }
    }

    public async Task<Data.Models.Script> UpdateAsync(Script record)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}");

        Data.Models.Script existing;
        
        if (importContext.Record is Data.Models.Game game)
            existing = await _scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.GameId == game.Id);
        else if (importContext.Record is Data.Models.Redistributable redistributable)
            existing = await _scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.RedistributableId == redistributable.Id);
        else if (importContext.Record is Data.Models.Server server)
            existing = await _scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.ServerId == server.Id);
        else
            throw new ImportSkippedException<Script>(record,
                $"Cannot import a script for a {typeof(TParentRecord).Name}");

        try
        {
            existing.CreatedOn = record.CreatedOn;
            existing.Name = record.Name;
            existing.Description = record.Description;
            existing.RequiresAdmin = record.RequiresAdmin;
            existing.Type = record.Type;

            using (var streamReader = new StreamReader(archiveEntry.OpenEntryStream()))
            {
                existing.Contents = await streamReader.ReadToEndAsync();
            }

            existing = await _scriptService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Script>(record, "An unknown error occured while importing script", ex);
        }
    }

    public Task<bool> ExistsAsync(Script record)
    {
        if (importContext.Record is Data.Models.Game game)
            return _scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.GameId == game.Id);
        
        if (importContext.Record is Data.Models.Redistributable redistributable)
            return _scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.RedistributableId == redistributable.Id);
        
        if (importContext.Record is Data.Models.Server server)
            return _scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.ServerId == server.Id);
        
        throw new ImportSkippedException<Script>(record, $"Cannot import a script for a {typeof(TParentRecord).Name}");
    }
}