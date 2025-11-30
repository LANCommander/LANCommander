using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using CommandLine;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;

namespace LANCommander.Launcher.Services;

public class ElevatedScriptInterceptor : IScriptInterceptor
{
    public async Task<bool> ExecuteAsync(PowerShellScript script)
    {
        try
        {
            bool isElevated = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);

                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            else
            {
                isElevated = Environment.UserName == "root";
            }

            if (script.RunAsAdmin && !isElevated)
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
                var path = Process.GetCurrentProcess().MainModule!.FileName;

                var process = new Process();

                process.StartInfo.FileName = path;
                process.StartInfo.Verb = "runas";
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.WorkingDirectory = script.WorkingDirectory;
                process.StartInfo.Arguments = arguments;

                process.Start();

                await process.WaitForExitAsync();

                return true;
            }
        }
        catch (Exception ex)
        {
            // Not running as admin
        }

        return false;
    }
}