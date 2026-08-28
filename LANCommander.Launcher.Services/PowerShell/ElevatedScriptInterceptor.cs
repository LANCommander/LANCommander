using CommandLine;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;

namespace LANCommander.Launcher.Services;

public class ElevatedScriptInterceptor(
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
                };

                if (script.Type == ScriptType.KeyChange)
                    options.AllocatedKey = script.Variables.GetValue<string>("AllocatedKey");

                if (script.Type == ScriptType.NameChange)
                {
                    options.OldPlayerAlias = script.Variables.GetValue<string>("OldPlayerAlias");
                    options.NewPlayerAlias = script.Variables.GetValue<string>("NewPlayerAlias");
                }

                var arguments = Parser.Default.FormatCommandLine(options);

                // Re-launch this launcher as a minimal, elevated process that runs just this script
                // (with all its runtime parameters) and then exits. Wait until it has finished before
                // reporting the script as handled so the caller doesn't continue prematurely.
                await processLauncher.LaunchAndWaitAsync(new ElevatedProcessRequest
                {
                    FileName = currentProcessInfo.ExecutablePath,
                    Arguments = arguments,
                    WorkingDirectory = script.WorkingDirectory,
                });

                return true;
            }
        }
        catch (Exception)
        {
            // Unable to determine elevation state or launch the elevated process; fall back to
            // running the script in-process.
        }

        return false;
    }
}
