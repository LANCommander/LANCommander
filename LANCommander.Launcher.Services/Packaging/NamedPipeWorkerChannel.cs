using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using LANCommander.Packaging;
using LANCommander.Packaging.Ipc;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Packaging;

/// <summary>
/// A worker connected over a named pipe.
/// </summary>
/// <remarks>
/// The launcher is the pipe <em>server</em> and the worker is the client. That direction is
/// load-bearing: an elevated worker has to be able to reach a medium-integrity launcher, and a
/// high-integrity process can open a lower-integrity process's pipe but not the reverse.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class NamedPipeWorkerChannel : IPackagingWorkerChannel
{
    private readonly NamedPipeServerStream _pipe;
    private readonly PackagingMessageChannel _channel;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    private Process? _process;
    private Task? _readTask;
    private Task? _pingTask;
    private long _pingSequence;
    private long _lastPongSequence;
    private int _missedPings;
    private int _faulted;

    private NamedPipeWorkerChannel(
        NamedPipeServerStream pipe,
        WorkerHelloMessage hello,
        ILogger logger)
    {
        _pipe = pipe;
        _channel = new PackagingMessageChannel(pipe, leaveOpen: true);
        _logger = logger;

        Architecture = hello.Architecture;
        IsElevated = hello.IsElevated;
        WorkerProcessId = hello.WorkerProcessId;
    }

    public ProcessArchitecture Architecture { get; }

    public bool IsElevated { get; }

    public int WorkerProcessId { get; }

    public bool IsConnected => _pipe.IsConnected && Volatile.Read(ref _faulted) == 0;

    public event EventHandler<PackagingMessage>? MessageReceived;

    public event EventHandler<string>? Faulted;

    /// <summary>
    /// Waits for a worker to connect to <paramref name="pipe"/> and completes the handshake.
    /// </summary>
    /// <returns>The connected channel, or null when the handshake was rejected.</returns>
    public static async Task<NamedPipeWorkerChannel?> AcceptAsync(
        NamedPipeServerStream pipe,
        string expectedToken,
        ILogger logger,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(timeout);

        await pipe.WaitForConnectionAsync(timeoutCts.Token);

        var handshakeChannel = new PackagingMessageChannel(pipe, leaveOpen: true);

        var hello = await handshakeChannel.ReadAsync(timeoutCts.Token) as WorkerHelloMessage;

        var rejection = Validate(hello, expectedToken);

        if (rejection != null)
        {
            await handshakeChannel.WriteAsync(
                new HostHelloMessage { Accepted = false, RejectionReason = rejection },
                CancellationToken.None);

            handshakeChannel.Dispose();

            logger.LogError("Rejected a packaging worker: {Reason}", rejection);

            return null;
        }

        await handshakeChannel.WriteAsync(
            new HostHelloMessage
            {
                Accepted = true,
                NegotiatedVersion = PackagingProtocol.Version,
            },
            timeoutCts.Token);

        handshakeChannel.Dispose();

        var channel = new NamedPipeWorkerChannel(pipe, hello!, logger);

        channel.Start();

        return channel;
    }

    private static string? Validate(WorkerHelloMessage? hello, string expectedToken)
    {
        if (hello == null)
            return "The worker did not send a handshake.";

        // An in-place launcher update can leave a stale worker binary on disk. Failing loudly
        // here turns silent protocol corruption into a clear error.
        if (hello.ProtocolVersion != PackagingProtocol.Version)
            return $"Protocol version mismatch: worker speaks {hello.ProtocolVersion}, " +
                   $"launcher speaks {PackagingProtocol.Version}. The worker binary is out of date.";

        if (!FixedTimeEquals(hello.Token, expectedToken))
            return "The worker presented an invalid session token.";

        if (hello.Architecture is not (ProcessArchitecture.X86 or ProcessArchitecture.X64))
            return $"Unsupported worker architecture '{hello.Architecture}'.";

        return null;
    }

    /// <summary>
    /// Length-independent comparison so token validation does not leak its length by timing.
    /// </summary>
    private static bool FixedTimeEquals(string? left, string? right)
    {
        if (left == null || right == null)
            return false;

        var difference = left.Length ^ right.Length;

        for (var i = 0; i < left.Length && i < right.Length; i++)
            difference |= left[i] ^ right[i];

        return difference == 0;
    }

    /// <summary>
    /// Associates the OS process so its death is noticed even if the pipe does not break.
    /// </summary>
    public void AttachProcess(Process process)
    {
        _process = process;

        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Fault("the worker process exited");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not watch the packaging worker process for exit");
        }
    }

    private void Start()
    {
        _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
        _pingTask = Task.Run(() => PingLoopAsync(_cts.Token));
    }

    public Task SendAsync(PackagingMessage message, CancellationToken cancellationToken = default) =>
        _channel.WriteAsync(message, cancellationToken);

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await _channel.ReadAsync(cancellationToken);

                if (message == null)
                {
                    Fault("the worker closed the connection");

                    return;
                }

                if (message is PongMessage pong)
                {
                    Interlocked.Exchange(ref _lastPongSequence, pong.Sequence);
                    Interlocked.Exchange(ref _missedPings, 0);

                    continue;
                }

                MessageReceived?.Invoke(this, message);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Fault(ex.Message);
        }
    }

    /// <summary>
    /// Detects a worker that is alive but wedged.
    /// </summary>
    /// <remarks>
    /// Deliberately does not kill an unresponsive worker. Terminating one mid-injection can
    /// leave the installer suspended forever, which is strictly worse than a stalled indicator.
    /// </remarks>
    private async Task PingLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PackagingProtocol.PingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var sequence = Interlocked.Increment(ref _pingSequence);

                await SendAsync(new PingCommand { Sequence = sequence }, cancellationToken);

                if (sequence - Interlocked.Read(ref _lastPongSequence) <= 1)
                    continue;

                if (Interlocked.Increment(ref _missedPings) >= PackagingProtocol.MissedPingsBeforeUnresponsive)
                {
                    Fault("the worker stopped responding to pings");

                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Fault(ex.Message);
        }
    }

    private void Fault(string reason)
    {
        if (Interlocked.Exchange(ref _faulted, 1) != 0)
            return;

        Faulted?.Invoke(this, reason);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        foreach (var task in new[] { _readTask, _pingTask })
        {
            if (task == null)
                continue;

            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _channel.Dispose();

        try
        {
            if (_pipe.IsConnected)
                _pipe.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disconnecting a packaging worker pipe");
        }

        await _pipe.DisposeAsync();

        await WaitForProcessExitAsync();

        _cts.Dispose();
    }

    /// <summary>
    /// Gives a worker a moment to exit on its own before killing it. Workers exit when their
    /// pipe closes, so this normally returns immediately.
    /// </summary>
    private async Task WaitForProcessExitAsync()
    {
        if (_process == null)
            return;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await _process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                _process.Kill(entireProcessTree: false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not terminate an unresponsive packaging worker");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error waiting for a packaging worker to exit");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
