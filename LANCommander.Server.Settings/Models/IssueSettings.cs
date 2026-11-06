namespace LANCommander.Server.Settings.Models;

public class IssueSettings
{
    /// <summary>Files a user may attach to one issue. Zero turns attachments off.</summary>
    public int MaxAttachments { get; set; } = 5;

    /// <summary>Largest attachment, in megabytes.</summary>
    public int MaxAttachmentSize { get; set; } = 25;
}
