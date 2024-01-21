using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin.Models
{
    public class DownloadQueueItem
    {
        public Guid GameId { get; set; }
        public SDK.Models.Game Game { get; set; }
        public Playnite.SDK.Models.Game PlayniteGame { get; set; }
        public string Title { get; set; }
        public DateTime QueuedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public bool InProgress { get; set; }

        public long TotalDownloaded { get; set; }
        public decimal Progress { get; set; }
        public double Speed { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }
}
