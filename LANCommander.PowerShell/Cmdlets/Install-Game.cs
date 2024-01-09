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
    [Cmdlet(VerbsLifecycle.Install, "Game")]
    [OutputType(typeof(string))]
    public class InstallGameCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true)]
        public Client Client { get; set; }

        [Parameter(Mandatory = true)]
        public Guid Id { get; set; }

        [Parameter(Mandatory = false)]
        public string InstallDirectory { get; set; } = "C:\\Games";

        protected override void ProcessRecord()
        {
            var game = Client.Games.Get(Id);

            var progress = new ProgressRecord(1, $"Installing {game.Title}", "Progress:");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Client.Games.OnArchiveExtractionProgress += (long position, long length) =>
            {
                // Only update a max of every 500ms
                if (stopwatch.ElapsedMilliseconds > 500)
                {
                    progress.PercentComplete = (int)Math.Ceiling((position / (decimal)length) * 100);

                    WriteProgress(progress);

                    stopwatch.Restart();
                }
            };

            var installDirectory = Client.Games.Install(Id);

            stopwatch.Stop();

            RunInstallScript(installDirectory);
            RunNameChangeScript(installDirectory);
            RunKeyChangeScript(installDirectory);

            WriteObject(installDirectory);
        }

        private int RunInstallScript(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory, Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, Id, SDK.Enums.ScriptType.Install);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", InstallDirectory);
                script.AddVariable("ServerAddress", Client.BaseUrl);

                script.UseFile(ScriptHelper.GetScriptFilePath(installDirectory, Id, SDK.Enums.ScriptType.Install));

                return script.Execute();
            }

            return 0;
        }

        private int RunNameChangeScript(string installDirectory)
        {
            var user = Client.Profile.Get();
            var manifest = ManifestHelper.Read(installDirectory, Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, Id, SDK.Enums.ScriptType.NameChange);
            
            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", InstallDirectory);
                script.AddVariable("ServerAddress", Client.BaseUrl);
                script.AddVariable("OldPlayerAlias", "");
                script.AddVariable("NewPlayerAlias", user.UserName);

                script.UseFile(path);

                return script.Execute();
            }

            return 0;
        }

        private int RunKeyChangeScript(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory, Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, Id, SDK.Enums.ScriptType.KeyChange);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                var key = Client.Games.GetAllocatedKey(manifest.Id);

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", InstallDirectory);
                script.AddVariable("ServerAddress", Client.BaseUrl);
                script.AddVariable("AllocatedKey", key);

                script.UseFile(path);

                return script.Execute();
            }

            return 0;
        }
    }
}
