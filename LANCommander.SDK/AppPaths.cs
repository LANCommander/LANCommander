using System;
using System.IO;
using System.Reflection;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK;

public static class AppPaths
{
    private static string _configDirectory = String.Empty;

    public static string GetConfigPath(params string[] paths)
        => Path.Combine(GetConfigDirectory(), Path.Combine(paths));

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
    
    private static (string? Company, string? Product) GetCompanyAndProduct()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        
        return (company, product);
    }
}