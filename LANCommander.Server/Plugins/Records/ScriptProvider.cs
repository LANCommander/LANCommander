using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins.Records;

/// <summary>
/// Gives plugins the scripts of games, redistributables, tools and servers without exposing the
/// database model. Package scripts belong to the server and are left out.
/// </summary>
internal sealed class ScriptProvider(ScriptService scripts, RecordOwners owners) : IScriptProvider
{
    static readonly RecordOwnerKind[] Owners =
        [RecordOwnerKind.Game, RecordOwnerKind.Redistributable, RecordOwnerKind.Tool, RecordOwnerKind.Server];

    public async Task<IReadOnlyList<ScriptInfo>> GetAsync(RecordOwner owner, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Scripts", Owners);

        await owners.RequireAsync(owner);

        var id = owner.Id;
        var found = owner.Kind switch
        {
            RecordOwnerKind.Game => await ForVersionAsync(owner),
            RecordOwnerKind.Redistributable => await scripts.GetAsync(s => s.RedistributableId == id && s.Type != ScriptType.Package),
            RecordOwnerKind.Tool => await scripts.GetAsync(s => s.ToolId == id && s.Type != ScriptType.Package),
            _ => await scripts.GetAsync(s => s.ServerId == id && s.Type != ScriptType.Package),
        };

        return found.OrderBy(s => s.Type).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(Info).ToList();

        async Task<ICollection<Script>> ForVersionAsync(RecordOwner game)
        {
            var versionId = await owners.VersionOfAsync(game, create: false);

            // Scripts from before versions belong to the game alone, and apply to every version
            return await scripts.GetAsync(s => s.GameId == id
                                               && (s.GameVersionId == null || s.GameVersionId == versionId)
                                               && s.Type != ScriptType.Package);
        }
    }

    public async Task<ScriptInfo?> GetRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var script = await scripts.GetAsync(id);

        return script == null || script.Type == ScriptType.Package ? null : Info(script);
    }

    public async Task<ScriptInfo> AddAsync(RecordOwner owner, ScriptInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Scripts", Owners);

        await owners.RequireAsync(owner);

        var entity = new Script { Id = record.Id };

        Apply(record, entity);

        switch (owner.Kind)
        {
            case RecordOwnerKind.Game:
                entity.GameId = owner.Id;
                entity.GameVersionId = await owners.VersionOfAsync(owner, create: true);
                break;
            case RecordOwnerKind.Redistributable:
                entity.RedistributableId = owner.Id;
                break;
            case RecordOwnerKind.Tool:
                entity.ToolId = owner.Id;
                break;
            default:
                entity.ServerId = owner.Id;
                break;
        }

        var added = await scripts.AddAsync(entity);

        return Info(RecordGuard.Found(await scripts.GetAsync(added.Id), "script", added.Id));
    }

    public async Task<ScriptInfo> UpdateAsync(ScriptInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await scripts.Include(s => s.CreatedBy!).GetAsync(record.Id);

        if (entity == null || entity.Type == ScriptType.Package)
            throw RecordGuard.NotFound("script", record.Id);

        Apply(record, entity);

        await scripts.UpdateAsync(entity);

        return Info(RecordGuard.Found(await scripts.GetAsync(record.Id), "script", record.Id));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var script = await scripts.GetAsync(id);

        if (script != null && script.Type != ScriptType.Package)
            await scripts.DeleteAsync(script);
    }

    static void Apply(ScriptInfo record, Script entity)
    {
        if (string.IsNullOrWhiteSpace(record.Name))
            throw new ArgumentException("A script needs a name.", nameof(record));

        if (record.Type == ScriptType.Package)
            throw new ArgumentException("Package scripts belong to the server.", nameof(record));

        entity.Name = record.Name.Trim();
        entity.Description = record.Description;
        entity.Type = record.Type;
        entity.Contents = record.Contents ?? "";
        entity.RequiresAdmin = record.RequiresAdmin;
        entity.Platforms = record.Platforms;
    }

    static ScriptInfo Info(Script script) => new()
    {
        Id = script.Id,
        Name = script.Name,
        Description = script.Description,
        Type = script.Type,
        Contents = script.Contents,
        RequiresAdmin = script.RequiresAdmin,
        Platforms = script.Platforms,
        Owner = RecordOwners.Of(script.GameId, script.GameVersionId, script.RedistributableId, script.ToolId, script.ServerId),
    };
}
