using System;
using System.Collections.Generic;
using System.Threading;

namespace LANCommander.PlaynitePlugin.Models
{
    public enum DownloadQueueItemStatus
    {
        Idle,
        Downloading,
        InstallingRedistributables,
        RunningScripts,
        DownloadingSaves,
        Canceled
    }

    public class DownloadQueueItem : ObservableObject
    {
        public Playnite.SDK.Models.Game Game { get; set; }
        public string Title { get; set; }
        public string CoverPath { get; set; }
        public DateTime QueuedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public bool InProgress { get; set; }

        private bool progressIndeterminate { get; set; }
        public bool ProgressIndeterminate
        {
            get => progressIndeterminate;
            set
            {
                progressIndeterminate = value;
                OnPropertyChanged();
            }
        }

        private long totalDownloaded { get; set; }
        public long TotalDownloaded
        {
            get => totalDownloaded;
            set
            {
                totalDownloaded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private DownloadQueueItemStatus status { get; set; }
        public DownloadQueueItemStatus Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case DownloadQueueItemStatus.Downloading:
                        var percent = (totalDownloaded / (double)size) * 100;
                        return $"Downloading ({percent.ToString("0")}%)";
                    case DownloadQueueItemStatus.InstallingRedistributables:
                        return "Installing Redistributables";
                    case DownloadQueueItemStatus.RunningScripts:
                        return "Running Scripts";
                    case DownloadQueueItemStatus.DownloadingSaves:
                        return "Downloading Saves";
                    case DownloadQueueItemStatus.Canceled:
                        return "Canceling";
                    case DownloadQueueItemStatus.Idle:
                    default:
                        return "Idle";
                }
            }
        }

        private long size { get; set; }
        public long Size
        {
            get => size;
            set
            {
                size = value;
                OnPropertyChanged();
            }
        }

        private double speed { get; set; }
        public double Speed
        {
            get => speed;
            set
            {
                speed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SpeedText));
            }
        }
        public string SpeedText
        { 
            get
            {
                return ByteSizeLib.ByteSize.FromBytes(speed).ToString() + "/s";
            }
        }

        private string timeRemaining { get; set; }
        public string TimeRemaining
        {
            get => timeRemaining;
            set
            {
                timeRemaining = value;
                OnPropertyChanged();
            }
        }

        public CancellationToken CancellationToken { get; set; }
    }
}
