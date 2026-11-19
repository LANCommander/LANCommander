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
        
        /// <summary>
        /// Waits until the process and every process descended from it have exited. When
        /// <paramref name="cancellationToken"/> is cancelled first, kills all of them and returns.
        /// </summary>
        public static async Task WaitForAllExitAsync(this Process parentProcess, CancellationToken cancellationToken = default)
        {
            using var tree = new ProcessTree(parentProcess);

            await tree.WaitForExitAsync(cancellationToken);
        }
    }
}
