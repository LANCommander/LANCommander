namespace LANCommander.Server.Services.Models
{
    public class MediaGrabberDownload : IDisposable
    {
        public Stream Stream { get; set; }
        public string MimeType { get; set; }

        public void Dispose()
        {
            Stream?.Dispose();
        }
    }
}
