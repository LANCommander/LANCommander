using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class ScriptImporter(
    IMapper mapper,
    ScriptService scriptService) : BaseImporter<Script, Data.Models.Script>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Script record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Scripts,
            Name = record.Name,
            Size = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}")?.Size ?? 0,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Script record)
    {
        return new ExportItemInfo
        {
            Flag = ImportRecordFlags.Scripts,
            Name = record.Name,
            Size = record.Contents.Length,
        };
    }

    public override bool CanImport(Script record) =>
        ImportContext.DataRecord is Data.Models.Game
        ||
        ImportContext.DataRecord is Data.Models.Redistributable
        ||
        ImportContext.DataRecord is Data.Models.Server;

    public override bool CanExport(Script record) =>
        ImportContext.DataRecord is Data.Models.Game
        ||
        ImportContext.DataRecord is Data.Models.Redistributable
        ||
        ImportContext.DataRecord is Data.Models.Server;

    public override async Task<Data.Models.Script> AddAsync(Script record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}");
        
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

            if (ImportContext.DataRecord is Data.Models.Game game)
                newScript.Game = game;
            else if (ImportContext.DataRecord is Data.Models.Redistributable redistributable)
                newScript.Redistributable = redistributable;
            else if (ImportContext.DataRecord is Data.Models.Server server)
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

    public override async Task<Data.Models.Script> UpdateAsync(Script record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}");

        Data.Models.Script existing = null;
        
        if (ImportContext.DataRecord is Data.Models.Game game)
            existing = await scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.GameId == game.Id);
        else if (ImportContext.DataRecord is Data.Models.Redistributable redistributable)
            existing = await scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.RedistributableId == redistributable.Id);
        else if (ImportContext.DataRecord is Data.Models.Server server)
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

    public override async Task<Script> ExportAsync(Data.Models.Script entity)
    {
        using (var stream = new MemoryStream())
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(entity.Contents);
            writer.Flush();
            
            stream.Position = 0;
            
            ImportContext.Archive.AddEntry($"Scripts/{entity.Id}", stream);
        }
        
        return mapper.Map<Script>(entity);
    }

    public override async Task<bool> ExistsAsync(Script record)
    {
        if (ImportContext.DataRecord is Data.Models.Game game)
            return await scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.GameId == game.Id);
        
        if (ImportContext.DataRecord is Data.Models.Redistributable redistributable)
            return await scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.RedistributableId == redistributable.Id);
        
        if (ImportContext.DataRecord is Data.Models.Server server)
            return await scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.ServerId == server.Id);

        return false;
    }
}