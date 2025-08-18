using System;
using System.IO;
using System.Reflection;

namespace LANCommander.SDK;

public static class AppPaths
{
    private static string _configDirectory = String.Empty;

    public static string GetConfigDirectory()
    {
        if (!String.IsNullOrWhiteSpace(_configDirectory))
            return _configDirectory;

        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        if (IsDirectoryWritable(baseDirectory))
        {
            _configDirectory = baseDirectory;
        }
        else
        {
            var (company, product) = GetCompanyAndProduct();
            var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
            var appDataPath = Path.Combine(userRoot, company, product);
        
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            _configDirectory = appDataPath;
        }
        
        return _configDirectory;
    }

    private static bool IsDirectoryWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);

            var probeFile = Path.Combine(path, $".writetest.{Guid.NewGuid():N}.tmp");

            using (var fs = new FileStream(probeFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.WriteByte(0);
            }

            File.Delete(probeFile);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (string? Company, string? Product) GetCompanyAndProduct()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        
        return (company, product);
    }
}