using System;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    public class InstallTaskProgress
    {
        public Guid QueueItemId { get; set; }
        public Guid TaskId { get; set; }
        public InstallTaskType TaskType { get; set; }
        public string TaskTitle { get; set; }
        public InstallTaskStatus TaskStatus { get; set; }
        public float Progress { get; set; }
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public long TransferSpeed { get; set; }
        public TimeSpan TimeRemaining { get; set; }
        public bool Indeterminate { get; set; }
        public string ErrorMessage { get; set; }
    }
}
