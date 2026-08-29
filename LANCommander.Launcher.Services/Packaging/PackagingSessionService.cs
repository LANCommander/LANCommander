using System.Collections.Concurrent;
using LANCommander.Packaging;
using LANCommander.Packaging.Changes;
using LANCommander.Packaging.Ipc;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Packaging;

/// <summary>
/// Aggregates every worker's reports into one change set and routes injection work to the
/// worker whose architecture matches.
/// </summary>
public class PackagingSessionService : IPackagingSessionService
{
    private readonly IPackagingWorkerFactory _workerFactory;
    private readonly ILogger<PackagingSessionService> _logger;

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    // Keyed stores are what make cross-worker deduplication fall out for free: the same file
    // seen by two workers collapses to one entry because it normalizes to the same key.
    private readonly ConcurrentDictionary<string, FileChange> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RegistryChange> _registry = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, ProcessLedgerEntry> _processes = new();

    private readonly List<IPackagingWorkerChannel> _workers = [];
    private readonly ConcurrentQueue<string> _pendingLogs = new();

    private CancellationTokenSource? _sessionCts;
    private Task? _counterTask;
    private PackagingSessionOptions? _options;
    private Guid _sessionId;
    private int _droppedEvents;
    private int _elevationRequested;
    private volatile bool _elevated;

    public PackagingSessionService(
        IPackagingWorkerFactory workerFactory,
        ILogger<PackagingSessionService> logger)
    {
        _workerFactory = workerFactory;
        _logger = logger;
    }

    public bool IsSupported => _workerFactory.IsSupported;

    public PackagingSessionState State { get; private set; } = PackagingSessionState.Idle;

    public event EventHandler<PackagingCounters>? CountersChanged;
    public event EventHandler<string>? Logged;
    public event EventHandler? InstallerExited;
    public event EventHandler<string>? ElevationRequired;

    public async Task StartAsync(
        PackagingSessionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsSupported)
            throw new PlatformNotSupportedException(
                "Packaging requires Windows and the packaging workers to be installed alongside the launcher.");

        await _lifecycleLock.WaitAsync(cancellationToken);

        try
        {
            if (State is PackagingSessionState.Starting or PackagingSessionState.Monitoring)
                throw new InvalidOperationException("A packaging session is already running.");

            _options = options;
            _sessionId = Guid.NewGuid();
            _elevated = false;

            Interlocked.Exchange(ref _elevationRequested, 0);

            await StartInternalAsync(elevated: false, cancellationToken);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task RestartElevatedAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);

