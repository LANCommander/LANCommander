using LANCommander.SDK.Enums;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin
{
    internal class PowerShellRuntime
    {
        public void RunScript(string path)
        {
            var process = new Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = $@"-File ""{path}""";
            process.Start();
            process.WaitForExit();
        }

        public void RunScriptAsAdmin(string path)
        {
            var process = new Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Verb = "runas";
            process.StartInfo.Arguments = $@"-ExecutionPolicy Unrestricted -File ""{path}""";
            process.Start();
            process.WaitForExit();
        }

        public void RunScript(Game game, ScriptType type)
        {
            var path = GetScriptFilePath(game, type);

            if (File.Exists(path))
            {
                var contents = File.ReadAllText(path);

                if (contents.StartsWith("# Requires Admin"))
                    RunScriptAsAdmin(path);
                else
                    RunScript(path);
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
