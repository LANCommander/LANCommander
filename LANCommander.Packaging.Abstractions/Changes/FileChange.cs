namespace LANCommander.Packaging.Changes;

/// <summary>
/// A single file the monitored installer touched in a way that matters for packaging.
/// </summary>
public class FileChange
{
    /// <summary>Interposer verb, e.g. "FILE WRITE".</summary>
    public string Verb { get; set; } = string.Empty;

    /// <summary>Normalized absolute path. See PathNormalizer in LANCommander.Packaging.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Process the change was observed in, for diagnostics.</summary>
    public int ProcessId { get; set; }
}
