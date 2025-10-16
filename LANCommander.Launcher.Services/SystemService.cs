using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LANCommander.Launcher.Services;

public static class SystemService
{
    /// <summary>
    /// Registers custom lancommander:/ scheme with the system to have quick start of games
    /// </summary>
    public static void RegisterCustomScheme()
    {
        var scheme = "lancommander";
        
        if (CustomSchemeIsRegistered(scheme))
            return;
        
        var appPath = Environment.ProcessPath
                      ?? Process.GetCurrentProcess().MainModule?.FileName
                      ?? throw new InvalidOperationException("Unable to determine application path.");

        var baseKeyPath = $"Software\\Classes\\{scheme}";
        
        using var baseKey = Registry.CurrentUser.CreateSubKey(baseKeyPath, writable: true);
        
        baseKey.SetValue(null, $"URL:{scheme}", RegistryValueKind.String);
        baseKey.SetValue("URL Protocol", String.Empty, RegistryValueKind.String);

        using (var iconKey = baseKey.CreateSubKey("DefaultIcon", writable: true))
        {
            iconKey?.SetValue(null, $"{Quote(appPath)},0", RegistryValueKind.String);
        }

        var command = $"{Quote(appPath)} \"%1\"";

        using (var commandKey = baseKey.CreateSubKey("shell\\open\\command", writable: true))
        {
            commandKey?.SetValue(null, command, RegistryValueKind.String);
        }
    }

    private static bool CustomSchemeIsRegistered(string scheme)
    {
        using var k = Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{scheme}", writable: true);
        return k is not null;
    }
    
    private static string Quote(string path) =>
        path.Contains(' ') || path.Contains('\t') || path.Contains('"')
            ? $"\"{path.Replace("\"", "\\\"")}\""
            : path;

    public static bool TryBrowseFiles(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return BrowseFilesWindows(path);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return BrowseFilesLinux(path);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return BrowseFilesMac(path);
        
        throw new PlatformNotSupportedException();
    }

    private static bool BrowseFilesWindows(string path)
    {
        try
        {
            Process.Start("explorer", path);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BrowseFilesLinux(string path)
    {
        string[] fileManagers = new[]
        {
            "xdg-open",
            "nautilus",
            "gnome-open",
            "dolphin",
            "konqueror",
        };

        foreach (var fileManager in fileManagers)
        {
            try
            {
                Process.Start(fileManager, path);

                return true;
            }
            catch
            {
                // ignored
            }
        }
        
        return false;
    }

    private static bool BrowseFilesMac(string path)
    {
        try
        {
            Process.Start("open", path);

            return true;
        }
        catch
        {
            return false;
        }
    }
}