        try
        {
            if (_options == null)
                throw new InvalidOperationException("There is no session to restart.");

            if (_elevated)
                return;

            // The installer is about to be launched again from scratch, so the un-elevated run
            // has to go. Leaving it would put two copies of the same installer on screen, both
            // writing to the same place.
            await TearDownWorkersAsync(terminateTargets: true);

            // Anything captured before escalation came from the same installer, so it is kept;
            // the keyed stores make re-reported changes collapse rather than duplicate.
            await StartInternalAsync(elevated: true, cancellationToken);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StartInternalAsync(bool elevated, CancellationToken cancellationToken)
    {
        State = PackagingSessionState.Starting;

        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _elevated = elevated;

        // Both workers start up front rather than on demand. Spawning the second one lazily
        // would put process creation and a handshake on the critical path at the exact moment a
        // short-lived child process needs instrumenting, and it would usually lose that race.
        var workers = await _workerFactory.StartWorkersAsync(_sessionId, elevated, cancellationToken);

        if (workers.Count == 0)
        {
            State = PackagingSessionState.Failed;

            throw new InvalidOperationException("No packaging workers could be started.");
        }

        lock (_workers)
        {
            _workers.Clear();
            _workers.AddRange(workers);
        }

        foreach (var worker in workers)
        {
            worker.MessageReceived += OnWorkerMessage;
            worker.Faulted += OnWorkerFaulted;

            await worker.SendAsync(BuildFilterCommand(), cancellationToken);
        }

        _counterTask ??= Task.Run(() => PublishCountersAsync(_sessionCts.Token));

        var launchTarget = ResolveLaunchWorker(workers);

        await launchTarget.SendAsync(
            new LaunchInstallerCommand
            {
                CorrelationId = Guid.NewGuid(),
                ExecutablePath = _options!.InstallerPath,
                Arguments = _options.Arguments,
                WorkingDirectory = _options.WorkingDirectory,
            },
            cancellationToken);

        State = PackagingSessionState.Monitoring;
    }

    /// <summary>
    /// Picks the worker that can launch the installer: the one matching the installer's own
    /// architecture, falling back to any available worker.
    /// </summary>
    private IPackagingWorkerChannel ResolveLaunchWorker(IReadOnlyList<IPackagingWorkerChannel> workers)
    {
        var architecture = ProcessArchitectureReader.FromImage(_options!.InstallerPath);

        return workers.FirstOrDefault(w => w.Architecture == architecture) ?? workers[0];
    }

    private SetFilterCommand BuildFilterCommand() => new()
    {
        WriteVerbs = [.. ChangeFilter.DefaultWriteVerbs],
        RegistryWriteVerbs = [.. ChangeFilter.DefaultRegistryWriteVerbs],
        IgnoredPathPrefixes = ChangeFilter.BuildDefaultIgnoredPathPrefixes(),
        IncludeReads = _options?.IncludeReads ?? false,
    };

    private void OnWorkerMessage(object? sender, PackagingMessage message)
    {
        if (sender is not IPackagingWorkerChannel worker)
            return;

        switch (message)
        {
            case ChangeBatchMessage batch:
                Merge(batch);
                break;

            case ProcessDiscoveredMessage discovered:
                _ = HandleProcessDiscoveredAsync(discovered);
                break;

            case ProcessExitedMessage exited:
                HandleProcessExited(exited);
                break;

            case CommandResultMessage result:
                HandleCommandResult(worker, result);
                break;

            case WorkerLogMessage log:
                Log(log.Level, log.Message);
                break;

            case WorkerStoppedMessage stopped:
                Log(PackagingLogLevel.Information, $"Worker stopped: {stopped.Reason}");
                break;
        }
    }

    private void Merge(ChangeBatchMessage batch)
    {
        foreach (var file in batch.Files)
        {
            if (!string.IsNullOrEmpty(file.Path))
                _files[file.Path] = file;
        }

        foreach (var entry in batch.Registry)
        {
            // Architecture is part of the key: the same key path written from both a 32-bit and
            // a 64-bit process lands in two different physical places and needs two script lines.
            var key = $"{entry.KeyPath} {entry.ValueName} {(int)entry.SourceArchitecture}";

            _registry[key] = entry;
        }

        if (batch.DroppedCount > 0)
            Interlocked.Add(ref _droppedEvents, batch.DroppedCount);
    }

    /// <summary>
    /// Records a discovered process and, when the reporting worker could not inject into it,
    /// routes the injection to the worker that can. This is the cross-architecture handoff.
    /// </summary>
    private async Task HandleProcessDiscoveredAsync(ProcessDiscoveredMessage discovered)
    {
        var entry = _processes.GetOrAdd(discovered.ProcessId, _ => new ProcessLedgerEntry
        {
            ProcessId = discovered.ProcessId,
            ParentProcessId = discovered.ParentProcessId,
            ImagePath = discovered.ImagePath,
            Architecture = discovered.Architecture,
        });

        if (discovered.InjectedLocally)
        {
            entry.Instrumented = true;
            entry.InstrumentationError = null;

            return;
        }

        if (entry.Instrumented)
            return;

        entry.InstrumentationError = discovered.InjectionError;

        var target = FindWorker(discovered.Architecture);

        if (target == null)
        {
            entry.InstrumentationError ??= discovered.Architecture == ProcessArchitecture.Unknown
                ? "The process architecture could not be determined."
                : $"No {discovered.Architecture} packaging worker is available.";

            Log(PackagingLogLevel.Warning,
                $"PID {discovered.ProcessId} ({Path.GetFileName(discovered.ImagePath)}) could not be " +
                $"instrumented: {entry.InstrumentationError}");

            return;
        }

        // Both workers poll the same subtrees, so the same child is routinely discovered twice
        // within milliseconds. Only the first report gets to route it.
        if (!entry.TryBeginInjection())
            return;

        try
        {
            await target.SendAsync(new InjectCommand
            {
                CorrelationId = Guid.NewGuid(),
                ProcessId = discovered.ProcessId,
            });
        }
        catch (Exception ex)
        {
            entry.InstrumentationError = ex.Message;

            // The command never reached a worker, so let a later discovery try again.
            entry.ReleaseInjectionClaim();

            _logger.LogWarning(ex, "Could not route injection for PID {ProcessId}", discovered.ProcessId);
        }
    }

    private void HandleProcessExited(ProcessExitedMessage exited)
    {
        if (_processes.TryGetValue(exited.ProcessId, out var entry))
            entry.HasExited = true;

        if (exited.IsRoot)
            InstallerExited?.Invoke(this, EventArgs.Empty);
    }

    private void HandleCommandResult(IPackagingWorkerChannel worker, CommandResultMessage result)
    {
        if (result.Success)
        {
            if (result.ProcessId is { } processId && _processes.TryGetValue(processId, out var entry))
            {
                entry.Instrumented = true;
                entry.InstrumentationError = null;
            }

            return;
        }

        if (result.ProcessId is { } failedProcessId &&
            _processes.TryGetValue(failedProcessId, out var failedEntry))
        {
            failedEntry.InstrumentationError = result.Error;
        }

        if (result.RequiresElevation && !_elevated)
        {
            // Raise this once per session; a failing installer can produce a long stream of
            // access-denied results and the user only needs to be asked once.
            if (Interlocked.Exchange(ref _elevationRequested, 1) == 0)
            {
                Log(PackagingLogLevel.Warning,
                    "The installer requires administrator rights. Packaging needs to restart with elevation.");

                ElevationRequired?.Invoke(
                    this,
                    result.Error ?? "The installer requires administrator rights to be monitored.");
            }

            return;
        }

        Log(PackagingLogLevel.Error, result.Error ?? "A packaging worker command failed.");
    }

    private void OnWorkerFaulted(object? sender, string reason)
    {
        var architecture = (sender as IPackagingWorkerChannel)?.Architecture;

        // A dead worker degrades the capture rather than ending it: whatever the other workers
        // are still watching keeps being recorded.
        Log(PackagingLogLevel.Warning,
            $"The {architecture} packaging worker stopped responding ({reason}). " +
            "Processes of that architecture will no longer be captured.");
    }

    private IPackagingWorkerChannel? FindWorker(ProcessArchitecture architecture)
    {
        if (architecture == ProcessArchitecture.Unknown)
            return null;

        lock (_workers)
            return _workers.FirstOrDefault(w => w.Architecture == architecture && w.IsConnected);
    }

    private void Log(PackagingLogLevel level, string message)
    {
        _logger.Log(
            level switch
            {
                PackagingLogLevel.Debug => LogLevel.Debug,
                PackagingLogLevel.Warning => LogLevel.Warning,
                PackagingLogLevel.Error => LogLevel.Error,
                _ => LogLevel.Information,
            },
            "[Packaging] {Message}",
            message);

        if (level >= PackagingLogLevel.Information)
            _pendingLogs.Enqueue(message);
    }

    /// <summary>
    /// Publishes counters on a timer rather than per event. A busy install produces tens of
    /// thousands of changes and raising an event for each would swamp any subscriber.
    /// </summary>
    private async Task PublishCountersAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                CountersChanged?.Invoke(this, BuildCounters());

                while (_pendingLogs.TryDequeue(out var line))
                    Logged?.Invoke(this, line);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private PackagingCounters BuildCounters()
    {
        var processes = _processes.Values.ToList();

        return new PackagingCounters
        {
            FileCount = _files.Count,
            RegistryCount = _registry.Count,
            ProcessCount = processes.Count,
            UninstrumentedProcessCount = processes.Count(p => !p.Instrumented),
            DroppedEventCount = Volatile.Read(ref _droppedEvents),
            State = State,
            AnyWorkerElevated = _elevated,
        };
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);

        try
        {
            if (State is PackagingSessionState.Idle or PackagingSessionState.Stopped)
                return;

            State = PackagingSessionState.Stopping;

            await TearDownWorkersAsync();

            if (_sessionCts != null)
            {
                await _sessionCts.CancelAsync();

                _sessionCts.Dispose();
                _sessionCts = null;
            }

            if (_counterTask != null)
            {
                try
                {
                    await _counterTask;
                }
                catch (OperationCanceledException)
                {
                }

                _counterTask = null;
            }

            State = PackagingSessionState.Stopped;

            CountersChanged?.Invoke(this, BuildCounters());
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <param name="terminateTargets">
    /// Kill the processes being monitored. Only when the session is about to relaunch the same
    /// installer; a normal stop leaves it alone because the user may be mid-install.
    /// </param>
    private async Task TearDownWorkersAsync(bool terminateTargets = false)
    {
        List<IPackagingWorkerChannel> workers;

        lock (_workers)
        {
            workers = [.. _workers];

            _workers.Clear();
        }

        foreach (var worker in workers)
        {
            worker.MessageReceived -= OnWorkerMessage;
            worker.Faulted -= OnWorkerFaulted;

            try
            {
                await worker.SendAsync(new StopCommand { TerminateTargets = terminateTargets });
            }
            catch
            {
                // The worker may already be gone; disposal below is what actually matters.
            }

            try
            {
                await worker.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing a packaging worker");
            }
        }
    }

    public PackagingSessionSnapshot Snapshot() => new()
    {
        Files = [.. _files.Values],
        Registry = [.. _registry.Values],
        Processes = [.. _processes.Values],
        DroppedEventCount = Volatile.Read(ref _droppedEvents),
        State = State,
    };

    public void Reset()
    {
        _files.Clear();
        _registry.Clear();
        _processes.Clear();
        _pendingLogs.Clear();

        Interlocked.Exchange(ref _droppedEvents, 0);
        Interlocked.Exchange(ref _elevationRequested, 0);

        _options = null;
        _elevated = false;

        State = PackagingSessionState.Idle;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        try
        {
            await StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error stopping the packaging session during disposal");
        }

        _lifecycleLock.Dispose();
    }
}
