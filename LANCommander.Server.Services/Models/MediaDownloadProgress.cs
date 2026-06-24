namespace LANCommander.Server.Services.Models
{
    public class MediaDownloadProgress
    {
        public long BytesTransferred { get; set; }
        public long? TotalBytes { get; set; }
        public string Status { get; set; } = "Downloading...";

        public double? Percent => TotalBytes > 0
            ? Math.Round((double)BytesTransferred / TotalBytes.Value * 100, 1)
            : null;
    }
}
