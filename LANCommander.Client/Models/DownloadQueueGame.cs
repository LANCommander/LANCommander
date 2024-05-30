using LANCommander.Client.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Models
{
    internal class DownloadQueueGame : IDownloadQueueItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public string CoverPath { get; set; }
        public DateTime QueuedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public bool State {
            get
            {
                switch (Status)
                {
                    case DownloadStatus.Downloading:
                    case DownloadStatus.InstallingRedistributables:
                    case DownloadStatus.InstallingMods:
                    case DownloadStatus.InstallingExpansions:
                    case DownloadStatus.RunningScripts:
                    case DownloadStatus.DownloadingSaves:
                        return true;

                    default:
                        return false;
                }
            }
        }
        public DownloadStatus Status { get; set; }
        public SDK.Models.Game Game { get; set; }
        public float Progress {
            get
            {
                return BytesDownloaded / (float)TotalBytes;
            }
            set { }
        }
        public long TransferSpeed { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }

        public DownloadQueueGame(SDK.Models.Game game)
        {
            Game = game;
            Id = game.Id;
            Title = game.Title;
            Version = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.Version;
            QueuedOn = DateTime.Now;
            Status = DownloadStatus.Idle;
        }
    }
}
