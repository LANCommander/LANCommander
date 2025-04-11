using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class ScriptImporter(
    IMapper mapper,
    ScriptService scriptService,
    ImportContext importContext,
    ExportContext exportContext) : IImporter<Script, Data.Models.Script>
{
    public async Task<ImportItemInfo> InfoAsync(Script record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Scripts,
            Name = record.Name,
            Size = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}")?.Size ?? 0,
        };
    }

    public bool CanImport(Script record) =>
        importContext.DataRecord is Data.Models.Game
        ||
        importContext.DataRecord is Data.Models.Redistributable
        ||
        importContext.DataRecord is Data.Models.Server;

    public bool CanExport(Script record) =>
        exportContext.DataRecord is Data.Models.Game
        ||
        exportContext.DataRecord is Data.Models.Redistributable
        ||
        exportContext.DataRecord is Data.Models.Server;

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

            if (importContext.DataRecord is Data.Models.Game game)
                newScript.Game = game;
            else if (importContext.DataRecord is Data.Models.Redistributable redistributable)
                newScript.Redistributable = redistributable;
            else if (importContext.DataRecord is Data.Models.Server server)
                newScript.Server = server;

            using (var streamReader = new StreamReader(archiveEntry.OpenEntryStream()))
            {
                newScript.Contents = await streamReader.ReadToEndAsync();
            }

            script = await scriptService.AddAsync(newScript);

            return script;
        }
        catch (Exception ex)
        {
            if (script != null)
                await scriptService.DeleteAsync(script);
            
            throw new ImportSkippedException<Script>(record, "An unknown error occured while importing script", ex);
        }
    }

    public async Task<Data.Models.Script> UpdateAsync(Script record)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}");

        Data.Models.Script existing = null;
        
        if (importContext.DataRecord is Data.Models.Game game)
            existing = await scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.GameId == game.Id);
        else if (importContext.DataRecord is Data.Models.Redistributable redistributable)
            existing = await scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.RedistributableId == redistributable.Id);
        else if (importContext.DataRecord is Data.Models.Server server)
            existing = await scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.ServerId == server.Id);

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

            existing = await scriptService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Script>(record, "An unknown error occured while importing script", ex);
        }
    }

    public async Task<Script> ExportAsync(Data.Models.Script entity)
    {
        using (var stream = new MemoryStream())
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(entity.Contents);
            writer.Flush();
            
            stream.Position = 0;
            
            exportContext.Archive.AddEntry($"Scripts/{entity.Id}", stream);
        }
        
        return mapper.Map<Script>(entity);
    }

    public async Task<bool> ExistsAsync(Script record)
    {
        if (importContext.DataRecord is Data.Models.Game game)
            return await scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.GameId == game.Id);
        
        if (importContext.DataRecord is Data.Models.Redistributable redistributable)
            return await scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.RedistributableId == redistributable.Id);
        
        if (importContext.DataRecord is Data.Models.Server server)
            return await scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.ServerId == server.Id);

        return false;
    }
}