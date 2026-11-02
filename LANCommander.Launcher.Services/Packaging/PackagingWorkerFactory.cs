using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using LANCommander.Packaging;
using LANCommander.Packaging.IPC;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Packaging;

/// <summary>
/// Starts the per-architecture worker processes and hands the session connected channels.
/// </summary>
/// <remarks>
/// The worker for the launcher's own architecture is the launcher itself, started again with
/// <see cref="LauncherWorkerArgument"/>, when the host has opted in with
/// <see cref="LauncherHostsWorker"/>. Only the other architecture needs a separate worker, under
/// <c>Data\Libraries\Packaging\{arch}\</c>.
/// </remarks>
public class PackagingWorkerFactory : IPackagingWorkerFactory
{
    public const string WorkerExecutableName = "LANCommander.Packaging.Worker.exe";

    /// <summary>Starts the launcher as a packaging worker instead of its window.</summary>
    public const string LauncherWorkerArgument = "--packaging-worker";

    /// <summary>
    /// Whether the running executable handles <see cref="LauncherWorkerArgument"/>. Set by the
    /// launcher's entry point; any other host that uses this factory (tests, tools) would otherwise
    /// start itself as a worker that never connects.
    /// </summary>
    public static bool LauncherHostsWorker { get; set; }

    private static readonly ProcessArchitecture[] Architectures =
    [
        ProcessArchitecture.X64,
        ProcessArchitecture.X86,
    ];

    private readonly ILogger<PackagingWorkerFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public PackagingWorkerFactory(ILogger<PackagingWorkerFactory> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public bool IsSupported =>
        OperatingSystem.IsWindows() && SupportedArchitectures.Count > 0;

    public IReadOnlyList<ProcessArchitecture> SupportedArchitectures =>
        [.. Architectures.Where(a => ResolveWorker(a) != null)];

    /// <summary>
    /// Logs which workers were found, once, at startup.
    /// </summary>
    /// <remarks>
    /// Worth doing explicitly: worker discovery works under <c>dotnet run</c> and can silently
    /// fail in a single-file publish where the files were never copied, and the only symptom
    /// would be a packaging menu entry that never appears.
    /// </remarks>
    public void LogDiscovery()
    {
        if (!OperatingSystem.IsWindows())
            return;

        foreach (var architecture in Architectures)
        {
            var resolved = ResolveWorker(architecture);

            if (resolved != null)
            {
                _logger.LogInformation(
                    "Packaging worker found: {Architecture} at {Path}", architecture, resolved.FileName);

                continue;
            }

            // List every location that was checked; a worker in none of them usually means the
            // build did not produce them rather than that they are in the wrong place.
            _logger.LogWarning(
                "Packaging worker missing: {Architecture}. Looked in: {Paths}",
                architecture,
                string.Join(", ", GetCandidateWorkerPaths(architecture)));
        }
    }

    /// <summary>
    /// Every location a separate worker for <paramref name="architecture"/> could be deployed to.
    /// </summary>
    public static IEnumerable<string> GetCandidateWorkerPaths(ProcessArchitecture architecture)
    {
        var directoryName = LauncherLibraries.GetArchitectureDirectoryName(architecture);

        if (directoryName == null)
            yield break;

        foreach (var root in LauncherLibraries.GetCandidateRoots())
            yield return Path.Combine(root, LauncherLibraries.PackagingDirectoryName, directoryName, WorkerExecutableName);
    }

    /// <summary>
    /// The deployed worker for <paramref name="architecture"/>, or null when none is present.
    /// </summary>
    public static string? ResolveWorkerPath(ProcessArchitecture architecture) =>
        GetCandidateWorkerPaths(architecture).FirstOrDefault(File.Exists);

    /// <summary>
    /// How to start the worker for <paramref name="architecture"/>: the launcher itself when it
    /// is that architecture, otherwise the separate worker. Null when neither is available.
    /// </summary>
    public static WorkerCommand? ResolveWorker(ProcessArchitecture architecture)
    {
        if (architecture == ProcessArchitectureReader.Current &&
            LauncherLibraries.GetArchitectureDirectoryName(architecture) != null &&
            GetLauncherWorkerCommand() is { } launcher)
        {
            return launcher;
        }

        var path = ResolveWorkerPath(architecture);

        return path == null ? null : new WorkerCommand(path, [], IsLauncher: false);
    }

    private static WorkerCommand? GetLauncherWorkerCommand()
    {
        var processPath = Environment.ProcessPath;

        if (!LauncherHostsWorker || string.IsNullOrEmpty(processPath))
            return null;

        // Started through the shared host, the launcher is an argument to dotnet.exe
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.Location;

            return string.IsNullOrEmpty(entryAssembly)
                ? null
                : new WorkerCommand(processPath, [entryAssembly, LauncherWorkerArgument], IsLauncher: true);
        }

        return new WorkerCommand(processPath, [LauncherWorkerArgument], IsLauncher: true);
    }

