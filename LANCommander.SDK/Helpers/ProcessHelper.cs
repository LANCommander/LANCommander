using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Helpers
{
    internal static class ProcessHelper
    {
        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION pbi, uint processInformationLength, out uint returnLength);

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
        {
            internal IntPtr ExitStatus;
            internal IntPtr PebBaseAddress;
            internal IntPtr AffinityMask;
            internal IntPtr BasePriority;
            internal IntPtr UniqueProcessId;
            internal IntPtr InheritedFromUniqueProcessId;
        }

        internal static int? GetParentProcessId(int processId)
        {
            try
            {
                var pbi = new PROCESS_BASIC_INFORMATION();
                uint returnLength;
                using (var process = Process.GetProcessById(processId))
                {
                    int status = NtQueryInformationProcess(process.Handle, 0, ref pbi, (uint)Marshal.SizeOf(pbi), out returnLength);
                    if (status == 0) // STATUS_SUCCESS
                        return (int)pbi.InheritedFromUniqueProcessId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching parent process ID: {ex.Message}");
            }

            return null;
        }
    }
}
