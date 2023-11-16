using LANCommander.SDK;
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

            gameManager.Install(Id);

            stopwatch.Stop();
        }
    }
}
