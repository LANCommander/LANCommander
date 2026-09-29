using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using LANCommander.SDK;

namespace LANCommander.Launcher.Services;

public class CurrentProcessInfo : ICurrentProcessInfo
{
    public string ExecutablePath => Process.GetCurrentProcess().MainModule!.FileName;

    public string WorkingDirectory => Directory.GetCurrentDirectory();

    public string ConfigDirectory => AppPaths.GetConfigDirectory();

    public bool IsElevated
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);

                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return Environment.UserName == "root";
        }
    }
}
