namespace LANCommander.Server.Services.Models
{
    /// <summary>
    /// The result of inspecting a game archive for layouts that the launcher's forward-only
    /// (streaming) extractor cannot read reliably.
    /// </summary>
    public class ArchiveStreamingReport
    {
        /// <summary>
        /// True when no entries require a repack to be safely streamed by the launcher.
        /// </summary>
        public bool IsStreamingSafe => ProblemEntries.Count == 0;

        /// <summary>
        /// Total number of entries found in the archive's central directory.
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// Entries that cannot be reliably extracted by a streaming reader.
        /// </summary>
        public List<ArchiveStreamingProblemEntry> ProblemEntries { get; set; } = new();
    }

    public class ArchiveStreamingProblemEntry
    {
        public string Name { get; set; } = string.Empty;
        public long UncompressedSize { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
