namespace LANCommander.Server.UI.Controls;

/// <summary>What <see cref="ArchiveUploadDialog"/> uploads an archive for.</summary>
public sealed class ArchiveUploadOptions
{
    /// <summary>Replace the file of this existing archive instead of adding a new one.</summary>
    public Guid? ArchiveId { get; init; }

    public Guid? GameId { get; init; }

    /// <summary>Preselects this version of the game as the one the upload belongs to.</summary>
    public Guid? GameVersionId { get; init; }

    public Guid? RedistributableId { get; init; }

    public Guid? ToolId { get; init; }

    /// <summary>
    /// Called with the archive's Id once its file is stored, which can be after the dialog has closed.
    /// </summary>
    public Func<Guid, Task>? OnUploaded { get; init; }
}
