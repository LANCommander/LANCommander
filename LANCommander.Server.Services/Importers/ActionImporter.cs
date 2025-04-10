using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;
using Action = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Server.Services.Importers;

public class ActionImporter<TParentRecord>(
    ActionService actionService,
    ImportContext<TParentRecord> importContext) : IImporter<Action, Data.Models.Action> where TParentRecord : Data.Models.BaseModel
{
    public async Task<ImportItemInfo> InfoAsync(Action record) =>
        await Task.Run(() => new ImportItemInfo { Name = record.Name });

    public bool CanImport(Action record) => importContext.Record is Data.Models.Game;

    public async Task<Data.Models.Action> AddAsync(Action record)
    {
        try
        {
            var action = new Data.Models.Action
            {
                Game = importContext.Record as Data.Models.Game,
                Path = record.Path,
                WorkingDirectory = record.WorkingDirectory,
                PrimaryAction = record.IsPrimaryAction,
                SortOrder = record.SortOrder,
            };

            action = await actionService.AddAsync(action);

            return action;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Action>(record, "An unknown error occured while importing action", ex);
        }
    }

    public async Task<Data.Models.Action> UpdateAsync(Action record)
    {
        var existing = await actionService.FirstOrDefaultAsync(a => a.Name == record.Name);

        try
        {
            existing.Path = record.Path;
            existing.WorkingDirectory = record.WorkingDirectory;
            existing.PrimaryAction = record.IsPrimaryAction;
            existing.SortOrder = record.SortOrder;
            existing.Game = importContext.Record as Data.Models.Game;
            
            existing = await actionService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Action>(record, "An unknown error occured while importing action", ex);
        }
    }

    public async Task<bool> ExistsAsync(Action record)
    {
        return await actionService.ExistsAsync(a => a.Name == record.Name && a.GameId == importContext.Record.Id);
    }
}