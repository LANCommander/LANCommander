namespace LANCommander.SDK.Models
{
    public class CheckForUpdateResponse
    {
        public bool UpdateAvailable { get; set; }
        public string Version { get; set; }
        public string DownloadUrl { get; set; }

        /// <summary>The name of the release file, e.g. LANCommander.Launcher-Windows-x64-v2.3.0.zip.</summary>
        public string FileName { get; set; }

        /// <summary>Lowercase hex SHA-256 of the file, when the server hosts it. Empty when it redirects to GitHub.</summary>
        public string Sha256 { get; set; }

        public long? Size { get; set; }
    }
}
