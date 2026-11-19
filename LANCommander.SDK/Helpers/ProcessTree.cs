using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Helpers
{
    /// <summary>
    /// A game's first process and every process descended from it, so that a game counts as running
    /// until the last of them exits and stopping it stops all of them. Launchers such as Goldberg's
    /// ColdClientLoader start the real game as a child and may exit before it, or wait on it.
    /// </summary>
    /// <remarks>
    /// On Windows the tree is a job object, which children join as they are created, even those whose
    /// parent exits before the tracker sees them. Processes that leave the job or started before the
    /// first process joined it are found by parent process ID, which is also how the tree is followed
    /// on Linux. Elsewhere only the first process is tracked.
    /// </remarks>
    public sealed class ProcessTree : IDisposable
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

        // A child can't start before its parent, but start times and the launch time come from clocks
        // read at different moments
        private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(1);

        private readonly Process _root;
        private readonly DateTime _notBeforeUtc;
        private readonly ILogger _logger;
        private readonly WindowsJob _job;
        private readonly object _lock = new();

        // Holding each member's Process keeps a handle to it open, so its process ID can't be reused
        // by an unrelated process while it is a member, or a parent of one
        private readonly Dictionary<int, Process> _members = new();

        private bool _reportedRootExit;

        /// <param name="root">The game's first process, already started. Not disposed by the tree.</param>
        /// <param name="launchedAtUtc">
        /// When the game was started; descendants that started earlier belong to an earlier process
        /// that had the same process ID. Defaults to the first process's start time.
        /// </param>
        /// <param name="logger">Logs which processes keep the game running once its first has exited.</param>
        public ProcessTree(Process root, DateTime? launchedAtUtc = null, ILogger logger = null)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _logger = logger;
            _notBeforeUtc = (launchedAtUtc ?? TryGetStartTimeUtc(root) ?? DateTime.MinValue.AddDays(1)) - StartTolerance;

            _members[root.Id] = root;

            if (OperatingSystem.IsWindows())
            {
                _job = WindowsJob.TryCreate(logger);
                _job?.TryAssign(root.Id);
            }
        }

        /// <summary>The processes currently tracked, alive or not.</summary>
        internal IReadOnlyCollection<int> ProcessIds
        {
            get
            {
                lock (_lock)
                    return _members.Keys.ToArray();
            }
        }

        /// <summary>Whether any process in the tree is still running, as of the last refresh.</summary>
        public bool IsRunning
        {
            get
            {
                lock (_lock)
                    return _members.Values.Any(IsAlive);
            }
        }

        /// <summary>
        /// Waits until every process in the tree has exited. When <paramref name="cancellationToken"/>
        /// is cancelled first, kills every process in the tree and returns.
        /// </summary>
        public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                while (true)
                {
                    // Off the caller's thread: a snapshot of every process takes a few milliseconds
                    await Task.Run(Refresh, CancellationToken.None);

                    if (!IsRunning)
                        return;

                    await Task.Delay(PollInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await Task.Run(Kill, CancellationToken.None);
            }
        }

        /// <summary>Kills every process in the tree.</summary>
        public void Kill()
        {
            // Twice: a process can start a child between the snapshot and its own death
            for (var pass = 0; pass < 2; pass++)
            {
                Refresh();

                _job?.Terminate();

                Process[] alive;

                lock (_lock)
                    alive = _members.Values.Where(IsAlive).ToArray();

                if (alive.Length == 0)
                    break;

                foreach (var process in alive)
                {
                    try
                    {
                        _logger?.LogDebug("Killing game process {ProcessName} ({ProcessId})", SafeName(process), process.Id);

                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException or AggregateException)
                    {
                        _logger?.LogWarning(ex, "Could not kill game process {ProcessId}", process.Id);
                    }
                }

                foreach (var process in alive)
                {
                    try
                    {
                        process.WaitForExit(2000);
                    }
                    catch (SystemException)
                    {
                        // Not ours to wait on; the next pass sees whether it's gone
                    }
                }
            }
        }

        /// <summary>Adds processes that have joined the tree since the last refresh.</summary>
        internal void Refresh()
        {
            var snapshot = ProcessSnapshot.Take();

            lock (_lock)
            {
                if (_job != null)
                    foreach (var processId in _job.GetProcessIds())
                        TryAdd(processId, parent: null, "joined the game's job");

                if (snapshot != null)
                {
                    // Until nothing new turns up, so grandchildren are found in the same pass as their parents
                    bool added;

                    do
                    {
                        added = false;

                        foreach (var entry in snapshot)
                        {
                            if (entry.Id == entry.ParentId || _members.ContainsKey(entry.Id))
                                continue;

                            if (!_members.TryGetValue(entry.ParentId, out var parent))
                                continue;

                            if (TryAdd(entry.Id, parent, "started by a game process"))
                            {
                                _job?.TryAssign(entry.Id);
                                added = true;
                            }
                        }
                    }
                    while (added);
                }

                if (!_reportedRootExit && !IsAlive(_root) && _members.Values.Any(IsAlive))
                {
                    _reportedRootExit = true;

                    _logger?.LogInformation(
                        "Game process {ProcessName} ({ProcessId}) exited; the game keeps running while these do: {Processes}",
                        SafeName(_root),
                        _root.Id,
                        string.Join(", ", _members.Values.Where(IsAlive).Select(p => $"{SafeName(p)} ({p.Id})")));
                }
            }
        }

        private bool TryAdd(int processId, Process parent, string reason)
        {
            if (_members.ContainsKey(processId))
                return false;

            Process process = null;

            try
            {
                process = Process.GetProcessById(processId);

                var startTime = TryGetStartTimeUtc(process);

                // Its parent process ID belongs to an earlier process that had the same ID
                if (startTime == null || startTime < _notBeforeUtc)
                {
                    process.Dispose();
                    return false;
                }

                if (parent != null && TryGetStartTimeUtc(parent) is { } parentStart && startTime < parentStart - StartTolerance)
                {
                    process.Dispose();
                    return false;
                }

                // Opens the handle that keeps the process ID from being reused
                if (process.HasExited)
                {
                    process.Dispose();
                    return false;
                }

                _members[processId] = process;

                _logger?.LogDebug("Tracking game process {ProcessName} ({ProcessId}), {Reason}", SafeName(process), processId, reason);

                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
            {
                // Exited already, or not ours to look at
                process?.Dispose();

                return false;
            }
        }

        private static bool IsAlive(Process process)
        {
            try
            {
                return !process.HasExited;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return false;
            }
        }

        private static DateTime? TryGetStartTimeUtc(Process process)
        {
            try
            {
                return process.StartTime.ToUniversalTime();
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return null;
            }
        }

        private static string SafeName(Process process)
        {
            try
            {
                return process.ProcessName;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return "?";
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var process in _members.Values)
                    if (!ReferenceEquals(process, _root))
                        process.Dispose();

                _members.Clear();
            }

            // Closing the job leaves its processes running: the launcher closing doesn't stop the game
            _job?.Dispose();
        }

        /// <summary>Every running process's ID and its parent's.</summary>
        private static class ProcessSnapshot
        {
            internal readonly record struct Entry(int Id, int ParentId);

            /// <returns>Null where the platform has no way to list parent process IDs.</returns>
            internal static List<Entry> Take()
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                        return TakeWindows();

                    if (OperatingSystem.IsLinux())
                        return TakeLinux();
                }
                catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
                {
                }

                return null;
            }

            private static List<Entry> TakeWindows()
            {
                var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);

                if (snapshot == NativeMethods.InvalidHandleValue)
                    throw new Win32Exception();

                try
                {
                    var entries = new List<Entry>();
                    var entry = new NativeMethods.PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<NativeMethods.PROCESSENTRY32W>() };

                    if (!NativeMethods.Process32FirstW(snapshot, ref entry))
                        return entries;

                    do
                    {
                        entries.Add(new Entry((int)entry.th32ProcessID, (int)entry.th32ParentProcessID));
                    }
                    while (NativeMethods.Process32NextW(snapshot, ref entry));

                    return entries;
                }
                finally
                {
                    NativeMethods.CloseHandle(snapshot);
                }
            }

            private static List<Entry> TakeLinux()
            {
                var entries = new List<Entry>();

                foreach (var directory in Directory.EnumerateDirectories("/proc"))
                {
                    if (!int.TryParse(Path.GetFileName(directory), out var processId))
                        continue;

                    try
                    {
                        // pid (comm) state ppid ...; comm may itself contain spaces and parentheses
                        var stat = File.ReadAllText(Path.Combine(directory, "stat"));
                        var fields = stat.Substring(stat.LastIndexOf(')') + 2).Split(' ');

                        if (fields.Length > 1 && int.TryParse(fields[1], out var parentId))
                            entries.Add(new Entry(processId, parentId));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
                    {
                        // Exited while listing
                    }
                }

                return entries;
            }
        }

        /// <summary>
        /// A job object for the game's processes. Not kill-on-close, and processes may ask to break
        /// away from it, so games behave as they would outside one.
        /// </summary>
        private sealed class WindowsJob : IDisposable
        {
            private readonly IntPtr _handle;
            private readonly ILogger _logger;

            private WindowsJob(IntPtr handle, ILogger logger)
            {
                _handle = handle;
                _logger = logger;
            }

            internal static WindowsJob TryCreate(ILogger logger)
            {
                var handle = NativeMethods.CreateJobObjectW(IntPtr.Zero, null);

                if (handle == IntPtr.Zero)
                {
                    logger?.LogDebug(new Win32Exception(), "Could not create a job object for the game; following its processes by parent instead");
                    return null;
                }

                var limits = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                limits.BasicLimitInformation.LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_BREAKAWAY_OK;

                // Without it, a child created with CREATE_BREAKAWAY_FROM_JOB would fail to start
                if (!NativeMethods.SetInformationJobObject(
                        handle,
                        NativeMethods.JobObjectExtendedLimitInformation,
                        ref limits,
                        (uint)Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
                {
                    logger?.LogDebug(new Win32Exception(), "Could not allow breakaway from the game's job object; not using one");
                    NativeMethods.CloseHandle(handle);
                    return null;
                }

                return new WindowsJob(handle, logger);
            }

            internal void TryAssign(int processId)
            {
                var process = NativeMethods.OpenProcess(NativeMethods.PROCESS_SET_QUOTA | NativeMethods.PROCESS_TERMINATE, false, (uint)processId);

                if (process == IntPtr.Zero)
                {
                    // Most often a process running elevated; it's still followed by parent
                    _logger?.LogDebug(new Win32Exception(), "Could not open game process {ProcessId} to add it to the game's job", processId);
                    return;
                }

                try
                {
                    if (!NativeMethods.AssignProcessToJobObject(_handle, process))
                        _logger?.LogDebug(new Win32Exception(), "Could not add game process {ProcessId} to the game's job", processId);
                }
                finally
                {
                    NativeMethods.CloseHandle(process);
                }
            }

            internal IEnumerable<int> GetProcessIds()
            {
                var capacity = 64;

                while (true)
                {
                    var headerSize = 2 * sizeof(uint);
                    var size = headerSize + capacity * IntPtr.Size;
                    var buffer = Marshal.AllocHGlobal(size);

                    try
                    {
                        if (NativeMethods.QueryInformationJobObject(_handle, NativeMethods.JobObjectBasicProcessIdList, buffer, (uint)size, out _))
                        {
                            var count = Marshal.ReadInt32(buffer, sizeof(uint));
                            var ids = new int[count];

                            // ProcessIdList is ULONG_PTR-aligned after the two DWORD counts
                            var offset = IntPtr.Size == 8 ? 8 : headerSize;

                            for (var i = 0; i < count; i++)
                                ids[i] = (int)Marshal.ReadIntPtr(buffer, offset + i * IntPtr.Size);

                            return ids;
                        }

                        if (Marshal.GetLastWin32Error() != NativeMethods.ERROR_MORE_DATA || capacity >= 65536)
                            return Array.Empty<int>();

                        capacity *= 4;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }

            internal void Terminate()
            {
                if (!NativeMethods.TerminateJobObject(_handle, 1))
                    _logger?.LogDebug(new Win32Exception(), "Could not terminate the game's job object");
            }

            public void Dispose() => NativeMethods.CloseHandle(_handle);
        }

        private static class NativeMethods
        {
            internal const uint TH32CS_SNAPPROCESS = 0x00000002;
            internal const uint PROCESS_TERMINATE = 0x0001;
            internal const uint PROCESS_SET_QUOTA = 0x0100;
            internal const uint JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800;
            internal const int JobObjectBasicProcessIdList = 3;
            internal const int JobObjectExtendedLimitInformation = 9;
            internal const int ERROR_MORE_DATA = 234;

            internal static readonly IntPtr InvalidHandleValue = new(-1);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct PROCESSENTRY32W
            {
                public uint dwSize;
                public uint cntUsage;
                public uint th32ProcessID;
                public IntPtr th32DefaultHeapID;
                public uint th32ModuleID;
                public uint cntThreads;
                public uint th32ParentProcessID;
                public int pcPriClassBase;
                public uint dwFlags;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
                public string szExeFile;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                public long PerProcessUserTimeLimit;
                public long PerJobUserTimeLimit;
                public uint LimitFlags;
                public UIntPtr MinimumWorkingSetSize;
                public UIntPtr MaximumWorkingSetSize;
                public uint ActiveProcessLimit;
                public UIntPtr Affinity;
                public uint PriorityClass;
                public uint SchedulingClass;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct IO_COUNTERS
            {
                public ulong ReadOperationCount;
                public ulong WriteOperationCount;
                public ulong OtherOperationCount;
                public ulong ReadTransferCount;
                public ulong WriteTransferCount;
                public ulong OtherTransferCount;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
                public IO_COUNTERS IoInfo;
                public UIntPtr ProcessMemoryLimit;
                public UIntPtr JobMemoryLimit;
                public UIntPtr PeakProcessMemoryUsed;
                public UIntPtr PeakJobMemoryUsed;
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            internal static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            internal static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            internal static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string lpName);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, uint length);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool QueryInformationJobObject(IntPtr hJob, int infoClass, IntPtr info, uint length, out uint returnLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool CloseHandle(IntPtr hObject);
        }
    }
}
