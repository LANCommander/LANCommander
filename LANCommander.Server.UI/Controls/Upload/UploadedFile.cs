namespace LANCommander.Server.UI.Controls;

public enum UploadFileStatus
{
    Uploading,
    Done,
    Failed,
}

/// <summary>A file sent by an <see cref="Upload"/> control, and how far it has got.</summary>
public sealed class UploadedFile(string id, string name, long size)
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public long Size { get; } = size;

    public UploadFileStatus Status { get; internal set; } = UploadFileStatus.Uploading;

    /// <summary>Upload progress from 0 to 100.</summary>
    public double Percent { get; internal set; }

    /// <summary>The server's response when the upload failed.</summary>
    public string? Error { get; internal set; }
}

/// <summary>Where a <see cref="ChunkUploader"/> is with its file.</summary>
public enum ChunkUploadStatus
{
    /// <summary>No upload yet; a file may have been picked.</summary>
    Idle,
    Uploading,
    Complete,
}
