using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LANCommander.Interposer;
using LANCommander.Packager.Models;

namespace LANCommander.Packager.Services;

public class InstallerMonitorService : IDisposable
{
    private readonly List<InterposerService> _interposers = new();
    private readonly ConcurrentDictionary<int, bool> _injectedPids = new();

    private readonly ConcurrentDictionary<string, FileChangeEntry> _fileChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<RegistryChangeEntry> _registryChanges = new();

    private CancellationTokenSource? _childMonitorCts;

    private static readonly string[] IgnoredPathPrefixes = new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Temp",
    };

    private static readonly string[] WriteVerbs =
    {
        "FILE WRITE", 
        "FILE R/W", 
        "FILE COPY", 
        "FILE MOVE"
    };
    
    private static readonly string[] RegistryWriteVerbs =
    {
        "REG WRITE", 
        "REG CREATE"
    };

    public IReadOnlyCollection<FileChangeEntry> FileChanges => _fileChanges.Values.ToList();
    public IReadOnlyCollection<RegistryChangeEntry> RegistryChanges => _registryChanges.ToList();

    public event Action<FileChangeEntry>? OnFileChange;
    public event Action<RegistryChangeEntry>? OnRegistryChange;
    public event Action? OnInstallerExited;

    public int FileChangeCount => _fileChanges.Count;
    public int RegistryChangeCount => _registryChanges.Count;

    private Action<string>? _log;

    public string LaunchInstaller(string installerPath, Action<string>? log = null)
    {
        _log = log;
        
        installerPath = Path.GetFullPath(installerPath);

        if (!File.Exists(installerPath))
            throw new FileNotFoundException($"Installer not found: {installerPath}");

        var dllPath = ResolveNativeDllForTarget(installerPath);
        
        log?.Invoke($"Native DLL: {dllPath}");

        var interposer = CreateInterposer(dllPath, log);

        var psi = new ProcessStartInfo(installerPath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(installerPath)
        };

        var process = interposer.Start(psi, dllPath);
        _injectedPids.TryAdd(process.Id, true);

        log?.Invoke($"Interposer injected (PID {process.Id}).");

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            // Give child processes a moment to finish
            Thread.Sleep(1000);
            _childMonitorCts?.Cancel();
            OnInstallerExited?.Invoke();
        };

        // Start monitoring for child processes
        _childMonitorCts = new CancellationTokenSource();
        
        Task.Run(() => MonitorChildProcesses(process.Id, dllPath, _childMonitorCts.Token));

        return "live";
    }

    private InterposerService CreateInterposer(string? dllPath, Action<string>? log)
    {
        var interposer = new InterposerService();
        _interposers.Add(interposer);

        interposer.PipeDiagnostic += (sender, msg) => log?.Invoke(msg);

        interposer.FileAccessed += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Path))
                return;

            log?.Invoke($"[FILE] {e.Verb}: {e.Path}");

            if (!IsWriteVerb(e.Verb) || IsIgnoredPath(e.Path))
                return;

            var entry = new FileChangeEntry { Verb = e.Verb, Path = e.Path };
            
            _fileChanges.AddOrUpdate(e.Path, entry, (_, _) => entry);
            OnFileChange?.Invoke(entry);
        };

        interposer.RegistryAccessed += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.KeyPath))
                return;

            log?.Invoke($"[REG] {e.Verb}: {e.KeyPath}");

            if (!IsRegistryWriteVerb(e.Verb))
                return;

            var entry = new RegistryChangeEntry
            {
                Verb = e.Verb,
                KeyPath = e.KeyPath,
                ValueName = e.ValueName ?? string.Empty
            };
            
            _registryChanges.Add(entry);
            OnRegistryChange?.Invoke(entry);
        };

        return interposer;
    }

    private async Task MonitorChildProcesses(int parentPid, string? dllPath, CancellationToken ct)
    {
        _log?.Invoke("Monitoring for child processes...");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(500, ct);

                var children = GetChildProcessIds(parentPid);

                foreach (var childPid in children)
                {
                    if (_injectedPids.TryAdd(childPid, true))
                    {
                        try
                        {
                            _log?.Invoke($"Injecting into child process PID {childPid}...");
                            var childInterposer = CreateInterposer(dllPath, _log);
                            childInterposer.Inject(childPid, dllPath);
                            _log?.Invoke($"Injected into child PID {childPid}.");

                            // Also monitor grandchildren
                            _ = Task.Run(() => MonitorChildProcesses(childPid, dllPath, ct));
                        }
                        catch (Exception ex)
                        {
                            _log?.Invoke($"Failed to inject into PID {childPid}: {ex.Message}");
                        }
                    }
                }
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                _log?.Invoke($"Child monitor error: {ex.Message}");
            }
        }
    }

    #region Child process detection via toolhelp32

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
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

    private static List<int> GetChildProcessIds(int parentPid)
    {
        var children = new List<int>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return children;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };

            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    if (entry.th32ParentProcessID == (uint)parentPid)
                        children.Add((int)entry.th32ProcessID);
                }
                while (Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return children;
    }

    #endregion

    #region Install directory detection

    public string DetectInstallDirectory()
    {
        var filePaths = _fileChanges.Keys
            .Where(p => !string.IsNullOrWhiteSpace(p) && Path.IsPathRooted(p))
            .Select(p => Path.GetDirectoryName(p))
            .Where(p => p != null)
            .Cast<string>()
            .ToList();

        if (filePaths.Count == 0)
            return string.Empty;

        var directoryGroups = filePaths
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var group in directoryGroups)
        {
            var dir = group.Key;
            
            if (!IsIgnoredPath(dir) && !IsSystemPath(dir))
                return dir;
        }

        var nonSystemPaths = filePaths.Where(p => !IsIgnoredPath(p) && !IsSystemPath(p)).ToList();
        
        if (nonSystemPaths.Count > 0)
            return FindCommonAncestor(nonSystemPaths);

        return filePaths.First();
    }

    private static string FindCommonAncestor(List<string> paths)
    {
        if (paths.Count == 0)
            return string.Empty;
        
        if (paths.Count == 1)
            return paths[0];

        var splits = paths.Select(p => p.Split(Path.DirectorySeparatorChar)).ToList();
        var minLength = splits.Min(s => s.Length);
        var common = new List<string>();

        for (int i = 0; i < minLength; i++)
        {
            var segment = splits[0][i];
            
            if (splits.All(s => s[i].Equals(segment, StringComparison.OrdinalIgnoreCase)))
                common.Add(segment);
            else
                break;
        }

        return string.Join(Path.DirectorySeparatorChar, common);
    }

    #endregion

    #region Architecture detection

    private static string? ResolveNativeDllForTarget(string exePath)
    {
        bool targetIs64 = IsPE64Bit(exePath);
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var candidates = targetIs64
            ? new[]
            {
                Path.Combine(baseDir, "runtimes", "win-x64", "native", "LANCommander.Interposer.dll"),
                Path.Combine(baseDir, "LANCommander.Interposer.dll"),
            }
            : new[]
            {
                Path.Combine(baseDir, "runtimes", "win-x86", "native", "LANCommander.Interposer.dll"),
            };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            
            if (File.Exists(resolved))
                return resolved;
        }

        return null;
    }

    private static bool IsPE64Bit(string exePath)
    {
        try
        {
            using var fs = File.OpenRead(exePath);
            using var reader = new BinaryReader(fs);

            fs.Seek(0x3C, SeekOrigin.Begin);
            int peOffset = reader.ReadInt32();

            fs.Seek(peOffset, SeekOrigin.Begin);
            uint peSignature = reader.ReadUInt32();
            if (peSignature != 0x00004550)
                return Environment.Is64BitProcess;

            ushort machine = reader.ReadUInt16();
            return machine == 0x8664 || machine == 0xAA64;
        }
        catch
        {
            return Environment.Is64BitProcess;
        }
    }

    #endregion

    #region Verb/path filters

    private static bool IsWriteVerb(string verb)
        => WriteVerbs.Any(v => verb.Equals(v, StringComparison.OrdinalIgnoreCase));

    private static bool IsRegistryWriteVerb(string verb)
        => RegistryWriteVerbs.Any(v => verb.Equals(v, StringComparison.OrdinalIgnoreCase));

    private static bool IsIgnoredPath(string path)
        => IgnoredPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsSystemPath(string path)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (path.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase))
            return false;

        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return path.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    public void Dispose()
    {
        _childMonitorCts?.Cancel();
        _childMonitorCts?.Dispose();

        foreach (var interposer in _interposers)
            interposer.Dispose();

        _interposers.Clear();
    }
}
