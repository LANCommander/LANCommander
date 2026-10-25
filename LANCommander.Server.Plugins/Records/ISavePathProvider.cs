using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>A file, folder or registry path where a game keeps its saves.</summary>
public sealed record SavePathInfo
{
    public Guid Id { get; init; }
    public SavePathType Type { get; init; }
    public string Path { get; init; } = "";
    public string? WorkingDirectory { get; init; }

    /// <summary>Whether <see cref="Path"/> is a regular expression.</summary>
    public bool IsRegex { get; init; }

    /// <summary>The platforms the path applies to.</summary>
    public RuntimePlatform Platforms { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public RecordOwner Owner { get; init; }
}

/// <summary>
/// Manages the save paths of games, per version. Provided by the server; inject it into a scoped
/// plugin service or page.
/// </summary>
public interface ISavePathProvider : IOwnedRecordProvider<SavePathInfo>;
