using LANCommander.SDK.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Extensions
{
    public static class ProcessExtensions
    {
        internal static class LibC
        {
            [DllImport("libc", SetLastError = true)]
            internal static extern int kill(int pid, int sig);
        }
        
        public static void Kill(this Process process, ProcessTerminationMethod signal = ProcessTerminationMethod.Kill)
        {
            switch (signal)
            {
                case ProcessTerminationMethod.Close:
                    if (!process.CloseMainWindow())
                        process.Kill(true);
                    break;

                case ProcessTerminationMethod.Kill:
                    process.Kill(true);
                    break;

                case ProcessTerminationMethod.SIGHUP:
                    SendSignal(process, 1);
                    break;

                case ProcessTerminationMethod.SIGINT:
                    SendSignal(process, 2);
                    break;

                case ProcessTerminationMethod.SIGKILL:
                    SendSignal(process, 9);
                    break;

                case ProcessTerminationMethod.SIGTERM:
                    SendSignal(process, 15);
                    break;
            }
        }

        private static void SendSignal(Process process, int signal)
        {
            // POSIX signals aren't available on Windows, so fall back to a hard kill.
            if (OperatingSystem.IsWindows())
                process.Kill(true);
            else
                LibC.kill(process.Id, signal);
        }
        
        public static async Task WaitForAllExitAsync(this Process parentProcess, CancellationToken cancellationToken = default)
        {
            var exited = parentProcess.HasExited;
            int pid = parentProcess.Id;

            IEnumerable<int> existingProcessIds = Process.GetProcesses().Select(p => p.Id);

            try
            {
                await parentProcess.WaitForExitAsync();

                IEnumerable<int> newProcessIds = Process.GetProcesses().Where(p => !existingProcessIds.Contains(p.Id)).Select(p => p.Id);

                foreach (var processId in newProcessIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // This could be adapted better for non-Windows platforms.
                    // Not even sure how Process works on other platforms, but
                    // there's other ways of tracking child processes. One way
                    // that would need to be tested out would be to get the
                    // child process at this point and check the executable's
                    // file location. If it's in {InstallDir}, track it!
                    int? parentProcessId = ProcessHelper.GetParentProcessId(processId);

                    if (parentProcessId == pid)
                    {
                        var process = Process.GetProcessById(processId);

                        if (!process.HasExited)
                        {
                            
                            try
                            {
                                await process.WaitForExitAsync(cancellationToken);
                            }
                            catch (OperationCanceledException)
                            {
                                await Task.Run(() =>
                                {
                                    if (!process.HasExited)
                                        process.Kill();
                                });
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                var childProcessIds = Process.GetProcesses().Where(p => ProcessHelper.GetParentProcessId(p.Id) == pid).Select(p => p.Id);

                foreach (var childProcessId in childProcessIds)
                {
                    try
                    {
                        var childProcess = Process.GetProcessById(childProcessId);

                        await Task.Run(() =>
                        {
                            if (!childProcess.HasExited)
                                childProcess.Kill();
                        });
                    }
                    catch { }
                }

                await Task.Run(() =>
                {
                    if (!parentProcess.HasExited)
                        parentProcess.Kill();
                });
            }
        }
    }
}
