namespace LANCommander.UI.Services;

public enum UploadType
{
    Archive,
    Import,
}

public enum UploadStatus
{
    Uploading,
    Complete,
    Error,
}

public class BackgroundUploadInfo
{
    public string UploadId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Percent { get; set; }
    public double Speed { get; set; }
    public UploadType Type { get; set; }
    public UploadStatus Status { get; set; } = UploadStatus.Uploading;
    public string? ErrorMessage { get; set; }
    public string? CompletedObjectKey { get; set; }
    public Func<string, Task>? OnCompleted { get; set; }
}
