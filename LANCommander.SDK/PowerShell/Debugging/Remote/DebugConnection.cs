#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell.Debugging.Remote;

/// <summary>
/// One established debug pipe, after the handshake. Owns the only writer (so messages leave in the order
/// they were posted: output always precedes the stop it led up to) and the only reader, and pairs
/// responses with the requests that are waiting for them.
/// </summary>
/// <remarks>
/// <see cref="MessageReceived"/> is raised on the read loop and must not block; a handler that needs to
/// do real work starts a task. <see cref="Post"/> never blocks, so it is safe on the pipeline thread.
/// </remarks>
internal sealed class DebugConnection : IAsyncDisposable
{
    private readonly DebugMessageChannel _channel;
    private readonly ILogger? _logger;
    private readonly Channel<DebugMessage> _outbound = Channel.CreateUnbounded<DebugMessage>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<long, TaskCompletionSource<DebugResponse?>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private long _nextRequestId;
    private Task? _readTask;
    private Task? _writeTask;
    private int _disconnected;

    public DebugConnection(DebugMessageChannel channel, ILogger? logger)
    {
        _channel = channel;
        _logger = logger;
    }

    /// <summary>Raised on the read loop for every message that is not a response.</summary>
    public event Action<DebugMessage>? MessageReceived;

    /// <summary>Raised once, when the pipe closes for any reason.</summary>
    public event Action? Disconnected;

    public bool IsConnected => Volatile.Read(ref _disconnected) == 0;

    /// <summary>Completes when the pipe closes.</summary>
    public Task Closed => _closed.Task;

    public void Start()
    {
        _readTask = Task.Run(ReadLoopAsync);
        _writeTask = Task.Run(WriteLoopAsync);
    }

    /// <summary>Queue a message. Never blocks; dropped silently once disconnected.</summary>
    public void Post(DebugMessage message)
    {
        if (IsConnected)
            _outbound.Writer.TryWrite(message);
    }

    /// <summary>
    /// Send a request and wait for its response. Returns null on timeout, disconnect, or a response of
    /// the wrong type; callers map that to the same fallback a local session would produce.
    /// </summary>
    public async Task<TResponse?> RequestAsync<TResponse>(DebugRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
        where TResponse : DebugResponse
    {
        if (!IsConnected)
            return null;

        request.RequestId = Interlocked.Increment(ref _nextRequestId);

        var completion = new TaskCompletionSource<DebugResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[request.RequestId] = completion;

        try
        {
            Post(request);

            var winner = await Task.WhenAny(completion.Task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);

            if (winner != completion.Task)
                return null;

            return await completion.Task.ConfigureAwait(false) as TResponse;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _pending.TryRemove(request.RequestId, out _);
        }
    }

    /// <summary>Answer a request received from the peer.</summary>
    public void Respond(DebugRequest request, DebugResponse response)
    {
        response.RequestId = request.RequestId;
        Post(response);
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var message = await _channel.ReadAsync(_cts.Token).ConfigureAwait(false);

                if (message is null)
                    break;

                if (message is DebugResponse response)
                {
                    if (_pending.TryGetValue(response.RequestId, out var waiter))
                        waiter.TrySetResult(response);

                    continue;
                }

                try
                {
                    MessageReceived?.Invoke(message);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Script debugger message handler failed for {MessageType}", message.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException or System.Text.Json.JsonException)
        {
            _logger?.LogDebug(ex, "Script debugger pipe closed");
        }
        finally
        {
            OnDisconnected();
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var message in _outbound.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
                await _channel.WriteAsync(message, _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            _logger?.LogDebug(ex, "Script debugger pipe write failed");
        }
        finally
        {
            OnDisconnected();
        }
    }

    private void OnDisconnected()
    {
        if (Interlocked.Exchange(ref _disconnected, 1) != 0)
            return;

        _outbound.Writer.TryComplete();
        _cts.Cancel();

        foreach (var waiter in _pending.Values)
            waiter.TrySetResult(null);

        try { Disconnected?.Invoke(); }
        catch (Exception ex) { _logger?.LogWarning(ex, "Script debugger disconnect handler failed"); }

        _closed.TrySetResult();
    }

    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        OnDisconnected();

        _channel.Dispose();

        try
        {
            if (_readTask is not null)
                await _readTask.ConfigureAwait(false);

            if (_writeTask is not null)
                await _writeTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        _cts.Dispose();
    }
}
