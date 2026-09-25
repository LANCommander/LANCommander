using CommandLine;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Debugging;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services;

public class ElevatedScriptInterceptor(
    ILogger<ElevatedScriptInterceptor> logger,
    ICurrentProcessInfo currentProcessInfo,
    IElevatedProcessLauncher processLauncher,
    IScriptDebugBroker? debugBroker = null) : IScriptInterceptor
{
    public async Task<bool> ExecuteAsync(PowerShellScript script)
    {
        try
        {
            if (script.RunAsAdmin && !currentProcessInfo.IsElevated)
            {
                // These report a result (installed? / did the wrapper launch?) or must be cancellable
                // mid-run, and neither can cross a process boundary. They run in-process, as they
                // effectively always have.
                if (script.Type is ScriptType.DetectInstall or ScriptType.RunWrapper)
                {
                    logger.LogWarning(
                        "{ScriptType} scripts cannot be run elevated; running it in-process. Anything requiring administrator rights will fail",
                        script.Type);

                    return false;
                }

                var identity = script.Identity;
                var manifest = script.Variables.FirstOrDefault(v => v.Name == "GameManifest")?.Value as SDK.Models.Manifest.Game;

                var options = new RunScriptCommandLineOptions
                {
                    InstallDirectory = identity?.InstallDirectory ?? script.Variables.GetValue<string>("InstallDirectory"),
                    GameId = identity?.GameId ?? manifest?.Id ?? Guid.Empty,
                    Type = script.Type,
                    // The child cannot inherit our environment (the runas verb requires
                    // UseShellExecute, which forbids setting environment variables), so the data root
                    // is passed on the command line. Without it the child resolves its own config
                    // directory from its working directory and boots an empty profile — no server
                    // address, no token, no database — and dies before it ever runs the script.
                    DataDirectory = currentProcessInfo.ConfigDirectory,
                };

                // Without these the child would run the game's script of the same type instead.
                if (identity?.OwnerKind == ScriptOwnerKind.Redistributable)
                    options.RedistributableId = identity.Key.OwnerId;
                else if (identity?.OwnerKind == ScriptOwnerKind.Tool)
                    options.ToolId = identity.Key.OwnerId;

                if (script.Type == ScriptType.KeyChange)
                    options.AllocatedKey = script.Variables.GetValue<string>("AllocatedKey");

                if (script.Type == ScriptType.NameChange)
                {
                    options.OldPlayerAlias = script.Variables.GetValue<string>("OldPlayerAlias");
                    options.NewPlayerAlias = script.Variables.GetValue<string>("NewPlayerAlias");
                }

                // When the script debugger is watching this script, the child attaches to it over a pipe.
                if (identity is not null && debugBroker?.GetRemoteEndpoint(identity) is { } endpoint)
                {
                    options.DebugPipe = endpoint.PipeName;
                    options.DebugToken = endpoint.Token;
                }

                var arguments = Parser.Default.FormatCommandLine(options);

                logger.LogInformation(
                    "Re-launching elevated to run {ScriptType} script for game {GameId}",
                    script.Type, options.GameId);

                // Re-launch this launcher as a minimal, elevated process that runs just this script
                // (with all its runtime parameters) and then exits. Wait until it has finished before
                // reporting the script as handled so the caller doesn't continue prematurely.
                //
                // The child deliberately does NOT inherit the script's working directory: config
                // directory resolution keys off the current directory, so handing it the game's
                // install folder would point it at a different data root than ours.
                var exitCode = await processLauncher.LaunchAndWaitAsync(new ElevatedProcessRequest
                {
                    FileName = currentProcessInfo.ExecutablePath,
                    Arguments = arguments,
                    WorkingDirectory = currentProcessInfo.WorkingDirectory,
                });

                if (exitCode != 0)
                    logger.LogError(
                        "Elevated {ScriptType} script for game {GameId} exited with code {ExitCode}; the script may not have run",
                        script.Type, options.GameId, exitCode);
                else
                    logger.LogInformation(
                        "Elevated {ScriptType} script for game {GameId} completed", script.Type, options.GameId);

                return true;
            }
        }
        catch (Exception ex)
        {
            // Unable to determine elevation state or launch the elevated process; fall back to
            // running the script in-process.
            logger.LogError(ex, "Could not run {ScriptType} script elevated; falling back to in-process execution", script.Type);
        }

        return false;
    }
}
