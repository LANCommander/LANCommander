using System;
using System.Collections.Generic;
using System.Threading;

namespace LANCommander.PlaynitePlugin.Models
{
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
            }
        }
        public long Size { get; set; }
        public double Speed { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }
}
