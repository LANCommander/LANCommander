namespace LANCommander.Packaging.Changes;

/// <summary>
/// Decides which hook events are worth keeping.
/// <para>
/// Applied in the worker, before anything crosses the pipe. An installer generates a very
/// large number of file events and the overwhelming majority are reads of files it did not
/// create; filtering at the source is what keeps the IPC channel from being the bottleneck.
/// </para>
/// </summary>
public class ChangeFilter
{
    /// <summary>Verbs that indicate the installer produced or modified a file.</summary>
    public static readonly string[] DefaultWriteVerbs =
    [
        "FILE WRITE",
        "FILE R/W",
        "FILE COPY",
        "FILE MOVE",
    ];

    /// <summary>Verbs that indicate the installer created a key or wrote a value.</summary>
    public static readonly string[] DefaultRegistryWriteVerbs =
    [
        "REG WRITE",
        "REG CREATE",
    ];

    public IReadOnlyList<string> WriteVerbs { get; set; } = DefaultWriteVerbs;

    public IReadOnlyList<string> RegistryWriteVerbs { get; set; } = DefaultRegistryWriteVerbs;

    /// <summary>
    /// Paths under these prefixes are discarded. Installers scribble constantly in the Windows
    /// directory and in temp, and none of it belongs in a game package.
    /// </summary>
    public IReadOnlyList<string> IgnoredPathPrefixes { get; set; } = [];

    /// <summary>
    /// When true, read events are forwarded too. Diagnostics only — this is a firehose.
    /// </summary>
    public bool IncludeReads { get; set; }

    /// <summary>
    /// Builds the default set of ignored prefixes for the machine the worker is running on.
    /// </summary>
    public static string[] BuildDefaultIgnoredPathPrefixes() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Temp",
    ];

    public bool IsWriteVerb(string? verb) =>
        verb != null && WriteVerbs.Any(v => verb.Equals(v, StringComparison.OrdinalIgnoreCase));

    public bool IsRegistryWriteVerb(string? verb) =>
        verb != null && RegistryWriteVerbs.Any(v => verb.Equals(v, StringComparison.OrdinalIgnoreCase));

    public bool IsIgnoredPath(string? path) =>
        path != null && IgnoredPathPrefixes.Any(p =>
            !string.IsNullOrEmpty(p) && path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when a file event should be forwarded to the launcher.
    /// </summary>
    public bool ShouldKeepFile(string? verb, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (!IncludeReads && !IsWriteVerb(verb))
            return false;

        return !IsIgnoredPath(path);
    }

    /// <summary>
    /// True when a registry event should be forwarded to the launcher.
    /// </summary>
    public bool ShouldKeepRegistry(string? verb, string? keyPath)
    {
        if (string.IsNullOrEmpty(keyPath))
            return false;

        return IncludeReads || IsRegistryWriteVerb(verb);
    }
}
