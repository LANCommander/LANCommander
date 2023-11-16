using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using System;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Windows.Forms;

namespace LANCommander.PowerShell.Cmdlets
{
    [Cmdlet(VerbsLifecycle.Install, "PrimaryDisplay")]
    [OutputType(typeof(string))]
    public class InstallGameCmdlet : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public Client Client { get; set; }

        [Parameter(Mandatory = true)]
        public Guid Id { get; set; }

        [Parameter(Mandatory = false)]
        public string InstallDirectory { get; set; } = "C:\\Games";

        protected override void ProcessRecord()
        {
            var gameManager = new GameManager(Client, InstallDirectory);
            var game = Client.GetGame(Id);

            var progress = new ProgressRecord(1, $"Installing {game.Title}", "Progress:");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            gameManager.OnArchiveExtractionProgress += (long position, long length) =>
            {
                // Only update a max of every 500ms
                if (stopwatch.ElapsedMilliseconds > 500)
                {
                    progress.PercentComplete = (int)Math.Ceiling((position / (decimal)length) * 100);

                    WriteProgress(progress);

                    stopwatch.Restart();
                }
            };

            var installDirectory = gameManager.Install(Id);

            stopwatch.Stop();

            RunInstallScript(installDirectory);
            RunNameChangeScript(installDirectory);
            RunKeyChangeScript(installDirectory);
        }

        private int RunInstallScript(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory);
            var script = new PowerShellScript();

            script.AddVariable("InstallDirectory", installDirectory);
            script.AddVariable("GameManifest", manifest);
            script.AddVariable("DefaultInstallDirectory", InstallDirectory);
            script.AddVariable("ServerAddress", Client.BaseUrl);

            script.UseFile(ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.Install));

            return script.Execute();
        }

        private int RunNameChangeScript(string installDirectory)
        {
            var user = Client.GetProfile();
            var manifest = ManifestHelper.Read(installDirectory);
            var script = new PowerShellScript();

            script.AddVariable("InstallDirectory", installDirectory);
            script.AddVariable("GameManifest", manifest);
            script.AddVariable("DefaultInstallDirectory", InstallDirectory);
            script.AddVariable("ServerAddress", Client.BaseUrl);
            script.AddVariable("OldPlayerAlias", "");
            script.AddVariable("NewPlayerAlias", user.UserName);

            script.UseFile(ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.NameChange));

            return script.Execute();
        }

        private int RunKeyChangeScript(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory);
            var script = new PowerShellScript();

            var key = Client.GetAllocatedKey(manifest.Id);

            script.AddVariable("InstallDirectory", installDirectory);
            script.AddVariable("GameManifest", manifest);
            script.AddVariable("DefaultInstallDirectory", InstallDirectory);
            script.AddVariable("ServerAddress", Client.BaseUrl);
            script.AddVariable("AllocatedKey", key);

            script.UseFile(ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.KeyChange));

            return script.Execute();
        }
    }
}
