using System;
using System.IO;
using System.Reflection;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK;

public static class AppPaths
{
    private static string _configDirectory = String.Empty;

    /// <summary>
    /// Builds a full path under the application's config directory.
    /// </summary>
    /// <param name="paths">Additional path segments appended to the config directory.</param>
    /// <returns>The combined path under the config directory.</returns>
    public static string GetConfigPath(params string[] paths)
        => Path.Combine(GetConfigDirectory(), Path.Combine(paths));

    /// <summary>
    /// Locates (and creates if necessary) the directory in which application data will be stored.
    /// Prefers the current working directory when writable; otherwise falls back to the user's local application data.
    /// </summary>
    /// <returns>The resolved config directory path.</returns>
    public static string GetConfigDirectory()
    {
        if (!String.IsNullOrWhiteSpace(_configDirectory))
            return _configDirectory;

        var baseDirectory = Directory.GetCurrentDirectory();

        if (DirectoryHelper.IsDirectoryWritable(baseDirectory))
            _configDirectory = baseDirectory;
        else
            _configDirectory = GetAppDataPath();
        
        _configDirectory = Path.Combine(_configDirectory, "Data");
        
        if (!Directory.Exists(_configDirectory))
            Directory.CreateDirectory(_configDirectory);
        
        return _configDirectory;
    }

    /// <summary>
    /// Gets (and creates if necessary) the base local application data directory for the current user,
    /// scoped by the entry assembly's company and product metadata.
    /// </summary>
    /// <returns>The local application data path for this application.</returns>
    public static string GetAppDataPath()
    {
        var (company, product) = GetCompanyAndProduct();
        var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
        var appDataPath = Path.Combine(userRoot, company, product);
        
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