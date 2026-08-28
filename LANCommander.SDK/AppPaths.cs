using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK;

public static class AppPaths
{
    private static string _configDirectory = String.Empty;

    public const string DataDirectoryEnvironmentVariable = "LANCOMMANDER_DATA_DIR";

    /// <summary>
    /// Builds a full path under the application's config directory.
    /// </summary>
    /// <param name="paths">Additional path segments appended to the config directory.</param>
    /// <returns>The combined path under the config directory.</returns>
    public static string GetConfigPath(params string[] paths)
        => Path.Combine(GetConfigDirectory(), Path.Combine(paths));

    /// <summary>
    /// Resolves a storage location path to an absolute path using a single, consistent rule so that
    /// every consumer (saves, media, archives, ...) resolves the same way: rooted paths are used as-is,
    /// while relative paths are resolved beneath the config directory (i.e. next to the server binary).
    /// </summary>
    /// <param name="storageLocationPath">The configured storage location path (absolute or relative).</param>
    /// <param name="segments">Additional path segments appended to the resolved storage location.</param>
    /// <returns>The absolute path to the storage location (plus any appended segments).</returns>
    public static string ResolveStorageLocationPath(string storageLocationPath, params string[] segments)
    {
        if (String.IsNullOrWhiteSpace(storageLocationPath))
            throw new ArgumentException("A storage location path must be provided.", nameof(storageLocationPath));

        var root = Path.IsPathRooted(storageLocationPath)
            ? storageLocationPath
            : Path.Combine(GetConfigDirectory(), storageLocationPath);

        return segments is { Length: > 0 }
            ? Path.Combine(new[] { root }.Concat(segments).ToArray())
            : root;
    }

    /// <summary>
    /// Locates (and creates if necessary) the directory in which application data will be stored.
    /// Resolution order: the <see cref="DataDirectoryEnvironmentVariable"/> override if set; otherwise a
    /// "Data" folder under the current working directory when writable; otherwise a "Data" folder under
    /// the current user's platform-native application data directory.
    /// </summary>
    /// <returns>The resolved config directory path.</returns>
    public static string GetConfigDirectory()
    {
        if (!String.IsNullOrWhiteSpace(_configDirectory))
            return _configDirectory;

        var overrideDirectory = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);

        if (!String.IsNullOrWhiteSpace(overrideDirectory))
        {
            // Operator-specified data root is used verbatim (no implicit "Data" subfolder).
            _configDirectory = Path.GetFullPath(overrideDirectory);
        }
        else
        {
            var baseDirectory = Directory.GetCurrentDirectory();

            _configDirectory = DirectoryHelper.IsDirectoryWritable(baseDirectory)
                ? Path.Combine(baseDirectory, "Data")
                : Path.Combine(GetAppDataPath(), "Data");
        }

        if (!Directory.Exists(_configDirectory))
            Directory.CreateDirectory(_configDirectory);

        return _configDirectory;
    }

    /// <summary>
    /// Gets (and creates if necessary) the base per-user application data directory for the current user,
    /// scoped by the entry assembly's company and product metadata. Uses the platform-native convention:
    /// <c>%LOCALAPPDATA%</c> on Windows, <c>~/Library/Application Support</c> on macOS, and
    /// <c>$XDG_DATA_HOME</c> (<c>~/.local/share</c>) on Linux.
    /// </summary>
    /// <returns>The application data path for this application.</returns>
    public static string GetAppDataPath()
    {
        var (company, product) = GetCompanyAndProduct();

        string userRoot;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            // .NET maps LocalApplicationData to ~/.local/share on macOS; use the native location instead.
            userRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        else
            // Windows: %LOCALAPPDATA%. Linux: $XDG_DATA_HOME or ~/.local/share.
            userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var appDataPath = Path.Combine(new[] { userRoot, company, product }
            .Where(segment => !String.IsNullOrWhiteSpace(segment))
            .ToArray()!);

        if (!Directory.Exists(appDataPath))
            Directory.CreateDirectory(appDataPath);

        return appDataPath;
    }
    
    /// <summary>
    /// Checks if the config directory is currently mounted. This should be used in Docker containers only.
    /// </summary>
    /// <returns><c>true</c> if the config directory is a mount point; otherwise <c>false</c>.</returns>
    public static bool ConfigDirectoryIsMounted()
    {
        var path = GetConfigDirectory();
        var fullPath = Path.GetFullPath(path);

        foreach (var line in File.ReadLines("/proc/self/mountinfo"))
        {
            // mountinfo format: see `man proc`
            var parts = line.Split(' ');
            if (parts.Length > 4)
            {
                var mountPoint = parts[4];
                if (string.Equals(mountPoint, fullPath, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// Reads company and product metadata from the entry assembly (or executing assembly as a fallback).
    /// </summary>
    /// <returns>A tuple containing company and product strings (may be null if not defined).</returns>
    private static (string? Company, string? Product) GetCompanyAndProduct()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        
        return (company, product);
    }
}