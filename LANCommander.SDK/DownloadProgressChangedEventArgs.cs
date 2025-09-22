namespace LANCommander.SDK;

public class DownloadProgressChangedEventArgs
{
    public long BytesReceived { get; set; }
    public long TotalBytes { get; set; }
}