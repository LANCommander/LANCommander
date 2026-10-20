using System.IO.Pipes;
using LANCommander.Packaging.IPC;

namespace LANCommander.Packaging.Worker;

/// <summary>
/// The worker's end of the launcher channel.
/// </summary>
/// <remarks>
/// The worker is the pipe <em>client</em>. That direction is deliberate: when the worker has
/// been respawned elevated it has to reach a medium-integrity launcher, and a high-integrity
/// process can open a lower-integrity process's pipe but not the other way around.
/// </remarks>
internal sealed class WorkerConnection : IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly PackagingMessageChannel _channel;

    public WorkerConnection(NamedPipeClientStream pipe)
    {
        _pipe = pipe;
        _channel = new PackagingMessageChannel(pipe, leaveOpen: true);
    }

    /// <summary>
    /// Connects to the launcher's pipe.
    /// </summary>
    public static async Task<WorkerConnection> ConnectAsync(
        string pipeName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(
            ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(timeout);

        await pipe.ConnectAsync(timeoutCts.Token);

        return new WorkerConnection(pipe);
    }

    public Task SendAsync(PackagingMessage message, CancellationToken cancellationToken = default) =>
        _channel.WriteAsync(message, cancellationToken);

    public Task<PackagingMessage?> ReceiveAsync(CancellationToken cancellationToken = default) =>
        _channel.ReadAsync(cancellationToken);

    public Task LogAsync(PackagingLogLevel level, string message)
    {
        // Diagnostics must never take down a capture, and they are frequently emitted from
        // callbacks that cannot await.
        try
        {
            return SendAsync(new WorkerLogMessage { Level = level, Message = message });
        }
        catch
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Performs the version and token handshake.
    /// </summary>
    /// <returns>True when the launcher accepted this worker.</returns>
    public async Task<bool> HandshakeAsync(
        WorkerHelloMessage hello, CancellationToken cancellationToken)
    {
        await SendAsync(hello, cancellationToken);

        var reply = await ReceiveAsync(cancellationToken);

        return reply is HostHelloMessage { Accepted: true };
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Dispose();

        await _pipe.DisposeAsync();
    }
}
