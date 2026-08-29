using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using LANCommander.Packaging;
using LANCommander.Packaging.Ipc;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Packaging;

/// <summary>
/// Starts the per-architecture worker processes and hands the session connected channels.
/// </summary>
public class PackagingWorkerFactory : IPackagingWorkerFactory
{
    /// <summary>Directory the CI publish step places the workers into, beside the apphost.</summary>
    public const string WorkersDirectoryName = "Packaging";

    public const string WorkerExecutableName = "LANCommander.Packaging.Worker.exe";

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
        [.. Architectures.Where(a => ResolveWorkerPath(a) != null)];

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
            var resolved = ResolveWorkerPath(architecture);

            if (resolved != null)
            {
                _logger.LogInformation(
                    "Packaging worker found: {Architecture} at {Path}", architecture, resolved);

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
    /// Directories the workers may sit in, most likely first.
    /// </summary>
    /// <remarks>
    /// Both are needed, because neither is correct on its own:
    /// <list type="bullet">
    /// <item><see cref="AppContext.BaseDirectory"/> is the application's own directory, including
    /// under single-file publish, but is not what a host-launched process reports.</item>
    /// <item><see cref="Environment.ProcessPath"/> is the actual executable — which is
    /// <c>dotnet.exe</c> when the launcher is started through the shared host rather than its
    /// apphost, and then points at the SDK install rather than the app.</item>
    /// </list>
    /// </remarks>
    private static IEnumerable<string> GetCandidateBaseDirectories()
    {
        var appBase = AppContext.BaseDirectory;

        if (!string.IsNullOrEmpty(appBase))
            yield return appBase;

        var processDirectory = Path.GetDirectoryName(Environment.ProcessPath);

        if (!string.IsNullOrEmpty(processDirectory) &&
            !string.Equals(
                Path.TrimEndingDirectorySeparator(processDirectory),
                Path.TrimEndingDirectorySeparator(appBase ?? string.Empty),
                StringComparison.OrdinalIgnoreCase))
        {
            yield return processDirectory;
        }
    }

    /// <summary>
    /// Every location a worker for <paramref name="architecture"/> could be deployed to.
    /// </summary>
    public static IEnumerable<string> GetCandidateWorkerPaths(ProcessArchitecture architecture)
    {
        var runtimeIdentifier = ProcessArchitectureReader.GetWorkerRuntimeIdentifier(architecture);

        if (runtimeIdentifier == null)
            yield break;

        foreach (var baseDirectory in GetCandidateBaseDirectories())
            yield return Path.Combine(baseDirectory, WorkersDirectoryName, runtimeIdentifier, WorkerExecutableName);
    }

    /// <summary>
    /// The deployed worker for <paramref name="architecture"/>, or null when none is present.
    /// </summary>
    public static string? ResolveWorkerPath(ProcessArchitecture architecture) =>
        GetCandidateWorkerPaths(architecture).FirstOrDefault(File.Exists);

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

        var pending = available.Select(architecture =>
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
        var workerPath = ResolveWorkerPath(worker.Architecture);

        if (workerPath == null)
        {
            _logger.LogWarning(
                "No {Architecture} packaging worker is deployed; that architecture will not be captured",
                worker.Architecture);

            return null;
        }

        var startInfo = new ProcessStartInfo(workerPath)
        {
            WorkingDirectory = Path.GetDirectoryName(workerPath),
            // ShellExecute is required for the runas verb, and forbidden without it if we want
            // to suppress a console window.
            UseShellExecute = elevated,
            CreateNoWindow = !elevated,
            Verb = elevated ? "runas" : string.Empty,
        };

        var arguments = new List<string>
        {
            "--pipe", worker.PipeName,
            "--token", worker.Token,
            "--host-pid", Environment.ProcessId.ToString(),
        };

        // Only one companion is ever needed today (x86 alongside x64), and the worker CLI
        // accepts a single spawn request.
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
