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
        public void RunScript(string script)
        {
            var process = new Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Verb = "runas";
            process.StartInfo.Arguments = $@"-ExecutionPolicy Unrestricted -File ""{script}""";
            process.Start();
            process.WaitForExit();
        }

        public void RunInstallScript(Game game)
        {
            var scriptPath = Path.Combine(game.InstallDirectory, "_install.ps1");

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException(scriptPath);

            RunScript(scriptPath);
        }

        public void RunUninstallScript(Game game)
        {
            var scriptPath = Path.Combine(game.InstallDirectory, "_uninstall.ps1");

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException(scriptPath);

            RunScript(scriptPath);
        }
    }
}
