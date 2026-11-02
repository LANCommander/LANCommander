using System.Runtime.Versioning;
using System.Text;

namespace LANCommander.Packaging.Worker;

/// <summary>
/// Enumerates and classifies processes.
/// </summary>
/// <remarks>
/// This work has to happen in the worker rather than the launcher. Snapshotting the process
/// list needs no special rights, but reading a process's image path or machine type requires
/// PROCESS_QUERY_LIMITED_INFORMATION, which a medium-integrity launcher is not guaranteed on an
/// elevated target — and injecting requires far more than that.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class ProcessInspector
{
    internal record ProcessEntry(int ProcessId, int ParentProcessId, string ExecutableName);

    /// <summary>
    /// Returns every running process with its parent, from a single snapshot.
    /// </summary>
    public static List<ProcessEntry> Snapshot()
    {
        var entries = new List<ProcessEntry>();
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);

        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return entries;

        try
        {
            var entry = new NativeMethods.PROCESSENTRY32W
            {
                dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.PROCESSENTRY32W>(),
            };

            if (!NativeMethods.Process32FirstW(snapshot, ref entry))
                return entries;

            do
            {
                entries.Add(new ProcessEntry(
                    (int)entry.th32ProcessID,
                    (int)entry.th32ParentProcessID,
                    entry.szExeFile ?? string.Empty));
            }
            while (NativeMethods.Process32NextW(snapshot, ref entry));
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }

        return entries;
    }

    /// <summary>
    /// Every descendant of <paramref name="rootProcessId"/> in the given snapshot.
    /// </summary>
    /// <remarks>
    /// Walks the whole subtree from one snapshot rather than polling per level, so a
    /// grandchild that appears between polls is still found on the next pass. PIDs are
    /// recycled, so a malformed tree could in principle contain a cycle; the visited set makes
    /// that terminate instead of hanging the poll loop.
    /// </remarks>
    public static List<ProcessEntry> GetDescendants(
        IReadOnlyCollection<ProcessEntry> snapshot, int rootProcessId)
    {
        var byParent = snapshot
            .GroupBy(e => e.ParentProcessId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var descendants = new List<ProcessEntry>();
        var visited = new HashSet<int> { rootProcessId };
        var pending = new Queue<int>();

        pending.Enqueue(rootProcessId);

        while (pending.Count > 0)
        {
            var parentId = pending.Dequeue();

            if (!byParent.TryGetValue(parentId, out var children))
                continue;

            foreach (var child in children)
            {
                if (!visited.Add(child.ProcessId))
                    continue;

                descendants.Add(child);
                pending.Enqueue(child.ProcessId);
            }
        }

        return descendants;
    }

    /// <summary>
    /// Full path of a process's executable, or null when it cannot be read.
    /// </summary>
    public static string? GetImagePath(int processId)
    {
        var handle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);

        if (handle == IntPtr.Zero)
            return null;

        try
        {
            var buffer = new StringBuilder(1024);
            var size = (uint)buffer.Capacity;

            return NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref size)
                ? buffer.ToString()
                : null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    /// <summary>
    /// Determines a running process's architecture, preferring the PE header and falling back
    /// to IsWow64Process2 when the image cannot be read.
    /// </summary>
    public static ProcessArchitecture GetArchitecture(int processId, string? imagePath)
    {
        var fromImage = ProcessArchitectureReader.FromImage(imagePath);

        if (fromImage != ProcessArchitecture.Unknown)
            return fromImage;

        var handle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);

        if (handle == IntPtr.Zero)
            return ProcessArchitecture.Unknown;

        try
        {
            if (!NativeMethods.IsWow64Process2(handle, out var processMachine, out var nativeMachine))
                return ProcessArchitecture.Unknown;

            // IMAGE_FILE_MACHINE_UNKNOWN means "not running under emulation", i.e. the process
            // is native to the host.
            var machine = processMachine == (ushort)PeMachineType.Unknown ? nativeMachine : processMachine;

            return ProcessArchitectureReader.FromMachineType((PeMachineType)machine);
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
