using System;
using System.Collections.Generic;

namespace LANCommander.SDK.Models
{
    /// <summary>
    /// A problem reported against a game. When opening an issue only <see cref="Title"/>,
    /// <see cref="Description"/> and <see cref="GameId"/> are read; the rest is filled in by the
    /// server when issues are listed.
    /// </summary>
    public class Issue
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Guid GameId { get; set; }

        public bool IsPinned { get; set; }
        public bool IsResolved => ResolvedOn != null;
        public DateTime? ResolvedOn { get; set; }

        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; }

        public IEnumerable<IssueAttachment> Attachments { get; set; } = [];
    }
}
