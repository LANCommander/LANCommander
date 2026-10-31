namespace LANCommander.Server.Services.Utilities;

/// <summary>
/// Paths among a game server's files: relative to wherever its engine keeps them, with "/" between
/// folders and "" for the top. Everything an administrator asks for goes through
/// <see cref="Normalize"/> before it reaches an engine, so no path can climb out of the server's files.
/// </summary>
public static class ServerFilePath
{
    /// <summary>
    /// The path in its one spelling: backslashes read as "/", empty and "." segments dropped, no
    /// leading or trailing "/". Throws <see cref="ArgumentException"/> for a path that climbs with
    /// "..", names a drive or stream with ":", or holds a control character.
    /// </summary>
    public static string Normalize(string? path)
    {
        if (String.IsNullOrWhiteSpace(path))
            return "";

        var segments = new List<string>();

        foreach (var segment in path.Replace('\\', '/').Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
                continue;

            if (segment == "..")
                throw new ArgumentException("The path can't leave the server's files.", nameof(path));

            if (segment.Contains(':') || segment.Any(Char.IsControl))
                throw new ArgumentException($"\"{segment}\" isn't a valid file name.", nameof(path));

            segments.Add(segment);
        }

        return String.Join('/', segments);
    }

    /// <summary>A name inside a folder, both normalized.</summary>
    public static string Combine(string? directory, string name)
    {
        var normalizedDirectory = Normalize(directory);
        var normalizedName = Normalize(name);

        return normalizedDirectory.Length == 0 ? normalizedName
            : normalizedName.Length == 0 ? normalizedDirectory
            : $"{normalizedDirectory}/{normalizedName}";
    }

    /// <summary>The folder a path is in; "" for the top.</summary>
    public static string GetDirectory(string? path)
    {
        var normalized = Normalize(path);
        var slash = normalized.LastIndexOf('/');

        return slash < 0 ? "" : normalized[..slash];
    }

    /// <summary>The last segment of a path.</summary>
    public static string GetName(string? path)
    {
        var normalized = Normalize(path);

        return normalized[(normalized.LastIndexOf('/') + 1)..];
    }
}
