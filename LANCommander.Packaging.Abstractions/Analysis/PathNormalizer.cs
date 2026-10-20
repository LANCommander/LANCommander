using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace LANCommander.Packaging.Analysis;

/// <summary>
/// Canonicalizes the paths reported by the file hooks so the same file is always the same key.
/// <para>
/// The kernel32 hooks report whatever form the caller passed to CreateFileW, so one file can
/// arrive as <c>C:\Games\X</c>, <c>\?\C:\Games\X</c> and <c>\Device\HarddiskVolume2\Games\X</c>.
/// Without normalization each form becomes a separate change record and a separate node in the
/// file selection tree.
/// </para>
/// </summary>
public static class PathNormalizer
{
    private const string ExtendedPrefix = @"\\?\";
    private const string ExtendedUncPrefix = @"\\?\UNC\";
    private const string DevicePrefix = @"\Device\";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> DeviceMap =
        new(BuildDeviceMap, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Returns a canonical absolute path, or the trimmed input when it cannot be resolved.
    /// Never throws.
    /// </summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var result = path.Trim();

        result = StripExtendedPrefix(result);
        result = ResolveDevicePath(result);

        try
        {
            if (Path.IsPathRooted(result))
                result = Path.GetFullPath(result);
        }
        catch
        {
            // Paths containing wildcards or invalid characters (installers do probe with them)
            // cannot be canonicalized; the stripped form is still a better key than the raw one.
        }

        return TrimTrailingSeparator(result);
    }

    /// <summary>
    /// True when both paths refer to the same file once normalized.
    /// </summary>
    public static bool AreSame(string? left, string? right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string StripExtendedPrefix(string path)
    {
        // \?\UNC\server\share -> \server\share
        if (path.StartsWith(ExtendedUncPrefix, StringComparison.OrdinalIgnoreCase))
            return @"\\" + path[ExtendedUncPrefix.Length..];

        if (path.StartsWith(ExtendedPrefix, StringComparison.Ordinal))
            return path[ExtendedPrefix.Length..];

        return path;
    }

    /// <summary>
    /// Rewrites <c>\Device\HarddiskVolumeN\rest</c> as <c>X:\rest</c> when the volume maps to a
    /// drive letter. Unmapped device paths are returned unchanged.
    /// </summary>
    private static string ResolveDevicePath(string path)
    {
        if (!path.StartsWith(DevicePrefix, StringComparison.OrdinalIgnoreCase))
            return path;

        foreach (var (devicePath, driveLetter) in DeviceMap.Value)
        {
            if (!path.StartsWith(devicePath, StringComparison.OrdinalIgnoreCase))
                continue;

            // Only match on a path-segment boundary so HarddiskVolume1 never swallows
            // HarddiskVolume10.
            if (path.Length > devicePath.Length && path[devicePath.Length] != '\\')
                continue;

            var remainder = path[devicePath.Length..].TrimStart('\\');

            return Path.Combine(driveLetter + @"\", remainder);
        }

        return path;
    }

    private static string TrimTrailingSeparator(string path)
    {
        if (path.Length <= 3)
            return path;

        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Keep the separator on a bare drive root ("C:\"), drop it everywhere else.
        return trimmed.Length == 2 && trimmed[1] == ':' ? trimmed + Path.DirectorySeparatorChar : trimmed;
    }

    private static IReadOnlyDictionary<string, string> BuildDeviceMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows())
            return map;

        try
        {
            for (var letter = 'A'; letter <= 'Z'; letter++)
            {
                var drive = $"{letter}:";
                var buffer = new StringBuilder(512);

                if (QueryDosDeviceW(drive, buffer, buffer.Capacity) == 0)
                    continue;

                var target = buffer.ToString();

                if (!string.IsNullOrEmpty(target))
                    map[target] = drive;
            }
        }
        catch
        {
            // Without the map, device paths simply stay in device form.
        }

        return map;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDeviceW(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);
}
