using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using LANCommander.Interposer;
using LANCommander.Packaging.IPC;

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

            // Report the root like any other process so the launcher's ledger knows about it.
            // Without this the installer we started ourselves is the one process the session
            // cannot later terminate.
            await _connection.SendAsync(new ProcessDiscoveredMessage
            {
                ProcessId = process.Id,
                ParentProcessId = Environment.ProcessId,
                ImagePath = executablePath,
                Architecture = _architecture,
                InjectedLocally = true,
            });

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
        interposer.FileAccessed += (_, e) => _collector.AddFile(e.Verb, e.Path, e.SecondaryPath, 0);

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

                var code = FindWin32Exception(ex)?.NativeErrorCode ?? 0;

                message.InjectionError = ex.Message;
                message.Win32Error = code;

                // Both mean the target is out of this worker's reach rather than wrong in some
                // way. The launcher turns this into the offer to restart with elevation.
                message.RequiresElevation =
                    code is NativeMethods.ErrorAccessDenied or NativeMethods.ErrorElevationRequired;
            }
        }

        await _connection.SendAsync(message, cancellationToken);

        // Report child exits too, not just the root's. The launcher decides whether a capture
        // is finished by whether anything it discovered is still running, and an installer that
        // hands off to an elevated copy relies on exactly that.
        try
        {
            WatchForExit(Process.GetProcessById(entry.ProcessId), isRoot: false);
        }
        catch (ArgumentException)
        {
            // Exited between discovery and here; report it so the ledger does not hold it open.
            await _connection.SendAsync(
                new ProcessExitedMessage { ProcessId = entry.ProcessId, ExitCode = 0 },
                cancellationToken);
        }
    }

    /// <summary>
    /// Kills the processes being monitored and waits for them to actually go away.
    /// </summary>
    /// <remarks>
    /// Used when the session is about to relaunch the same installer elevated. Returning before
    /// the old run has exited would leave two copies of the installer writing to the same place.
    /// </remarks>
    public Task TerminateTargetsAsync(CancellationToken cancellationToken = default) =>
        // Everything ever reported, not just what was injected into: a process we failed to
        // inject into (an installer that self-elevated, typically) is precisely the one most
        // likely to still be running.
        TerminateProcessesAsync(
            _watchedRoots.Keys.Concat(_injected.Keys).Concat(_reported.Keys).Distinct(),
            cancellationToken);

    /// <summary>
    /// Kills the given processes and waits for them to exit.
    /// </summary>
    public async Task<CommandResultMessage> TerminateAsync(TerminateProcessesCommand command)
    {
        try
        {
            await TerminateProcessesAsync(command.ProcessIds, CancellationToken.None);

            return new CommandResultMessage
            {
                CorrelationId = command.CorrelationId,
                Success = true,
            };
        }
        catch (Exception ex)
        {
            return Failure(command.CorrelationId, ex);
        }
    }

    private async Task TerminateProcessesAsync(
        IEnumerable<int> processIds, CancellationToken cancellationToken)
    {
        var targets = processIds.Distinct().ToList();

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

            // Report the exit explicitly. The worker that had been watching these has already
            // been torn down, so nothing else will — and the launcher treats a process it still
            // believes is running as a reason to keep monitoring after the install has finished.
            try
            {
                await _connection.SendAsync(
                    new ProcessExitedMessage { ProcessId = processId, ExitCode = 0 },
                    cancellationToken);
            }
            catch (Exception)
            {
                // The channel is torn down elsewhere; nothing useful to do here.
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
