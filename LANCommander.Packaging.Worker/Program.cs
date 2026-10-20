using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using LANCommander.Packaging;
using LANCommander.Packaging.Changes;
using LANCommander.Packaging.IPC;
using LANCommander.Packaging.Worker;

// Exit codes are for diagnostics only; the launcher learns about failures over the pipe.
const int ExitOk = 0;
const int ExitBadArguments = 2;
const int ExitConnectFailed = 3;
const int ExitHandshakeRejected = 4;

if (!OperatingSystem.IsWindows())
    return ExitBadArguments;

var options = WorkerOptions.Parse(args);

if (options == null)
    return ExitBadArguments;

return await RunAsync(options);

[SupportedOSPlatform("windows")]
static async Task<int> RunAsync(WorkerOptions options)
{
    using var shutdown = new CancellationTokenSource();

    // The worker must not outlive the launcher: an orphaned injector monitoring a user's
    // installer with nothing to report to is strictly worse than no worker at all.
    WatchHostProcess(options.HostProcessId, shutdown);

    WorkerConnection connection;

    try
    {
        connection = await WorkerConnection.ConnectAsync(
            options.PipeName, TimeSpan.FromSeconds(15), shutdown.Token);
    }
    catch (Exception)
    {
        return ExitConnectFailed;
    }

    await using (connection)
    {
        var architecture = ProcessArchitectureReader.Current;
        var interposerDllPath = ResolveInterposerDll(options.InterposerDllPath);

        var accepted = await connection.HandshakeAsync(
            new WorkerHelloMessage
            {
                ProtocolVersion = PackagingProtocol.Version,
                Token = options.Token,
                WorkerProcessId = Environment.ProcessId,
                Architecture = architecture,
                IsElevated = IsElevated(),
                InterposerDllPath = interposerDllPath,
            },
            shutdown.Token);

        if (!accepted)
            return ExitHandshakeRejected;

        // Started by an elevated sibling rather than the launcher, so this inherits elevation
        // without a second consent prompt.
        if (options.HasSpawnRequest)
            SpawnSiblingWorker(options, connection);

        await using var collector = new ChangeCollector(
            (batch, ct) => connection.SendAsync(batch, ct));

        await using var monitor = new InstallerMonitor(
            collector, connection, interposerDllPath, architecture);

        var reason = await PumpAsync(connection, collector, monitor, shutdown);

        await collector.FlushAsync(CancellationToken.None);

        try
        {
            await connection.SendAsync(new WorkerStoppedMessage { Reason = reason });
        }
        catch
        {
            // The launcher may already be gone, which is the usual reason we got here.
        }
    }

    return ExitOk;
}

/// <summary>
/// Reads commands until the launcher stops sending or asks the worker to stop.
/// </summary>
[SupportedOSPlatform("windows")]
static async Task<string> PumpAsync(
    WorkerConnection connection,
    ChangeCollector collector,
    InstallerMonitor monitor,
    CancellationTokenSource shutdown)
{
    try
    {
        while (!shutdown.IsCancellationRequested)
        {
            var message = await connection.ReceiveAsync(shutdown.Token);

            // A closed pipe means the launcher went away.
            if (message == null)
                return "Launcher disconnected.";

            switch (message)
            {
                case PingCommand ping:
                    await connection.SendAsync(new PongMessage { Sequence = ping.Sequence }, shutdown.Token);
                    break;

                case SetFilterCommand filter:
                    collector.Filter = new ChangeFilter
                    {
                        WriteVerbs = filter.WriteVerbs,
                        RegistryWriteVerbs = filter.RegistryWriteVerbs,
                        IgnoredPathPrefixes = filter.IgnoredPathPrefixes,
                        IncludeReads = filter.IncludeReads,
                    };
                    break;

                case LaunchInstallerCommand launch:
                    await connection.SendAsync(await monitor.LaunchAsync(launch), shutdown.Token);
                    break;

                case InjectCommand inject:
                    await connection.SendAsync(await monitor.InjectAsync(inject), shutdown.Token);
                    break;

                case AdoptSubtreeCommand adopt:
                    await connection.SendAsync(await monitor.AdoptSubtreeAsync(adopt), shutdown.Token);
                    break;

                // Handled inline and awaited, so a LaunchInstaller sent straight after this is
                // not processed until the old processes are actually gone.
                case TerminateProcessesCommand terminate:
                    await connection.SendAsync(await monitor.TerminateAsync(terminate), shutdown.Token);
                    break;

                case StopCommand stop:
                    if (stop.TerminateTargets)
                        await monitor.TerminateTargetsAsync(shutdown.Token);

                    return "Stopped by launcher.";
            }
        }
    }
    catch (OperationCanceledException)
    {
        return "Launcher process exited.";
    }
    catch (Exception ex)
    {
        return $"Channel error: {ex.Message}";
    }

    return "Shutting down.";
}

/// <summary>
/// Cancels <paramref name="shutdown"/> when the launcher exits.
/// </summary>
static void WatchHostProcess(int hostProcessId, CancellationTokenSource shutdown)
{
    try
    {
        var host = Process.GetProcessById(hostProcessId);

        host.EnableRaisingEvents = true;
        host.Exited += (_, _) => shutdown.Cancel();

        // Guard against the launcher having exited between the check and the subscription.
        if (host.HasExited)
            shutdown.Cancel();
    }
    catch (ArgumentException)
    {
        // Already gone.
        shutdown.Cancel();
    }
}

/// <summary>
/// Starts the other architecture's worker as a child so it inherits this process's elevation.
/// </summary>
static void SpawnSiblingWorker(WorkerOptions options, WorkerConnection connection)
{
    try
    {
        var startInfo = new ProcessStartInfo(options.SpawnWorkerPath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(options.SpawnWorkerPath!),
        };

        startInfo.ArgumentList.Add("--pipe");
        startInfo.ArgumentList.Add(options.SpawnWorkerPipeName!);
        startInfo.ArgumentList.Add("--token");
        startInfo.ArgumentList.Add(options.SpawnWorkerToken!);
        startInfo.ArgumentList.Add("--host-pid");
        startInfo.ArgumentList.Add(options.HostProcessId.ToString());

        Process.Start(startInfo);
    }
    catch (Exception ex)
    {
        _ = connection.LogAsync(
            PackagingLogLevel.Warning,
            $"Could not start the companion worker; processes of the other architecture will " +
            $"not be captured: {ex.Message}");
    }
}

/// <summary>
/// Resolves the native Interposer DLL, which the NuGet package places next to the worker for
/// the runtime identifier it was published under.
/// </summary>
static string? ResolveInterposerDll(string? explicitPath)
{
    if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
        return Path.GetFullPath(explicitPath);

    var baseDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    foreach (var candidate in new[] { "LANCommander.Interposer.dll", "interposer.dll" })
    {
        var path = Path.Combine(baseDirectory, candidate);

        if (File.Exists(path))
            return path;
    }

    // Null lets InterposerService fall back to its own probing and produce a clearer error.
    return null;
}

[SupportedOSPlatform("windows")]
static bool IsElevated()
{
    try
    {
        using var identity = WindowsIdentity.GetCurrent();

        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
        return false;
    }
}
