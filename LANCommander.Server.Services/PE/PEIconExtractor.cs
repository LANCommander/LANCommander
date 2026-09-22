using AsmResolver;
using AsmResolver.PE;
using AsmResolver.PE.Win32Resources.Icon;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LANCommander.Server.Services.PE;

/// <summary>
/// Extracts icons from a Windows Portable Executable (PE) file such as a .exe or .dll.
/// </summary>
/// <remarks>
/// Icons in PE files are stored as two resource types:
/// <list type="bullet">
///   <item><term>RT_GROUP_ICON (14)</term><description>A header that groups related icon images (different sizes/depths) together.</description></item>
///   <item><term>RT_ICON (3)</term><description>The raw pixel data for one icon image, either a DIB (Device Independent Bitmap) or a PNG stream.</description></item>
/// </list>
/// One RT_GROUP_ICON plus the RT_ICON resources it references is exactly the content of an ICO file, which is
/// what <see cref="ExtractPrimaryIcon"/> produces.
/// </remarks>
public sealed class PEIconExtractor : IDisposable
{
    private readonly byte[] _data;
    private bool _disposed;
    private bool _parsed;

    private List<GroupBinding> _groups = new();

    // RT_ICON resource ID -> raw image data (DIB or PNG)
    private Dictionary<int, byte[]> _images = new();

    /// <summary>Opens a PE file at <paramref name="filePath"/> for icon extraction.</summary>
    public PEIconExtractor(string filePath)
    {
        _data = File.ReadAllBytes(filePath);
    }

    public PEIconExtractor(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        _data = buffer.ToArray();
    }

    public PEIconExtractor(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
    }

    // -------------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true when <paramref name="data"/> starts with the "MZ" DOS signature every PE binary carries.
    /// </summary>
    public static bool HasExecutableHeader(ReadOnlySpan<byte> data) =>
        data.Length >= 2 && data[0] == 0x4D && data[1] == 0x5A;

    public static async Task<PEIconExtractor> FromStreamAsync(Stream stream, long maxBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new InvalidDataException($"Executable is larger than the {maxBytes} byte limit.");

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return new PEIconExtractor(buffer.ToArray());
    }

    /// <summary>
    /// Returns every icon group in the binary. Groups are ordered the way Windows itself indexes them:
    /// named groups first, then numeric groups by ascending resource ID.
    /// </summary>
    public IReadOnlyList<IconGroupInfo> GetIconGroups()
    {
        EnsureParsed();

        return _groups.Select(g => g.Info).ToList();
    }

    /// <summary>
    /// Returns metadata for every icon image present in the binary.
    /// Multiple entries may share the same <see cref="IconInfo.GroupId"/> — they are different
    /// resolutions or colour depths belonging to the same logical icon.
    /// </summary>
    public IReadOnlyList<IconInfo> GetIcons()
    {
        EnsureParsed();

        return _groups.SelectMany(g => g.Info.Images).ToList();
    }

    /// <summary>
    /// Returns the group Windows would display for this binary — the first one in resource directory order —
    /// or <c>null</c> when the binary carries no icons.
    /// </summary>
    public IconGroupInfo? GetPrimaryIconGroup()
    {
        EnsureParsed();

        return _groups.Count > 0 ? _groups[0].Info : null;
    }

    /// <summary>
    /// Builds an ICO file from the binary's primary icon group, preserving every resolution and colour depth
    /// it contains. Returns <c>null</c> when the binary carries no icons.
    /// </summary>
    public byte[]? ExtractPrimaryIcon()
    {
        var group = GetPrimaryIconGroup();

        return group == null ? null : ExtractIcon(group);
    }