    public async Task<IReadOnlyList<IPackagingWorkerChannel>> StartWorkersAsync(
        Guid sessionId, bool elevated, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return [];

        return await StartWindowsWorkersAsync(sessionId, elevated, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    private async Task<IReadOnlyList<IPackagingWorkerChannel>> StartWindowsWorkersAsync(
        Guid sessionId, bool elevated, CancellationToken cancellationToken)
    {
        var available = SupportedArchitectures;

        if (available.Count == 0)
        {
            _logger.LogError(
                "No packaging workers were found. Looked in: {Paths}",
                string.Join(
                    ", ",
                    Architectures.SelectMany(GetCandidateWorkerPaths)));

            return [];
        }

        // The launcher's own worker goes first so that, when elevating, it is the one that goes
        // through UAC and starts the separate worker as its companion; the companion is then
        // always a plain executable.
        var pending = available.OrderByDescending(a => ResolveWorker(a)?.IsLauncher == true).Select(architecture =>
        {
            var pipeName = PackagingProtocol.BuildPipeName(
                Environment.ProcessId, sessionId, DeterministicWorkerId(sessionId, architecture));

            return new PendingWorker(architecture, pipeName, CreatePipe(pipeName));
        }).ToList();

        try
        {
            LaunchProcesses(pending, elevated);

            var channels = new List<IPackagingWorkerChannel>();

            foreach (var worker in pending)
            {
                var channel = await AcceptAsync(worker, cancellationToken);

                if (channel != null)
                    channels.Add(channel);
            }

            // Dispose the pipes of any worker that never showed up.
            foreach (var worker in pending.Where(w => !w.Accepted))
                await worker.Pipe.DisposeAsync();

            return channels;
        }
        catch
        {
            foreach (var worker in pending.Where(w => !w.Accepted))
                await worker.Pipe.DisposeAsync();

            throw;
        }
    }

    /// <summary>
    /// Starts the worker processes.
    /// </summary>
    /// <remarks>
    /// When elevating, only the first worker goes through UAC; it is told to start the others
    /// as its own children, which inherit its elevation. That is what keeps a session to a
    /// single consent prompt instead of one per architecture.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private void LaunchProcesses(List<PendingWorker> pending, bool elevated)
    {
        if (!elevated)
        {
            foreach (var worker in pending)
                worker.Process = StartWorkerProcess(worker, companions: [], elevated: false);

            return;
        }

        var primary = pending[0];
        var companions = pending.Skip(1).ToList();

        primary.Process = StartWorkerProcess(primary, companions, elevated: true);
    }

    [SupportedOSPlatform("windows")]
    private Process? StartWorkerProcess(PendingWorker worker, List<PendingWorker> companions, bool elevated)
    {
        var command = ResolveWorker(worker.Architecture);

        if (command == null)
        {
            _logger.LogWarning(
                "No {Architecture} packaging worker is deployed; that architecture will not be captured",
                worker.Architecture);

            return null;
        }

        var startInfo = new ProcessStartInfo(command.FileName)
        {
            WorkingDirectory = Path.GetDirectoryName(command.FileName),
            // ShellExecute is required for the runas verb, and forbidden without it if we want
            // to suppress a console window.
            UseShellExecute = elevated,
            CreateNoWindow = !elevated,
            Verb = elevated ? "runas" : string.Empty,
        };

        var arguments = new List<string>(command.Arguments)
        {
            "--pipe", worker.PipeName,
            "--token", worker.Token,
            "--host-pid", Environment.ProcessId.ToString(),
        };

        // Only one companion is ever needed today (x86 alongside x64), and the worker CLI
        // accepts a single spawn request. It is started by path alone, so it has to be a separate
        // worker rather than the launcher, which the ordering above guarantees.
        var companion = companions.FirstOrDefault();

        var companionPath = companion == null ? null : ResolveWorkerPath(companion.Architecture);

        if (companion != null && companionPath != null)
        {
            arguments.AddRange(
            [
                "--spawn-worker", companionPath,
                "--spawn-worker-pipe", companion.PipeName,
                "--spawn-worker-token", companion.Token,
            ]);
        }

        if (startInfo.UseShellExecute)
            startInfo.Arguments = string.Join(' ', arguments.Select(Quote));
        else
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

        try
        {
            return Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            // A declined UAC prompt lands here. The session falls back to whatever connected,
            // and warns the user that self-elevating installers will not be captured.
            _logger.LogWarning(
                ex, "Could not start the {Architecture} packaging worker", worker.Architecture);

            return null;
        }
    }

    private static string Quote(string argument) =>
        argument.Contains(' ') ? $"\"{argument}\"" : argument;

    [SupportedOSPlatform("windows")]
    private async Task<IPackagingWorkerChannel?> AcceptAsync(
        PendingWorker worker, CancellationToken cancellationToken)
    {
        try
        {
            var channel = await NamedPipeWorkerChannel.AcceptAsync(
                worker.Pipe,
                worker.Token,
                _loggerFactory.CreateLogger<NamedPipeWorkerChannel>(),
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (channel == null)
                return null;

            worker.Accepted = true;

            if (worker.Process != null)
                channel.AttachProcess(worker.Process);

            _logger.LogInformation(
                "Packaging worker connected: {Architecture}, elevated {Elevated}",
                channel.Architecture, channel.IsElevated);

            return channel;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "The {Architecture} packaging worker did not connect in time", worker.Architecture);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "The {Architecture} packaging worker failed to connect", worker.Architecture);

            return null;
        }
    }

    /// <summary>
    /// Creates the listening pipe for one worker.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreatePipe(string name)
    {
        var security = new PipeSecurity();

        var currentUser = WindowsIdentity.GetCurrent().User;

        if (currentUser != null)
        {
            security.AddAccessRule(new PipeAccessRule(
                currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
        }

        // The Administrators rule is load-bearing, not defensive. Under over-the-shoulder UAC a
        // standard user supplies an administrator's credentials, so the elevated worker runs as
        // a different account entirely and could not open a pipe granted only to the launching
        // user. Without this, packaging silently fails for every non-administrator.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            name,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            security);
    }

    private static Guid DeterministicWorkerId(Guid sessionId, ProcessArchitecture architecture)
    {
        var bytes = sessionId.ToByteArray();

        bytes[0] ^= (byte)architecture;

        return new Guid(bytes);
    }

    /// <summary>
    /// An executable and the arguments that make it a worker, ahead of the per-session ones.
    /// </summary>
    public sealed record WorkerCommand(string FileName, IReadOnlyList<string> Arguments, bool IsLauncher);

    private sealed class PendingWorker(
        ProcessArchitecture architecture, string pipeName, NamedPipeServerStream pipe)
    {
        public ProcessArchitecture Architecture { get; } = architecture;

        public NamedPipeServerStream Pipe { get; } = pipe;

        public string PipeName { get; } = pipeName;

        public string Token { get; } = Guid.NewGuid().ToString("N");

        public Process? Process { get; set; }

        public bool Accepted { get; set; }
    }
}
