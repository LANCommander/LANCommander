#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell.Debugging.Hosting;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell.Debugging.Remote;

/// <summary>
/// Launcher side of cross-process debugging. Listens on a named pipe that elevated child processes
/// connect to, and bridges each of them to an <see cref="IScriptDebugTarget"/> (a debugger window).
/// </summary>
/// <remarks>
/// The launcher is the server and the elevated child the client because a high-integrity process may
/// open a medium-integrity process's pipe, while the reverse is blocked. The same direction and ACL are
/// used by the packaging workers.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class ScriptDebugPipeServer : IAsyncDisposable
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private readonly IScriptDebugTarget _target;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<ParentConnection, byte> _connections = new();

    private Task? _acceptTask;

    public ScriptDebugPipeServer(IScriptDebugTarget target, ILogger? logger = null)
    {
        _target = target;
        _logger = logger;

        Endpoint = new ScriptDebugRemoteEndpoint(
            DebugProtocol.BuildPipeName(Environment.ProcessId, Guid.NewGuid()),
            DebugProtocol.CreateToken());
    }

    public ScriptDebugRemoteEndpoint Endpoint { get; }

    public void Start() => _acceptTask ??= Task.Run(AcceptLoopAsync);

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;

            try
            {
                pipe = CreatePipe(Endpoint.PipeName);

                await pipe.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);

                var accepted = pipe;
                pipe = null;

                _ = Task.Run(() => HandleConnectionAsync(accepted));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Script debugger pipe server failed to accept a connection");

                // Don't spin if the pipe cannot be created at all.
                try { await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                if (pipe is not null)
                    await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe)
    {
        var channel = new DebugMessageChannel(pipe);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            timeout.CancelAfter(HandshakeTimeout);

            var hello = await channel.ReadAsync(timeout.Token).ConfigureAwait(false) as HelloMessage;
            var rejection = Validate(hello);

            if (rejection is not null)
            {
                _logger?.LogWarning("Rejected script debugger connection: {Reason}", rejection);

                await channel.WriteAsync(new HelloAckMessage { Accepted = false, Reason = rejection }, timeout.Token).ConfigureAwait(false);
                channel.Dispose();
                return;
            }

            await channel.WriteAsync(new HelloAckMessage { Accepted = true }, timeout.Token).ConfigureAwait(false);

            var connection = new ParentConnection(new DebugConnection(channel, _logger), _target, hello!.ProcessId, _logger);

            _connections[connection] = 0;

            connection.Start();

            await connection.Closed.ConfigureAwait(false);

            _connections.TryRemove(connection, out _);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Script debugger connection ended during the handshake");
            channel.Dispose();
        }
    }

    private string? Validate(HelloMessage? hello)
    {
        if (hello is null)
            return "No hello message was received.";

        if (hello.ProtocolVersion != DebugProtocol.Version)
            return $"Protocol version {hello.ProtocolVersion} is not supported (expected {DebugProtocol.Version}).";

        if (!DebugProtocol.TokensMatch(hello.Token, Endpoint.Token))
            return "The connection token was not recognised.";

        return null;
    }

    private static NamedPipeServerStream CreatePipe(string name)
    {
        var security = new PipeSecurity();

        var currentUser = WindowsIdentity.GetCurrent().User;

        if (currentUser != null)
            security.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));

        // Load-bearing: under over-the-shoulder UAC the elevated process runs as a different (administrator)
        // account, which could not open a pipe granted only to the launching user.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            name,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            security);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_acceptTask is not null)
        {
            try { await _acceptTask.ConfigureAwait(false); }
            catch (Exception) { }
        }

        foreach (var connection in _connections.Keys)
            await connection.DisposeAsync().ConfigureAwait(false);

        _connections.Clear();
        _cts.Dispose();
    }

    /// <summary>One connected elevated process, bridged to the debugger target.</summary>
    internal sealed class ParentConnection : IAsyncDisposable
    {
        private readonly DebugConnection _connection;
        private readonly IScriptDebugTarget _target;
        private readonly int _processId;
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<long, CancellationTokenSource> _pendingReads = new();
        private readonly CancellationTokenSource _cts = new();

        private RemoteDebugSession? _session;
        private IConsoleSink _sink = NullConsoleSink.Instance;

        public ParentConnection(DebugConnection connection, IScriptDebugTarget target, int processId, ILogger? logger)
        {
            _connection = connection;
            _target = target;
            _processId = processId;
            _logger = logger;

            _connection.MessageReceived += OnMessage;
            _connection.Disconnected += OnDisconnected;
        }

        public Task Closed => _connection.Closed;

        public void Start() => _connection.Start();

        private void OnMessage(DebugMessage message)
        {
            var session = Volatile.Read(ref _session);

            switch (message)
            {
                case AttachRequest attach:
                    HandleAttach(attach);
                    break;

                case PrepareScriptFileRequest prepare:
                    // Writes a file; keep it off the read loop.
                    _ = Task.Run(() =>
                    {
                        try { _target.PrepareScriptFile(prepare.Identity, prepare.Path); }
                        catch (Exception ex) { _logger?.LogWarning(ex, "Could not prepare {Path} for an elevated script", prepare.Path); }

                        _connection.Respond(prepare, new AckResponse());
                    });
                    break;

                case OutputMessage output when output.NewLine:
                    _sink.WriteLine(output.Kind, output.Text, output.Foreground, output.Background);
                    break;

                case OutputMessage output:
                    _sink.Write(output.Kind, output.Text, output.Foreground, output.Background);
                    break;

                case ProgressMessage progress:
                    _sink.ReportProgress(progress.SourceId, progress.Progress);
                    break;

                case ReadLineRequest read:
                    _ = AnswerReadLineAsync(read);
                    break;

                case ReadLineCancelMessage cancel:
                    if (_pendingReads.TryRemove(cancel.RequestId, out var readCancellation))
                        readCancellation.Cancel();
                    break;

                case StateChangedEvent state:
                    session?.OnStateChanged(state.State);
                    break;

                case StoppedEvent stopped:
                    session?.OnStopped(stopped.Info);
                    break;

                case ResumedEvent:
                    session?.OnResumed();
                    break;

                case BreakpointChangedEvent breakpoint:
                    session?.OnBreakpointChanged(breakpoint.Update);
                    break;

                case RunCompletedEvent completed:
                    session?.OnRunCompleted(new RunCompletion(
                        completed.ExitCode,
                        completed.Faulted,
                        completed.ErrorMessage,
                        TimeSpan.FromMilliseconds(completed.DurationMilliseconds)));

                    Interlocked.CompareExchange(ref _session, null, session);
                    _sink = NullConsoleSink.Instance;
                    break;
            }
        }

        private void HandleAttach(AttachRequest request)
        {
            ScriptDebugAttachment? attachment = null;

            try
            {
                attachment = _target.BeginAttach(request.Identity);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "The script debugger could not attach to an elevated script");
            }

            if (attachment is null)
            {
                _connection.Respond(request, new AttachResponse { Accepted = false });
                return;
            }

            var session = new RemoteDebugSession(_connection, request.Identity.ScriptPath ?? string.Empty, _processId);

            _sink = attachment.Sink;
            Volatile.Write(ref _session, session);

            try
            {
                attachment.SessionStarted(session);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "The script debugger failed to observe an elevated script session");
            }

            _connection.Respond(request, new AttachResponse
            {
                Accepted = true,
                Breakpoints = [.. attachment.Breakpoints],
                StepIntoOnStart = attachment.StepIntoOnStart,
            });
        }

        private async Task AnswerReadLineAsync(ReadLineRequest request)
        {
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _pendingReads[request.RequestId] = cancellation;

            try
            {
                var text = await _sink.ReadLineAsync(request.Prompt, request.Secure, cancellation.Token).ConfigureAwait(false);

                _connection.Respond(request, new ReadLineResponse { Text = text });
            }
            catch (OperationCanceledException)
            {
                _connection.Respond(request, new ReadLineResponse { Cancelled = true });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not answer Read-Host for an elevated script");
                _connection.Respond(request, new ReadLineResponse { Text = string.Empty });
            }
            finally
            {
                _pendingReads.TryRemove(request.RequestId, out _);
                cancellation.Dispose();
            }
        }

        private void OnDisconnected()
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                try { _cts.Cancel(); }
                catch (ObjectDisposedException) { }
            }

            Interlocked.Exchange(ref _session, null)?.OnDisconnected();
        }

        private int _disposed;

        /// <summary>Called by both the connection handler and server shutdown; only the first call counts.</summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _connection.MessageReceived -= OnMessage;

            try { _cts.Cancel(); }
            catch (ObjectDisposedException) { }

            await _connection.DisposeAsync().ConfigureAwait(false);

            Interlocked.Exchange(ref _session, null)?.OnDisconnected();

            _cts.Dispose();
        }
    }
}
