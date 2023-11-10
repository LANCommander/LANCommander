using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK
{
    internal class PowerShellRuntime
    {
        public static readonly ILogger Logger;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Wow64RevertWow64FsRedirection(ref IntPtr ptr);

        public void RunCommand(string command, bool asAdmin = false)
        {
            Logger.LogTrace($"Executing command `{command}` | Admin: {asAdmin}");

            var tempScript = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".ps1");

            Logger.LogTrace($"Creating temp script at path {tempScript}");

            File.WriteAllText(tempScript, command);

            RunScript(tempScript, asAdmin);

            File.Delete(tempScript);
        }

        public int RunScript(string path, bool asAdmin = false, string arguments = null, string workingDirectory = null)
        {
            Logger.LogTrace($"Executing script at path {path} | Admin: {asAdmin} | Arguments: {arguments}");

            var wow64Value = IntPtr.Zero;

            // Disable Wow64 redirection so we can hit areas of the registry absolutely
            Wow64DisableWow64FsRedirection(ref wow64Value);

            var process = new Process();

            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = $@"-ExecutionPolicy Unrestricted -File ""{path}""";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = false;

            if (arguments != null)
                process.StartInfo.Arguments += " " + arguments;

            if (workingDirectory != null)
                process.StartInfo.WorkingDirectory = workingDirectory;

            if (asAdmin)
            {
                process.StartInfo.Verb = "runas";
                process.StartInfo.UseShellExecute = true;
            }

            process.Start();
            process.WaitForExit();

            Wow64RevertWow64FsRedirection(ref wow64Value);

            return process.ExitCode;
        }

        public void RunScript(Game game, ScriptType type, string arguments = null)
        {
            var path = GetScriptFilePath(game, type);

            if (File.Exists(path))
            {
                var contents = File.ReadAllText(path);

                if (contents.StartsWith("# Requires Admin"))
                    RunScript(path, true, arguments);
                else
                    RunScript(path, false, arguments);
            }
        }

        public void RunScriptsAsAdmin(IEnumerable<string> paths, string arguments = null)
        {
            // Concatenate scripts
            var sb = new StringBuilder();

            Logger.LogTrace("Concatenating scripts...");

            foreach (var path in paths)
            {
                var contents = File.ReadAllText(path);

                sb.AppendLine(contents);

                Logger.LogTrace($"Added {path}!");
            }

            Logger.LogTrace("Done concatenating!");

            if (sb.Length > 0)
            {
                var scriptPath = Path.GetTempFileName();

                Logger.LogTrace($"Creating temp script at path {scriptPath}");

                File.WriteAllText(scriptPath, sb.ToString());

                RunScript(scriptPath, true, arguments);
            }
        }

        public void RunScripts(IEnumerable<Game> games, ScriptType type, string arguments = null)
        {
            List<string> scripts = new List<string>();
            List<string> adminScripts = new List<string>();

            foreach (var game in games)
            {
                var path = GetScriptFilePath(game, type);

                if (!File.Exists(path))
                    continue;

                var contents = File.ReadAllText(path);

                if (contents.StartsWith("# Requires Admin"))
                    adminScripts.Add(path);
                else
                    scripts.Add(path);
            }

            RunScriptsAsAdmin(adminScripts, arguments);

            foreach (var script in scripts)
            {
                RunScript(script, false, arguments);
            }
        }

        public static string GetScriptFilePath(Game game, ScriptType type)
        {
            Dictionary<ScriptType, string> filenames = new Dictionary<ScriptType, string>() {
                { ScriptType.Install, "_install.ps1" },
                { ScriptType.Uninstall, "_uninstall.ps1" },
                { ScriptType.NameChange, "_changename.ps1" },
                { ScriptType.KeyChange, "_changekey.ps1" }
            };

            var filename = filenames[type];

            return Path.Combine(game.InstallDirectory, filename);
        }
    }
}
