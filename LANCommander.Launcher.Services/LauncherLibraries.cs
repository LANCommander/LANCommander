using LANCommander.Packaging;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Where the launcher keeps the binaries that have to stay real files beside it, because they are
/// loaded into or run as other processes: <c>Data\Libraries\</c>, next to the executable.
/// </summary>
/// <remarks>
/// Resolved from the application's directory, not from <see cref="SDK.AppPaths.GetConfigDirectory"/>:
/// they ship with the build, so they are found where it was extracted even when the launcher's data
/// lives elsewhere (an override, or a read-only install directory). That also keeps the elevated
/// packaging worker out of the per-user data directory, which anything the user runs can write to.
/// </remarks>
public static class LauncherLibraries
{
    public const string DataDirectoryName = "Data";

    public const string LibrariesDirectoryName = "Libraries";

    /// <summary>The Interposer's DLL and injector, under <c>x86\</c> and <c>x64\</c>.</summary>
    public const string InterposerDirectoryName = "Interposer";

    /// <summary>The packaging workers, under <c>x86\</c> and <c>x64\</c>.</summary>
    public const string PackagingDirectoryName = "Packaging";

    /// <summary>
    /// The libraries directory, whether or not it exists: the first candidate that does, otherwise
    /// the one under the application's base directory.
    /// </summary>
    public static string Root
    {
        get
        {
            var candidates = GetCandidateRoots().ToList();

            return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        }
    }

    public static string InterposerDirectory => Path.Combine(Root, InterposerDirectoryName);

    public static string PackagingDirectory => Path.Combine(Root, PackagingDirectoryName);

    /// <summary>
    /// Directories the libraries may sit in, most likely first.
    /// </summary>
    /// <remarks>
    /// Both are needed, because neither is correct on its own:
    /// <list type="bullet">
    /// <item><see cref="AppContext.BaseDirectory"/> is the application's own directory, including
    /// under single-file publish, but is not what a host-launched process reports.</item>
    /// <item><see cref="Environment.ProcessPath"/> is the actual executable — which is
    /// <c>dotnet.exe</c> when the launcher is started through the shared host rather than its
    /// apphost, and then points at the SDK install rather than the app.</item>
    /// </list>
    /// </remarks>
    public static IEnumerable<string> GetCandidateRoots()
    {
        var appBase = AppContext.BaseDirectory;

        yield return Path.Combine(appBase, DataDirectoryName, LibrariesDirectoryName);

        var processDirectory = Path.GetDirectoryName(Environment.ProcessPath);

        if (!string.IsNullOrEmpty(processDirectory) &&
            !string.Equals(
                Path.TrimEndingDirectorySeparator(processDirectory),
                Path.TrimEndingDirectorySeparator(appBase),
                StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(processDirectory, DataDirectoryName, LibrariesDirectoryName);
        }
    }

    /// <summary>
    /// The per-architecture subdirectory name, or null for an architecture nothing is built for.
    /// </summary>
    public static string? GetArchitectureDirectoryName(ProcessArchitecture architecture) => architecture switch
    {
        ProcessArchitecture.X86 => "x86",
        ProcessArchitecture.X64 => "x64",
        _ => null,
    };

    /// <summary>
    /// Removes what builds before <c>Data\Libraries\</c> put beside the executable. The updater
    /// extracts a release over the old one, so without this an updated install keeps both.
    /// </summary>
    public static void RemoveLegacyLayout(ILogger? logger = null)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var appBase = AppContext.BaseDirectory;

        // Only what those builds are known to have put there, so nothing a user added is touched
        foreach (var runtimeIdentifier in new[] { "win-x64", "win-x86" })
        {
            var workerDirectory = Path.Combine(appBase, PackagingDirectoryName, runtimeIdentifier);

            if (File.Exists(Path.Combine(workerDirectory, "LANCommander.Packaging.Worker.exe")))
                TryDelete(() => Directory.Delete(workerDirectory, recursive: true), workerDirectory, logger);
        }

        TryDeleteIfEmpty(Path.Combine(appBase, PackagingDirectoryName), logger);

        foreach (var architecture in new[] { "x64", "x86" })
        {
            var directory = Path.Combine(appBase, architecture);

            foreach (var file in new[] { "LANCommander.Interposer.dll", "LANCommander.Interposer.Injector.exe" })
            {
                var path = Path.Combine(directory, file);

                if (File.Exists(path))
                    TryDelete(() => File.Delete(path), path, logger);
            }

            TryDeleteIfEmpty(directory, logger);
        }
    }

    private static void TryDeleteIfEmpty(string directory, ILogger? logger)
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            TryDelete(() => Directory.Delete(directory), directory, logger);
    }

    private static void TryDelete(Action delete, string path, ILogger? logger)
    {
        try
        {
            delete();

            logger?.LogInformation("Removed {Path}, which now lives under {Libraries}", path, Root);
        }
        catch (Exception ex)
        {
            // Most likely a game still running with the old DLL loaded; the next start tries again
            logger?.LogDebug(ex, "Could not remove {Path}", path);
        }
    }
}
