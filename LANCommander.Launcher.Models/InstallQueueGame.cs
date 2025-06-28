using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public class InstallQueueGame : IInstallQueueItem
    {
        public Guid Id { get; set; }
        public Guid[] AddonIds { get; set; }
        public Dictionary<Guid, string?> AddonVersions { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public string InstallDirectory { get; set; }
        public Guid CoverId { get; set; }
        public Guid IconId { get; set; }
        public DateTime QueuedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public bool IsUpdate { get; set; }
        public bool State {
            get
            {
                switch (Status)
                {
                    case InstallStatus.Starting:
                    case InstallStatus.Moving:
                    case InstallStatus.Downloading:
                    case InstallStatus.InstallingRedistributables:
                    case InstallStatus.InstallingMods:
                    case InstallStatus.InstallingExpansions:
                    case InstallStatus.InstallingAddons:
                    case InstallStatus.RunningScripts:
                    case InstallStatus.DownloadingSaves:
                        return true;

                    default:
                        return false;
                }
            }
        }
        public InstallStatus Status { get; set; }
        public SDK.Models.Game Game { get; set; }
        public float Progress {
            get
            {
                return BytesDownloaded / (float)TotalBytes;
            }
            set { }
        }
        public double TransferSpeed { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }

        public InstallQueueGame(SDK.Models.Game game)
        {
            Game = game;
            Id = game.Id;
            Title = game.Title;
            Version = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.Version;
            QueuedOn = DateTime.Now;
            Status = InstallStatus.Queued;

            var cover = game.Media.FirstOrDefault(m => m.Type == SDK.Enums.MediaType.Cover);

            if (cover != null)
                CoverId = cover.Id;

            var icon = game.Media.FirstOrDefault(m => m.Type == SDK.Enums.MediaType.Icon);

            if (icon != null)
                IconId = icon.Id;
        }
    }
}
