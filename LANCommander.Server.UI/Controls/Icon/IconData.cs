using System.Collections.Concurrent;
using System.IO.Compression;

namespace LANCommander.Server.UI.Controls;

/// <summary>
/// Inner SVG markup for every <see cref="IconType"/>, per weight, loaded on first use from the
/// embedded resources that <c>generate-icons.py</c> writes.
/// </summary>
internal static class IconData
{
    private static readonly ConcurrentDictionary<IconWeight, IReadOnlyDictionary<IconType, string>> _weights = new();

    public static string Get(IconType type, IconWeight weight) =>
        _weights.GetOrAdd(weight, Load).TryGetValue(type, out var markup) ? markup : "";

    private static IReadOnlyDictionary<IconType, string> Load(IconWeight weight)
    {
        var resourceName = $"LANCommander.Server.UI.Icons.phosphor-{weight.ToString().ToLowerInvariant()}.tsv.gz";

        using var stream = typeof(IconData).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing icon resource '{resourceName}'. Run Controls/Icon/generate-icons.py.");
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);

        var icons = new Dictionary<IconType, string>();

        while (reader.ReadLine() is { } line)
        {
            var separator = line.IndexOf('\t');

            if (separator > 0 && Enum.TryParse<IconType>(line[..separator], out var type))
                icons[type] = line[(separator + 1)..];
        }

        return icons;
    }
}

internal enum IconWeight
{
    Regular,
    Bold,
    Fill,
    DuoTone,
}
