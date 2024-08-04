using LANCommander.Launcher.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public interface IDownloadQueueItem
    {
        Guid Id { get; set; }
        string Title { get; set; }
        string Version { get; set; }
        Guid CoverId { get; set; }
        Guid IconId { get; set; }
        DateTime QueuedOn { get; set; }
        DateTime? CompletedOn { get; set; }
        bool IsUpdate { get; set; }
        bool State { get; }
        DownloadStatus Status { get; set; }
        float Progress { get; set; }
        double TransferSpeed { get; set; }
        long BytesDownloaded { get; set; }
        long TotalBytes { get; set; }
    }
}
