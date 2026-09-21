namespace LANCommander.Server.Services.PE;

/// <summary>
/// Metadata describing a single icon image found within a PE binary resource.
/// Pass this to <see cref="PEIconExtractor.GetIcon"/> to decode the image.
/// </summary>
public sealed record IconInfo
{
    /// <summary>Resource ID of the RT_GROUP_ICON entry that owns this image.</summary>
    public int GroupId { get; init; }

    /// <summary>Name of the icon group, if the resource uses a string key rather than a numeric ID.</summary>
    public string? GroupName { get; init; }

    /// <summary>Resource ID of the RT_ICON entry for this image (used internally to locate the raw data).</summary>
    public int ImageId { get; init; }

    /// <summary>Width in pixels. 256 is returned when the underlying value is 0 (the PE convention for 256x256).</summary>
    public int Width { get; init; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Bits per pixel as declared in the group header (e.g. 32, 24, 8, 4, 1).</summary>
    public int BitCount { get; init; }

    /// <summary>True when the raw data is a PNG stream (Vista+ high-resolution icons).</summary>
    public bool IsPng { get; init; }
}