    /// <summary>Builds an ICO file from a specific icon group.</summary>
    /// <exception cref="InvalidDataException">None of the group's images could be read.</exception>
    public byte[] ExtractIcon(IconGroupInfo group)
    {
        ArgumentNullException.ThrowIfNull(group);
        EnsureParsed();

        var binding = _groups.FirstOrDefault(g => g.Info == group)
            ?? throw new InvalidOperationException($"Icon group '{group.Name ?? group.Id.ToString()}' was not found in the binary.");

        var images = new List<IconFileImage>(binding.Source.Icons.Count);

        foreach (var entry in binding.Source.Icons)
        {
            if (!_images.TryGetValue(entry.Id, out var data) || data.Length == 0)
                continue;

            images.Add(new IconFileImage(entry.Width, entry.Height, entry.ColorCount, entry.Planes, entry.BitsPerPixel, data));
        }

        if (images.Count == 0)
            throw new InvalidDataException("Icon group does not reference any readable images.");

        return IconFile.Build(images);
    }

    /// <summary>
    /// Decodes and returns the icon image described by <paramref name="iconInfo"/>.
    /// The caller is responsible for disposing the returned <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The resource referenced by <paramref name="iconInfo"/> was not found.</exception>
    public Image<Rgba32> GetIcon(IconInfo iconInfo)
    {
        ArgumentNullException.ThrowIfNull(iconInfo);
        EnsureParsed();

        if (!_images.TryGetValue(iconInfo.ImageId, out var data))
            throw new InvalidOperationException($"RT_ICON resource with ID {iconInfo.ImageId} was not found in the binary.");

        return DibDecoder.Decode(data);
    }

    // -------------------------------------------------------------------------
    //  Parsing
    // -------------------------------------------------------------------------

    private void EnsureParsed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_parsed)
            return;

        Parse();

        _parsed = true;
    }

    private void Parse()
    {
        _groups.Clear();
        _images.Clear();

        PEImage image;

        try
        {
            image = PEImage.FromBytes(_data);
        }
        catch (Exception ex) when (ex is BadImageFormatException or ArgumentException or IndexOutOfRangeException or EndOfStreamException)
        {
            throw new InvalidDataException("Not a valid PE file.", ex);
        }

        // A binary with no .rsrc section at all cannot carry icons.
        if (image.Resources == null)
            return;

        IconResource? icons;

        try
        {
            icons = IconResource.FromDirectory(image.Resources, IconType.Icon);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException("Could not read the icon resources in the PE file.", ex);
        }

        // Resources are present, but none of them are icons.
        if (icons == null)
            return;

        var ordered = icons.Groups
            .OrderBy(g => g.Name == null ? 1 : 0)
            .ThenBy(g => g.Name, StringComparer.Ordinal)
            .ThenBy(g => g.Id)
            .ThenBy(g => g.Lcid);

        foreach (var group in ordered)
        {
            var images = new List<IconInfo>(group.Icons.Count);

            foreach (var entry in group.Icons)
            {
                var data = ToArray(entry.PixelData);

                if (data.Length == 0)
                    continue;

                _images.TryAdd(entry.Id, data);

                images.Add(new IconInfo
                {
                    GroupId   = (int)group.Id,
                    GroupName = group.Name,
                    ImageId   = entry.Id,
                    // The PE convention for 256x256 icons is to store 0 in the width/height byte.
                    Width     = entry.Width  == 0 ? 256 : entry.Width,
                    Height    = entry.Height == 0 ? 256 : entry.Height,
                    BitCount  = entry.BitsPerPixel,
                    IsPng     = DibDecoder.IsPng(data),
                });
            }

            _groups.Add(new GroupBinding(
                new IconGroupInfo
                {
                    Id     = (int)group.Id,
                    Name   = group.Name,
                    Lcid   = group.Lcid,
                    Images = images,
                },
                group));
        }
    }

    private static byte[] ToArray(ISegment? segment) =>
        segment is IReadableSegment readable ? readable.ToArray() : Array.Empty<byte>();

    public void Dispose()
    {
        if (_disposed)
            return;

        _groups.Clear();
        _images.Clear();
        _disposed = true;
    }

    /// <summary>Pairs the metadata handed out to callers with the AsmResolver group it was read from.</summary>
    private sealed record GroupBinding(IconGroupInfo Info, IconGroup Source);
}
