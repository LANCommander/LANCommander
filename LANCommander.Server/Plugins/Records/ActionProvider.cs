using LANCommander.Server.Services;
using Action = LANCommander.Server.Data.Models.Action;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the actions of games, tools and servers without exposing the database model.</summary>
internal sealed class ActionProvider(ActionService actions, RecordOwners owners) : IActionProvider
{
    static readonly RecordOwnerKind[] Owners = [RecordOwnerKind.Game, RecordOwnerKind.Tool, RecordOwnerKind.Server];

    public async Task<IReadOnlyList<ActionInfo>> GetAsync(RecordOwner owner, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Actions", Owners);

        await owners.RequireAsync(owner);

        var id = owner.Id;
        var found = owner.Kind switch
        {
            RecordOwnerKind.Game => await ForVersionAsync(owner),
            RecordOwnerKind.Tool => await actions.GetAsync(a => a.ToolId == id),
            _ => await actions.GetAsync(a => a.ServerId == id),
        };

        return found.OrderBy(a => a.SortOrder).Select(Info).ToList();

        async Task<ICollection<Action>> ForVersionAsync(RecordOwner game)
        {
            var versionId = await owners.VersionOfAsync(game, create: false);

            // Actions from before versions belong to the game alone, and apply to every version
            return await actions.GetAsync(a => a.GameId == id && (a.GameVersionId == null || a.GameVersionId == versionId));
        }
    }

    public async Task<ActionInfo?> GetRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var action = await actions.GetAsync(id);

        return action == null ? null : Info(action);
    }

    public async Task<ActionInfo> AddAsync(RecordOwner owner, ActionInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Actions", Owners);

        await owners.RequireAsync(owner);

        var entity = new Action { Id = record.Id };

        Apply(record, entity);

        switch (owner.Kind)
        {
            case RecordOwnerKind.Game:
                entity.GameId = owner.Id;
                entity.GameVersionId = await owners.VersionOfAsync(owner, create: true);
                break;
            case RecordOwnerKind.Tool:
                entity.ToolId = owner.Id;
                break;
            default:
                entity.ServerId = owner.Id;
                break;
        }

        var added = await actions.AddAsync(entity);

        return Info(RecordGuard.Found(await actions.GetAsync(added.Id), "action", added.Id));
    }

    public async Task<ActionInfo> UpdateAsync(ActionInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await actions.Include(a => a.CreatedBy!).GetAsync(record.Id), "action", record.Id);

        Apply(record, entity);

        await actions.UpdateAsync(entity);

        return Info(RecordGuard.Found(await actions.GetAsync(record.Id), "action", record.Id));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var action = await actions.GetAsync(id);

        if (action != null)
            await actions.DeleteAsync(action);
    }

    static void Apply(ActionInfo record, Action entity)
    {
        if (string.IsNullOrWhiteSpace(record.Name))
            throw new ArgumentException("An action needs a name.", nameof(record));

        entity.Name = record.Name.Trim();
        entity.Path = record.Path;
        entity.Arguments = record.Arguments;
        entity.WorkingDirectory = record.WorkingDirectory;
        entity.PrimaryAction = record.IsPrimary;
        entity.SortOrder = record.SortOrder;
        entity.Platforms = record.Platforms;
        entity.OptionOverrides = record.OptionOverrides;
    }

    static ActionInfo Info(Action action) => new()
    {
        Id = action.Id,
        Name = action.Name,
        Path = action.Path,
        Arguments = action.Arguments,
        WorkingDirectory = action.WorkingDirectory,
        IsPrimary = action.PrimaryAction,
        SortOrder = action.SortOrder,
        Platforms = action.Platforms,
        OptionOverrides = action.OptionOverrides,
        Owner = RecordOwners.Of(action.GameId, action.GameVersionId, null, action.ToolId, action.ServerId),
    };
}
