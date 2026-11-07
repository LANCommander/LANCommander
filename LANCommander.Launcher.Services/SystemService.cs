using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LANCommander.Launcher.Services;

public static class SystemService
{
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