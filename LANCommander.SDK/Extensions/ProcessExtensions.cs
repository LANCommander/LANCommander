using LANCommander.SDK.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Extensions
{
    public static class ProcessExtensions
    {
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
