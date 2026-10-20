using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Services.ScriptDebugging;

/// <summary>
/// Runs one script on demand from the debugger window. It goes through the same
/// <see cref="ScriptClient"/> methods installs and launches use, so the script gets exactly the variables
/// it would in production and is elevated the same way; the debugger attaches because its window is
/// watching.
/// </summary>
public class ScriptDebugRunner(
    ScriptClient scriptClient,
    GameClient gameClient,
    UserService userService)
{
    /// <returns>What the script returned (an exit code, or true/false for DetectInstall).</returns>
    public async Task<object?> RunAsync(ScriptEntry entry, Guid gameId, string installDirectory)
    {
        if (!entry.CanRunDirectly)
            throw new InvalidOperationException($"{entry.Name} cannot be run on its own.");

        var ownerId = entry.Key.OwnerId;

        return entry.OwnerKind switch
        {
            ScriptOwnerKind.Game => await RunGameScriptAsync(entry.Type, installDirectory, ownerId),
            ScriptOwnerKind.Redistributable => await RunRedistributableScriptAsync(entry.Type, installDirectory, gameId, ownerId),
            ScriptOwnerKind.Tool => await RunToolScriptAsync(entry.Type, installDirectory, gameId, ownerId),
            _ => null,
        };
    }

    private async Task<object?> RunGameScriptAsync(ScriptType type, string installDirectory, Guid gameId) => type switch
    {
        ScriptType.Install => await scriptClient.Game_RunInstallScriptAsync(installDirectory, gameId),
        ScriptType.Uninstall => await scriptClient.Game_RunUninstallScriptAsync(installDirectory, gameId),
        ScriptType.BeforeStart => await scriptClient.Game_RunBeforeStartScriptAsync(installDirectory, gameId),
        ScriptType.AfterStop => await scriptClient.Game_RunAfterStopScriptAsync(installDirectory, gameId),
        ScriptType.NameChange => await scriptClient.Game_RunNameChangeScriptAsync(installDirectory, gameId, await GetPlayerAliasAsync()),
        ScriptType.KeyChange => await scriptClient.Game_RunKeyChangeScriptAsync(installDirectory, gameId, await gameClient.GetAllocatedKeyAsync(gameId)),
        _ => throw Unsupported(type),
    };

    private async Task<object?> RunRedistributableScriptAsync(ScriptType type, string installDirectory, Guid gameId, Guid redistributableId) => type switch
    {
        ScriptType.DetectInstall => await scriptClient.Redistributable_RunDetectInstallScriptAsync(installDirectory, gameId, redistributableId),
        ScriptType.Install => await scriptClient.Redistributable_RunInstallScriptAsync(installDirectory, gameId, redistributableId),
        ScriptType.Uninstall => await scriptClient.Redistributable_RunUninstallScriptAsync(installDirectory, gameId, redistributableId),
        ScriptType.BeforeStart => await scriptClient.Redistributable_RunBeforeStartScriptAsync(installDirectory, gameId, redistributableId),
        ScriptType.AfterStop => await scriptClient.Redistributable_RunAfterStopScriptAsync(installDirectory, gameId, redistributableId),
        ScriptType.NameChange => await scriptClient.Redistributable_RunNameChangeScriptAsync(installDirectory, gameId, redistributableId, await GetPlayerAliasAsync()),
        _ => throw Unsupported(type),
    };

    private async Task<object?> RunToolScriptAsync(ScriptType type, string installDirectory, Guid gameId, Guid toolId) => type switch
    {
        ScriptType.DetectInstall => await scriptClient.Tool_RunDetectInstallScriptAsync(installDirectory, gameId, toolId),
        ScriptType.Install => await scriptClient.Tool_RunInstallScriptAsync(installDirectory, toolId),
        ScriptType.Uninstall => await scriptClient.Tool_RunUninstallScriptAsync(installDirectory, toolId),
        ScriptType.BeforeStart => await scriptClient.Tool_RunBeforeStartScriptAsync(installDirectory, toolId),
        ScriptType.AfterStop => await scriptClient.Tool_RunAfterStopScriptAsync(installDirectory, toolId),
        _ => throw Unsupported(type),
    };

    private async Task<string> GetPlayerAliasAsync()
    {
        var user = await userService.GetCurrentUser();

        return user?.GetUserNameSafe ?? SDK.Models.Settings.DEFAULT_GAME_USERNAME;
    }

    private static NotSupportedException Unsupported(ScriptType type) => new($"{type} scripts cannot be run on their own.");
}
