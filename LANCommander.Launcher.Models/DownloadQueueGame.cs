using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public class DownloadQueueGame : IDownloadQueueItem
    {
        public Guid Id { get; set; }
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
                    case GameInstallStatus.Downloading:
                    case GameInstallStatus.InstallingRedistributables:
                    case GameInstallStatus.InstallingMods:
                    case GameInstallStatus.InstallingExpansions:
                    case GameInstallStatus.RunningScripts:
                    case GameInstallStatus.DownloadingSaves:
                        return true;

                    default:
                        return false;
                }
            }
        }
        public GameInstallStatus Status { get; set; }
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

        public DownloadQueueGame(SDK.Models.Game game)
        {
            Game = game;
            Id = game.Id;
            Title = game.Title;
            Version = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.Version;
            QueuedOn = DateTime.Now;
            Status = GameInstallStatus.Idle;

            var cover = game.Media.FirstOrDefault(m => m.Type == SDK.Enums.MediaType.Cover);

            if (cover != null)
                CoverId = cover.Id;

            var icon = game.Media.FirstOrDefault(m => m.Type == SDK.Enums.MediaType.Icon);

            if (icon != null)
                IconId = icon.Id;
        }
    }
}
