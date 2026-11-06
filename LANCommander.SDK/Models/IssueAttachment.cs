using System;

namespace LANCommander.SDK.Models
{
    /// <summary>A file attached to an issue. Download it with <c>IssueClient.DownloadAttachmentAsync</c>.</summary>
    public class IssueAttachment
    {
        public Guid Id { get; set; }
        public Guid IssueId { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
    }
}
