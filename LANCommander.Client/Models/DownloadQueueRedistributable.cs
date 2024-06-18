using LANCommander.Client.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Models
{
    public class DownloadQueueRedistributable : IDownloadQueueItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public Guid CoverId { get; set; }
        public Guid IconId { get; set; }
        public DateTime QueuedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public bool IsUpdate { get; set; }
        public bool State
        {
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
        public SDK.Models.Redistributable Redistributable { get; set; }
        public float Progress { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double TransferSpeed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public long BytesDownloaded { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public long TotalBytes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public DownloadQueueRedistributable(SDK.Models.Redistributable redistributable)
        {
            Redistributable = redistributable;
            Id = redistributable.Id;
            Title = redistributable.Name;
            Version = redistributable.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.Version;
        }
    }
}
