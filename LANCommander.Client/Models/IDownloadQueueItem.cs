using LANCommander.Client.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Models
{
    public interface IDownloadQueueItem
    {
        Guid Id { get; set; }
        string Title { get; set; }
        string Version { get; set; }
        string CoverPath { get; set; }
        DateTime QueuedOn { get; set; }
        DateTime? CompletedOn { get; set; }
        bool IsUpdate { get; set; }
        bool State { get; }
        DownloadStatus Status { get; set; }
        float Progress { get; set; }
        long TransferSpeed { get; set; }
        long BytesDownloaded { get; set; }
        long TotalBytes { get; set; }
    }
}
