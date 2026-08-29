using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using LANCommander.Interposer;
using LANCommander.Packaging.Ipc;

namespace LANCommander.Packaging.Worker;

/// <summary>
/// Owns injection and child-process discovery for this worker's architecture.
/// </summary>
/// <remarks>
/// Reports <em>every</em> process it discovers, including ones it cannot inject into. Injection
/// only works between processes of the same bitness, so a process this worker cannot handle is
/// still news the launcher needs in order to route it to the worker that can.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class InstallerMonitor : IAsyncDisposable
{
    private readonly ChangeCollector _collector;
    private readonly WorkerConnection _connection;
    private readonly string? _interposerDllPath;
    private readonly ProcessArchitecture _architecture;

    private readonly List<InterposerService> _interposers = [];
    private readonly ConcurrentDictionary<int, byte> _injected = new();
    private readonly ConcurrentDictionary<int, byte> _reported = new();
    private readonly ConcurrentDictionary<int, byte> _watchedRoots = new();

    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _interposerLock = new();

    private Task? _pollTask;

    public InstallerMonitor(
        ChangeCollector collector,
        WorkerConnection connection,
        string? interposerDllPath,
        ProcessArchitecture architecture)
    {
        _collector = collector;
        _connection = connection;
        _interposerDllPath = interposerDllPath;
        _architecture = architecture;
    }

    /// <summary>
    /// Launches the installer suspended, injects, and resumes it.
    /// </summary>
    public async Task<CommandResultMessage> LaunchAsync(LaunchInstallerCommand command)
    {
        try
        {
            var executablePath = Path.GetFullPath(command.ExecutablePath);

            if (!File.Exists(executablePath))
                throw new FileNotFoundException($"Installer not found: {executablePath}");

            var interposer = CreateInterposer();

            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false,
                Arguments = command.Arguments ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(command.WorkingDirectory)
                    ? Path.GetDirectoryName(executablePath)
                    : command.WorkingDirectory,
            };

            var process = interposer.Start(startInfo, _interposerDllPath!);

            _injected.TryAdd(process.Id, 0);
            _reported.TryAdd(process.Id, 0);
            _watchedRoots.TryAdd(process.Id, 0);

            await _connection.LogAsync(
                PackagingLogLevel.Information,
                $"Launched and injected {Path.GetFileName(executablePath)} (PID {process.Id}).");

            WatchForExit(process, isRoot: true);
            StartPolling();

            return new CommandResultMessage
            {
                CorrelationId = command.CorrelationId,
                Success = true,
                ProcessId = process.Id,
            };
        }
        catch (Exception ex)
        {
            return Failure(command.CorrelationId, ex);
        }
    }

    /// <summary>
    /// Injects into a process the launcher routed here because its architecture matches.
    /// </summary>
    public async Task<CommandResultMessage> InjectAsync(InjectCommand command)
    {
        try
        {
            if (!_injected.TryAdd(command.ProcessId, 0))
                return new CommandResultMessage
                {
                    CorrelationId = command.CorrelationId,
                    Success = true,
                    ProcessId = command.ProcessId,
                };

            var interposer = CreateInterposer();

            interposer.Inject(command.ProcessId, _interposerDllPath!);

            _watchedRoots.TryAdd(command.ProcessId, 0);

            await _connection.LogAsync(
                PackagingLogLevel.Information, $"Injected into PID {command.ProcessId}.");

            StartPolling();

            return new CommandResultMessage
            {
                CorrelationId = command.CorrelationId,
                Success = true,
                ProcessId = command.ProcessId,
            };
        }
        catch (Exception ex)
        {
            // Let a later discovery retry rather than permanently marking it injected.
            _injected.TryRemove(command.ProcessId, out _);

            return Failure(command.CorrelationId, ex, command.ProcessId);
        }
    }

    /// <summary>
    /// Adds a subtree to this worker's polling scope.
    /// </summary>
    public Task<CommandResultMessage> AdoptSubtreeAsync(AdoptSubtreeCommand command)
    {
        _watchedRoots.TryAdd(command.RootProcessId, 0);

        StartPolling();

        return Task.FromResult(new CommandResultMessage
        {
            CorrelationId = command.CorrelationId,
            Success = true,
            ProcessId = command.RootProcessId,
        });
    }

    private InterposerService CreateInterposer()
    {
        var interposer = new InterposerService();

        lock (_interposerLock)
            _interposers.Add(interposer);

        // These handlers run on the Interposer's pipe-reader task. They must not block: the
        // collector's ingest is a non-blocking enqueue for exactly that reason.
        interposer.FileAccessed += (_, e) => _collector.AddFile(e.Verb, e.Path, 0);

        interposer.RegistryAccessed += (_, e) =>
            _collector.AddRegistry(e.Verb, e.KeyPath, e.ValueName, 0, _architecture);

        interposer.PipeDiagnostic += (_, message) =>
            _ = _connection.LogAsync(PackagingLogLevel.Debug, message);

        return interposer;
    }

    private void StartPolling()
    {
        _pollTask ??= Task.Run(() => PollAsync(_cts.Token));
    }

    /// <summary>
    /// Rescans watched subtrees, injecting into same-architecture processes and reporting
    /// everything else upward for the launcher to route.
    /// </summary>
    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PackagingProtocol.ChildScanInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var snapshot = ProcessInspector.Snapshot();

                foreach (var rootId in _watchedRoots.Keys)
                {
                    foreach (var descendant in ProcessInspector.GetDescendants(snapshot, rootId))
                    {
                        if (!_reported.TryAdd(descendant.ProcessId, 0))
                            continue;

                        await HandleDiscoveredAsync(descendant, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await _connection.LogAsync(
                PackagingLogLevel.Error, $"Child process scan stopped: {ex.Message}");
        }
    }

    private async Task HandleDiscoveredAsync(
        ProcessInspector.ProcessEntry entry, CancellationToken cancellationToken)
    {
        var imagePath = ProcessInspector.GetImagePath(entry.ProcessId);
        var architecture = ProcessInspector.GetArchitecture(entry.ProcessId, imagePath);

        var message = new ProcessDiscoveredMessage
        {
            ProcessId = entry.ProcessId,
            ParentProcessId = entry.ParentProcessId,
            ImagePath = imagePath ?? entry.ExecutableName,
            Architecture = architecture,
        };

        if (architecture == _architecture)
        {
            try
            {
                if (_injected.TryAdd(entry.ProcessId, 0))
                {
                    var interposer = CreateInterposer();

                    interposer.Inject(entry.ProcessId, _interposerDllPath!);

                    _watchedRoots.TryAdd(entry.ProcessId, 0);
                }

                message.InjectedLocally = true;
            }
            catch (Exception ex)
            {
                _injected.TryRemove(entry.ProcessId, out _);

                message.InjectionError = ex.Message;
            }
        }

        await _connection.SendAsync(message, cancellationToken);
    }

    /// <summary>
    /// Kills the processes being monitored and waits for them to actually go away.
    /// </summary>
    /// <remarks>
    /// Used when the session is about to relaunch the same installer elevated. Returning before
    /// the old run has exited would leave two copies of the installer writing to the same place.
    /// </remarks>
    public async Task TerminateTargetsAsync(CancellationToken cancellationToken = default)
    {
        // Roots first: killing a process tree takes its children with it, so the remaining
        // per-pid passes usually find nothing left to do.
        var targets = _watchedRoots.Keys.Concat(_injected.Keys).Distinct().ToList();

        foreach (var processId in targets)
        {
            try
            {
                using var process = Process.GetProcessById(processId);

                if (process.HasExited)
                    continue;

                process.Kill(entireProcessTree: true);

                await _connection.LogAsync(
                    PackagingLogLevel.Information, $"Terminated monitored process {processId}.");
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
            catch (Exception ex)
            {
                await _connection.LogAsync(
                    PackagingLogLevel.Warning,
                    $"Could not terminate monitored process {processId}: {ex.Message}");
            }
        }

        foreach (var processId in targets)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeout.CancelAfter(TimeSpan.FromSeconds(10));

                await process.WaitForExitAsync(timeout.Token);
            }
            catch (ArgumentException)
            {
                // Already gone, which is the outcome we wanted.
            }
            catch (Exception)
            {
                // A process that will not die should not block the restart; the elevated run
                // will surface the conflict itself if there really is one.
            }
        }
    }

    private void WatchForExit(Process process, bool isRoot)
    {
        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += async (_, _) =>
            {
                try
                {
                    await _connection.SendAsync(new ProcessExitedMessage
                    {
                        ProcessId = process.Id,
                        ExitCode = process.ExitCode,
                        IsRoot = isRoot,
                    });
                }
                catch
                {
                    // The connection is torn down elsewhere; nothing useful to do here.
                }
            };
        }
        catch (Exception)
        {
            // A process that exits between launch and subscription is not an error.
        }
    }

    private static CommandResultMessage Failure(Guid correlationId, Exception ex, int? processId = null)
    {
        var win32 = FindWin32Exception(ex);
        var code = win32?.NativeErrorCode ?? 0;

        return new CommandResultMessage
        {
            CorrelationId = correlationId,
            Success = false,
            Error = ex.Message,
            Win32Error = code,
            ProcessId = processId,

            // Both of these mean "this worker's integrity level is too low", which the launcher
            // can fix by respawning it through UAC.
            RequiresElevation =
                code is NativeMethods.ErrorElevationRequired or NativeMethods.ErrorAccessDenied,
        };
    }

    private static Win32Exception? FindWin32Exception(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is Win32Exception win32)
                return win32;

            ex = ex.InnerException;
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        if (_pollTask != null)
        {
            try
            {
                await _pollTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        lock (_interposerLock)
        {
            foreach (var interposer in _interposers)
            {
                try
                {
                    interposer.Dispose();
                }
                catch
                {
                    // Disposing tears down the event pipe to a process that may already be gone.
                }
            }

            _interposers.Clear();
        }

        _cts.Dispose();
    }
}
