using System.Diagnostics;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services;

public class ElevatedProcessLauncher : IElevatedProcessLauncher
{
    public async Task LaunchAndWaitAsync(ElevatedProcessRequest request)
    {
        using var process = new Process();

        process.StartInfo.FileName = request.FileName;
        process.StartInfo.Verb = "runas";
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.WorkingDirectory = request.WorkingDirectory;
        process.StartInfo.Arguments = request.Arguments;

        process.Start();

        await process.WaitForExitAsync();
    }
}
