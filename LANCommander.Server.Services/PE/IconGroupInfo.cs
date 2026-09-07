namespace LANCommander.Server.Services.PE;

/// <summary>
/// A single RT_GROUP_ICON resource: one logical icon made up of several images at different
/// resolutions and colour depths. This is the unit that maps one-to-one onto an ICO file.
/// </summary>
public sealed record IconGroupInfo
{
    /// <summary>Resource ID of the group. Zero when the group is keyed by <see cref="Name"/> instead.</summary>
    public int Id { get; init; }

    /// <summary>Name of the group, if the resource uses a string key rather than a numeric ID.</summary>
    public string? Name { get; init; }

    /// <summary>Locale ID the group was declared under.</summary>
    public uint Lcid { get; init; }

    /// <summary>The images belonging to this group, in the order they appear in the group header.</summary>
    public IReadOnlyList<IconInfo> Images { get; init; } = Array.Empty<IconInfo>();
}
