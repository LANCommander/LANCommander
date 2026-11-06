namespace LANCommander.SDK.Models
{
    /// <summary>
    /// How many files may be attached to an issue and how large each may be. Set by the server's
    /// administrators; get them with <c>IssueClient.GetLimitsAsync</c>.
    /// </summary>
    public class IssueAttachmentLimits
    {
        /// <summary>Files per issue. Zero means attachments are turned off.</summary>
        public int MaxFiles { get; set; } = DefaultMaxFiles;

        /// <summary>Bytes per file.</summary>
        public long MaxFileSize { get; set; } = DefaultMaxFileSize;

        public bool Enabled => MaxFiles > 0;

        public const int DefaultMaxFiles = 5;
        public const long DefaultMaxFileSize = 25L * 1024 * 1024;

        public static IssueAttachmentLimits Default => new();
    }
}
