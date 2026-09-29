using CommandLine;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services;

public class ElevatedScriptInterceptor(
    ILogger<ElevatedScriptInterceptor> logger,
    ICurrentProcessInfo currentProcessInfo,
    IElevatedProcessLauncher processLauncher) : IScriptInterceptor
{
    public async Task<bool> ExecuteAsync(PowerShellScript script)
    {
        try
        {
            if (script.RunAsAdmin && !currentProcessInfo.IsElevated)
            {
                var manifest = script.Variables.GetValue<SDK.Models.Manifest.Game>("GameManifest");

                var options = new RunScriptCommandLineOptions
                {
                    InstallDirectory = script.Variables.GetValue<string>("InstallDirectory"),
                    GameId = manifest.Id,
                    Type = script.Type,
                    // The child cannot inherit our environment (the runas verb requires
                    // UseShellExecute, which forbids setting environment variables), so the data root
                    // is passed on the command line. Without it the child resolves its own config
                    // directory from its working directory and boots an empty profile — no server
                    // address, no token, no database — and dies before it ever runs the script.
                    DataDirectory = currentProcessInfo.ConfigDirectory,
                };

                if (script.Type == ScriptType.KeyChange)
                    options.AllocatedKey = script.Variables.GetValue<string>("AllocatedKey");

                if (script.Type == ScriptType.NameChange)
                {
                    options.OldPlayerAlias = script.Variables.GetValue<string>("OldPlayerAlias");
                    options.NewPlayerAlias = script.Variables.GetValue<string>("NewPlayerAlias");
                }

                var arguments = Parser.Default.FormatCommandLine(options);

                logger.LogInformation(
                    "Re-launching elevated to run {ScriptType} script for game {GameId}",
                    script.Type, manifest.Id);

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
                        script.Type, manifest.Id, exitCode);
                else
                    logger.LogInformation(
                        "Elevated {ScriptType} script for game {GameId} completed", script.Type, manifest.Id);

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
