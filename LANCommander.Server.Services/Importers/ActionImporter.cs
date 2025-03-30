using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;
using Action = LANCommander.SDK.Models.Action;

namespace LANCommander.Server.Services.Importers;

public class ActionImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Action, Data.Models.Action>
{
    ActionService _actionService = serviceProvider.GetRequiredService<ActionService>();
    
    public async Task<Data.Models.Action> AddAsync(Action record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Action>(record, $"Cannot import action for a {typeof(TParentRecord).Name}");

        try
        {
            var action = new Data.Models.Action
            {
                Id = record.Id,
                Game = game,
                Path = record.Path,
                WorkingDirectory = record.WorkingDirectory,
                PrimaryAction = record.IsPrimaryAction,
                SortOrder = record.SortOrder,
            };

            action = await _actionService.AddAsync(action);

            return action;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Action>(record, "An unknown error occured while importing action", ex);
        }
    }

    public async Task<Data.Models.Action> UpdateAsync(Action record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Action>(record, $"Cannot import actions for a {typeof(TParentRecord).Name}");

        var existing = await _actionService.FirstOrDefaultAsync(p => p.Id == record.Id);

        try
        {
            existing.Path = record.Path;
            existing.WorkingDirectory = record.WorkingDirectory;
            existing.PrimaryAction = record.IsPrimaryAction;
            existing.SortOrder = record.SortOrder;
            
            existing = await _actionService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Action>(record, "An unknown error occured while importing action", ex);
        }
    }

    public async Task<bool> ExistsAsync(Action record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Action>(record, $"Cannot import actions for a {typeof(TParentRecord).Name}");
        
        return await _actionService.ExistsAsync(p => p.Id == record.Id);
    }
}