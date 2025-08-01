using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Action = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Server.ImportExport.Importers;

public class ActionImporter(
    IMapper mapper,
    ActionService actionService) : BaseImporter<Action, Data.Models.Action>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Action record) =>
        await Task.Run(() => new ImportItemInfo { Name = record.Name, Type = ImportExportRecordType.Action });

    public override bool CanImport(Action record) => ImportContext.DataRecord is Data.Models.Game;
    

    public override async Task<Data.Models.Action> AddAsync(Action record)
    {
        try
        {
            var action = new Data.Models.Action
            {
                Name = record.Name,
                Game = ImportContext.DataRecord as Data.Models.Game,
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

    public override async Task<Data.Models.Action> UpdateAsync(Action record)
    {
        var existing = await actionService.FirstOrDefaultAsync(a => a.Name == record.Name);

        try
        {
            existing.Path = record.Path;
            existing.WorkingDirectory = record.WorkingDirectory;
            existing.PrimaryAction = record.IsPrimaryAction;
            existing.SortOrder = record.SortOrder;
            existing.Game = ImportContext.DataRecord as Data.Models.Game;
            
            existing = await actionService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Action>(record, "An unknown error occured while importing action", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Action record)
    {
        return await actionService.ExistsAsync(a => a.Name == record.Name && a.GameId == ImportContext.DataRecord.Id);
    }
}