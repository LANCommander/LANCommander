using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace LANCommander.Launcher.Services;

public class CurrentProcessInfo : ICurrentProcessInfo
{
    public string ExecutablePath => Process.GetCurrentProcess().MainModule!.FileName;

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
