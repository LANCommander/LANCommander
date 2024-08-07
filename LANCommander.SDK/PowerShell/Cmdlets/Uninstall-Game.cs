using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsLifecycle.Uninstall, "Game")]
    [OutputType(typeof(string))]
    public class UninstallGameCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true)]
        public Client Client { get; set; }

        [Parameter(Mandatory = true)]
        public string InstallDirectory { get; set; }

        [Parameter(Mandatory = true)]
        public Guid Id { get; set; }

        protected override void ProcessRecord()
        {
            var scriptPath = ScriptHelper.GetScriptFilePath(InstallDirectory, Id, SDK.Enums.ScriptType.Uninstall);

            if (!String.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
            {
                var manifest = ManifestHelper.Read(InstallDirectory, Id);
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", InstallDirectory);
                script.AddVariable("GameManifest", manifest);

                script.UseFile(scriptPath);

                script.ExecuteAsync().Wait();
            }

            // Client.Games.UninstallAsync(InstallDirectory, Id);

            var metadataPath = GameService.GetMetadataDirectoryPath(InstallDirectory, Id);

            if (Directory.Exists(metadataPath))
                Directory.Delete(metadataPath, true);

            DirectoryHelper.DeleteEmptyDirectories(InstallDirectory);
        }
    }
}
