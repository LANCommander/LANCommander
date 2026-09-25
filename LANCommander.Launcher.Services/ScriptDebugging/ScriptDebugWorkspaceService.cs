using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;
using Manifest = LANCommander.SDK.Models.Manifest;

namespace LANCommander.Launcher.Services.ScriptDebugging;

/// <summary>
/// Finds every script a game can run on this machine, reads and saves their text, and publishes edits
/// back to the server. Installed games are listed from the files in their install directory, which are
/// what actually runs; games that aren't installed are listed from the server.
/// </summary>
public class ScriptDebugWorkspaceService(
    ILogger<ScriptDebugWorkspaceService> logger,
    GameService gameService,
    GameClient gameClient,
    RedistributableClient redistributableClient,
    ToolClient toolClient,
    ScriptClient scriptClient,
    IConnectionClient connectionClient)
{
    /// <summary>Script types the launcher runs. Package, login and server scripts only ever run on the server.</summary>
    public static readonly IReadOnlyList<ScriptType> LauncherScriptTypes =
    [
        ScriptType.Install,
        ScriptType.Uninstall,
        ScriptType.BeforeStart,
        ScriptType.AfterStop,
        ScriptType.NameChange,
        ScriptType.KeyChange,
        ScriptType.DetectInstall,
        ScriptType.RunWrapper,
    ];

    public async Task<ScriptWorkspace> LoadAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var game = await gameService.GetAsync(gameId);
        var title = game?.Title ?? "Game";

        if (game is { Installed: true } && !string.IsNullOrWhiteSpace(game.InstallDirectory) && ManifestHelper.Exists(game.InstallDirectory, gameId))
            return await LoadInstalledAsync(gameId, title, game.InstallDirectory);

        if (connectionClient.IsOfflineMode() || !connectionClient.IsConnected())
            return new ScriptWorkspace(gameId, title, null, false, [], "Connect to the server to view scripts for games that aren't installed.");

        return await LoadFromServerAsync(gameId, title, cancellationToken);
    }

    private async Task<ScriptWorkspace> LoadInstalledAsync(Guid gameId, string title, string installDirectory)
    {
        var owners = new List<ScriptOwnerNode>();
        var manifests = await gameClient.GetManifestsAsync(installDirectory, gameId);
        var main = manifests.FirstOrDefault(m => m.Id == gameId);

        foreach (var manifest in manifests)
            owners.Add(InstalledOwner(gameId, installDirectory, ScriptOwnerKind.Game, manifest.Id, manifest.Title, manifest.Id != gameId, manifest.Scripts));

        foreach (var redistributable in main?.Redistributables ?? [])
        {
            var manifest = await TryReadManifestAsync<Manifest.Redistributable>(installDirectory, redistributable.Id) ?? redistributable;

            owners.Add(InstalledOwner(gameId, installDirectory, ScriptOwnerKind.Redistributable, manifest.Id, manifest.Name, false, manifest.Scripts));
        }

        foreach (var tool in main?.Tools ?? [])
        {
            var manifest = await TryReadManifestAsync<Manifest.Tool>(installDirectory, tool.Id) ?? tool;

            owners.Add(InstalledOwner(gameId, installDirectory, ScriptOwnerKind.Tool, manifest.Id, manifest.Name, false, manifest.Scripts));
        }

        return new ScriptWorkspace(gameId, title, installDirectory, true, owners.Where(o => o.Scripts.Count > 0).ToList());
    }

    private ScriptOwnerNode InstalledOwner(
        Guid gameId,
        string installDirectory,
        ScriptOwnerKind kind,
        Guid ownerId,
        string name,
        bool isAddon,
        IEnumerable<Manifest.Script>? manifestScripts)
    {
        var entries = new List<ScriptEntry>();
        var listed = (manifestScripts ?? []).ToDictionary(s => s.Type, s => s);

        // The files are authoritative: a script is listed if it will run, whether or not the manifest
        // mentions it.
        foreach (var type in LauncherScriptTypes)
        {
            var path = ScriptHelper.GetScriptFilePath(installDirectory, ownerId, type);

            if (!File.Exists(path))
                continue;

            listed.TryGetValue(type, out var script);

            var key = new ScriptKey(ownerId, type);

            entries.Add(new ScriptEntry
            {
                Key = key,
                OwnerKind = kind,
                OwnerName = name,
                ServerScriptId = script?.Id,
                Name = string.IsNullOrWhiteSpace(script?.Name) ? type.ToString() : script.Name,
                RequiresAdmin = script?.RequiresAdmin ?? false,
                Source = ScriptSource.Installed,
                LocalPath = path,
                DraftPath = ScriptDrafts.GetPath(gameId, key),
            });
        }

        return new ScriptOwnerNode(kind, ownerId, name, isAddon, entries);
    }

    private async Task<ScriptWorkspace> LoadFromServerAsync(Guid gameId, string title, CancellationToken cancellationToken)
    {
        var owners = new List<ScriptOwnerNode>();
        var manifest = await gameClient.GetManifestAsync(gameId);

        if (manifest is null)
            return new ScriptWorkspace(gameId, title, null, false, [], "The server did not return this game.");

        owners.Add(ServerOwner(gameId, ScriptOwnerKind.Game, manifest.Id, manifest.Title, false, manifest.Scripts,
            await TryGetScriptsAsync(() => gameClient.GetScriptsAsync(manifest.Id))));

        foreach (var addon in manifest.Addons ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();

            owners.Add(ServerOwner(gameId, ScriptOwnerKind.Game, addon.Id, addon.Title, true, addon.Scripts,
                await TryGetScriptsAsync(() => gameClient.GetScriptsAsync(addon.Id))));
        }

        foreach (var redistributable in manifest.Redistributables ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();

            owners.Add(ServerOwner(gameId, ScriptOwnerKind.Redistributable, redistributable.Id, redistributable.Name, false, redistributable.Scripts,
                await TryGetScriptsAsync(() => redistributableClient.GetScriptsAsync(redistributable.Id))));
        }

        foreach (var tool in manifest.Tools ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();

            owners.Add(ServerOwner(gameId, ScriptOwnerKind.Tool, tool.Id, tool.Name, false, tool.Scripts,
                await TryGetScriptsAsync(() => toolClient.GetScriptsAsync(tool.Id))));
        }

        title = string.IsNullOrWhiteSpace(manifest.Title) ? title : manifest.Title;

        return new ScriptWorkspace(gameId, title, null, false, owners.Where(o => o.Scripts.Count > 0).ToList());
    }

    private ScriptOwnerNode ServerOwner(
        Guid gameId,
        ScriptOwnerKind kind,
        Guid ownerId,
        string name,
        bool isAddon,
        IEnumerable<Manifest.Script>? manifestScripts,
        IReadOnlyList<SDK.Models.Script> serverScripts)
    {
        var entries = new List<ScriptEntry>();

        // A game's script list spans every version; the manifest names the ones the version that would
        // be installed uses. Owners without a manifest script list (older servers) fall back to the
        // newest script of each type.
        var wanted = manifestScripts?.Select(s => s.Id).ToHashSet() ?? [];

        foreach (var type in LauncherScriptTypes)
        {
            var candidates = serverScripts.Where(s => s.Type == type).ToList();
            var script = candidates.FirstOrDefault(s => wanted.Contains(s.Id))
                ?? (wanted.Count == 0 ? candidates.OrderByDescending(s => s.UpdatedOn).FirstOrDefault() : null);

            if (script is null)
                continue;

            var key = new ScriptKey(ownerId, type);
            var draftPath = ScriptDrafts.GetPath(gameId, key);

            entries.Add(new ScriptEntry
            {
                Key = key,
                OwnerKind = kind,
                OwnerName = name,
                ServerScriptId = script.Id,
                Name = string.IsNullOrWhiteSpace(script.Name) ? type.ToString() : script.Name,
                RequiresAdmin = script.RequiresAdmin,
                Source = File.Exists(draftPath) ? ScriptSource.Draft : ScriptSource.Server,
                DraftPath = draftPath,
                ServerContents = script.Contents ?? string.Empty,
            });
        }

        return new ScriptOwnerNode(kind, ownerId, name, isAddon, entries);
    }

    private async Task<IReadOnlyList<SDK.Models.Script>> TryGetScriptsAsync(Func<Task<IEnumerable<SDK.Models.Script>>> fetch)
    {
        try
        {
            return (await fetch())?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch scripts from the server");
            return [];
        }
    }

    private async Task<T?> TryReadManifestAsync<T>(string installDirectory, Guid id) where T : class
    {
        try
        {
            return ManifestHelper.Exists(installDirectory, id)
                ? await ManifestHelper.ReadAsync<T>(installDirectory, id)
                : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read manifest {Id} from {InstallDirectory}", id, installDirectory);
            return null;
        }
    }

    // ---- contents ------------------------------------------------------------------------

    /// <summary>The text the editor should show: the installed file, a draft, or the server's copy.</summary>
    public async Task<string> ReadAsync(ScriptEntry entry)
    {
        if (entry.Source == ScriptSource.Installed && entry.LocalPath is not null)
            return await File.ReadAllTextAsync(entry.LocalPath);

        if (File.Exists(entry.DraftPath))
            return await File.ReadAllTextAsync(entry.DraftPath);

        return entry.ServerContents ?? string.Empty;
    }

    /// <summary>
    /// Save editor text. Installed scripts are written to the file that runs; anything else becomes a
    /// draft. Returns the entry with its new source.
    /// </summary>
    public async Task<ScriptEntry> SaveAsync(Guid gameId, ScriptEntry entry, string contents)
    {
        if (entry.Source == ScriptSource.Installed && entry.LocalPath is not null)
        {
            await File.WriteAllTextAsync(entry.LocalPath, contents);
            return entry;
        }

        await ScriptDrafts.SaveAsync(gameId, entry.Key, contents);

        return entry with { Source = ScriptSource.Draft };
    }

    /// <summary>Throw away a draft and go back to the server's copy.</summary>
    public ScriptEntry DiscardDraft(Guid gameId, ScriptEntry entry)
    {
        ScriptDrafts.Discard(gameId, entry.Key);

        return entry.Source == ScriptSource.Draft ? entry with { Source = ScriptSource.Server } : entry;
    }

    /// <summary>
    /// Drafts left over from before the game was installed. Offered to the user when the window opens on
    /// an installed game, since the install itself may have run without the debugger watching.
    /// </summary>
    public IReadOnlyList<ScriptEntry> GetPendingDrafts(ScriptWorkspace workspace) =>
        workspace.IsInstalled
            ? workspace.Scripts.Where(s => s.LocalPath is not null && File.Exists(s.DraftPath)).ToList()
            : [];

    public void ApplyDraft(Guid gameId, ScriptEntry entry)
    {
        if (entry.LocalPath is null)
            throw new InvalidOperationException("A draft can only be applied to an installed script.");

        ScriptDrafts.TryPromote(gameId, entry.Key, entry.LocalPath, entry.RequiresAdmin);
    }

    /// <summary>
    /// Publish editor text to the server. The <c>#Requires -RunAsAdministrator</c> header the launcher
    /// adds on disk is stripped, since the server stores that as a flag.
    /// </summary>
    /// <param name="baseContents">The text the edit started from, for conflict detection.</param>
    /// <exception cref="ScriptConflictException">The server's copy changed after the edit started.</exception>
    public async Task UploadAsync(ScriptEntry entry, string contents, string? baseContents)
    {
        if (entry.ServerScriptId is not { } scriptId)
            throw new InvalidOperationException("This script has no server id and cannot be uploaded.");

        await scriptClient.UpdateContentsAsync(
            scriptId,
            ScriptHelper.StripRequiresAdminHeader(contents),
            baseContents is null ? null : ScriptHelper.StripRequiresAdminHeader(baseContents));
    }
}
