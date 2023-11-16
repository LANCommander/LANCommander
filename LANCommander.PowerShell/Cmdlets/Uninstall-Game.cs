using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Windows.Forms;

namespace LANCommander.PowerShell.Cmdlets
{
    [Cmdlet(VerbsLifecycle.Uninstall, "Game")]
    [OutputType(typeof(string))]
    public class UninstallGameCmdlet : PSCmdlet
    {
        [Parameter(Mandatory = false)]
        public string InstallDirectory { get; set; } = "C:\\Games";

        protected override void ProcessRecord()
        {
            var scriptPath = ScriptHelper.GetScriptFilePath(InstallDirectory, SDK.Enums.ScriptType.Uninstall);

            if (!String.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
            {
                var manifest = ManifestHelper.Read(InstallDirectory);
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", InstallDirectory);
                script.AddVariable("GameManifest", manifest);

                script.UseFile(scriptPath);

                script.Execute();
            }

            var gameManager = new GameManager(null, InstallDirectory);

            gameManager.Uninstall(InstallDirectory);
        }
    }
}
