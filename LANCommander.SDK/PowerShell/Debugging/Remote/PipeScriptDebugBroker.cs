#nullable enable
using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell.Debugging.Hosting;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell.Debugging.Remote;

/// <summary>
/// Elevated-child side of cross-process debugging. Registered in place of the in-process broker when the
/// launcher starts an elevated process with a debug pipe: every script the child runs attaches to the
/// launcher's debugger window over the pipe, while the debug engine itself runs here, next to the script.
/// </summary>
/// <remarks>
/// If the pipe cannot be reached, or drops mid-run, scripts run (or carry on running) without the
/// debugger rather than failing.
/// </remarks>
public sealed class PipeScriptDebugBroker : IScriptDebugBroker, IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly string _token;
    private readonly ILogger? _logger;
    private readonly Lazy<Task<DebugConnection?>> _connection;

    private IDebugSessionHandle? _session;

    public PipeScriptDebugBroker(string pipeName, string token, ILogger? logger = null)
    {
        _pipeName = pipeName;
        _token = token;
        _logger = logger;
        _connection = new Lazy<Task<DebugConnection?>>(ConnectAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private DebugConnection? Connection
    {
        get
        {
            // Callers are script-client threads in a headless process; blocking briefly is fine.
            var connection = _connection.Value.GetAwaiter().GetResult();

            return connection is { IsConnected: true } ? connection : null;
        }
    }

    public bool IsAttached(ScriptIdentity identity) => Connection is not null;

    public void PrepareScriptFile(ScriptIdentity identity, string path)
    {
        if (Connection is not { } connection)
            return;

        connection
            .RequestAsync<AckResponse>(new PrepareScriptFileRequest { Identity = identity, Path = path }, DebugProtocol.ConnectTimeout)
            .GetAwaiter()
            .GetResult();
    }

    public async ValueTask<ScriptDebugAttachment?> TryAttachAsync(ScriptIdentity identity, CancellationToken cancellationToken = default)
    {
        var connection = await _connection.Value.ConfigureAwait(false);

        if (connection is not { IsConnected: true })
            return null;

        var response = await connection
            .RequestAsync<AttachResponse>(new AttachRequest { Identity = identity }, DebugProtocol.ConnectTimeout, cancellationToken)
            .ConfigureAwait(false);

        if (response is not { Accepted: true })
            return null;

        return new ScriptDebugAttachment
        {
            Sink = new PipeConsoleSink(connection),
            Breakpoints = response.Breakpoints,
            StepIntoOnStart = response.StepIntoOnStart,
            SessionStarted = session => Bind(connection, session),
        };
    }

    /// <summary>The launcher is the one holding the pipe server; a child never offers its own.</summary>
    public ScriptDebugRemoteEndpoint? GetRemoteEndpoint(ScriptIdentity identity) => null;

    private async Task<DebugConnection?> ConnectAsync()
    {
        var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            using var timeout = new CancellationTokenSource(DebugProtocol.ConnectTimeout);

            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);

            var channel = new DebugMessageChannel(pipe);

            await channel.WriteAsync(new HelloMessage
            {
                ProtocolVersion = DebugProtocol.Version,
                Token = _token,
                ProcessId = Environment.ProcessId,
                IsElevated = Environment.IsPrivilegedProcess,
            }, timeout.Token).ConfigureAwait(false);

            var ack = await channel.ReadAsync(timeout.Token).ConfigureAwait(false) as HelloAckMessage;

            if (ack is not { Accepted: true })
            {
                _logger?.LogWarning("The launcher refused the script debugger connection: {Reason}", ack?.Reason ?? "no reply");
                channel.Dispose();
                return null;
            }

            var connection = new DebugConnection(channel, _logger);

            connection.MessageReceived += message => OnMessage(connection, message);
            connection.Disconnected += OnDisconnected;
            connection.Start();

            return connection;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not connect to the launcher's script debugger; scripts will run without it");
            await pipe.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    // ---- outbound: engine events → launcher ------------------------------------------------

    private void Bind(DebugConnection connection, IDebugSessionHandle session)
    {
        Volatile.Write(ref _session, session);

        session.StateChanged += state => connection.Post(new StateChangedEvent { State = state });
        session.Stopped += info => connection.Post(new StoppedEvent { Info = info });
        session.Resumed += () => connection.Post(new ResumedEvent());
        session.BreakpointChanged += update => connection.Post(new BreakpointChangedEvent { Update = update });
        session.RunCompleted += completion =>
        {
            connection.Post(new RunCompletedEvent
            {
                ExitCode = completion.ExitCode,
                Faulted = completion.Faulted,
                ErrorMessage = completion.ErrorMessage,
                DurationMilliseconds = (long)completion.Duration.TotalMilliseconds,
            });

            Interlocked.CompareExchange(ref _session, null, session);
        };
    }

    // ---- inbound: launcher → engine --------------------------------------------------------

    private void OnMessage(DebugConnection connection, DebugMessage message)
    {
        var session = Volatile.Read(ref _session);

        if (session is null)
            return;

        switch (message)
        {
            case ResumeCommand resume:
                session.Resume(resume.Kind);
                break;

            case StopCommand:
                session.RequestStop();
                break;

            case DetachCommand:
                session.Detach();
                break;

            case ChangeBreakpointCommand change:
                session.ChangeBreakpoint(change.Request, change.Add);
                break;

            case DebugRequest request:
                // Never await on the read loop: the engine answers through its pump, and the pump may be
                // waiting for a message this loop has yet to read.
                _ = AnswerAsync(connection, session, request);
                break;
        }
    }

    private static async Task AnswerAsync(DebugConnection connection, IDebugSessionHandle session, DebugRequest request)
    {
        DebugResponse response = request switch
        {
            EvaluateRequest evaluate when evaluate.IsConsoleCommand => new EvaluateResponse
            {
                Result = await session.ExecuteConsoleCommandAsync(evaluate.Expression, Timeout(evaluate.TimeoutMilliseconds)).ConfigureAwait(false),
            },
            EvaluateRequest evaluate => new EvaluateResponse
            {
                Result = await session.EvaluateAsync(evaluate.Expression, Timeout(evaluate.TimeoutMilliseconds)).ConfigureAwait(false),
            },
            FrameVariablesRequest frame => new VariablesResponse
            {
                Variables = [.. await session.GetFrameVariablesAsync(frame.Scope, Timeout(frame.TimeoutMilliseconds)).ConfigureAwait(false)],
            },
            ExpandVariableRequest expand => new VariablesResponse
            {
                Variables = [.. await session.ExpandVariableAsync(expand.Handle, Timeout(expand.TimeoutMilliseconds)).ConfigureAwait(false)],
            },
            CommandsRequest commands => new CommandsResponse
            {
                Commands = [.. await session.GetCommandsAsync(Timeout(commands.TimeoutMilliseconds)).ConfigureAwait(false)],
            },
            CommandDetailRequest detail => new CommandDetailResponse
            {
                Detail = await session.GetCommandDetailAsync(detail.Name, Timeout(detail.TimeoutMilliseconds)).ConfigureAwait(false),
            },
            _ => new AckResponse(),
        };

        connection.Respond(request, response);
    }

    private static TimeSpan Timeout(int milliseconds) => TimeSpan.FromMilliseconds(Math.Max(1, milliseconds));

    /// <summary>The launcher went away. Nobody is watching any more, so let the script finish on its own.</summary>
    private void OnDisconnected()
    {
        _logger?.LogWarning("Lost the connection to the launcher's script debugger; the script continues without it");

        Volatile.Read(ref _session)?.Detach();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_connection.IsValueCreated)
            return;

        var connection = await _connection.Value.ConfigureAwait(false);

        if (connection is not null)
            await connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Sends the script's console output to the launcher and asks it to answer Read-Host.</summary>
    private sealed class PipeConsoleSink(DebugConnection connection) : IConsoleSink
    {
        public void Write(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) =>
            connection.Post(new OutputMessage { Kind = kind, Text = text, NewLine = false, Foreground = foreground, Background = background });

        public void WriteLine(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) =>
            connection.Post(new OutputMessage { Kind = kind, Text = text, NewLine = true, Foreground = foreground, Background = background });

        public void ReportProgress(long sourceId, ScriptProgress progress) =>
            connection.Post(new ProgressMessage { SourceId = sourceId, Progress = progress });

        public async Task<string?> ReadLineAsync(string? prompt, bool secure, CancellationToken cancellationToken)
        {
            var request = new ReadLineRequest { Prompt = prompt, Secure = secure };

            // RequestAsync assigns the id before its first await, so it is known by the time this registers.
            var pending = connection.RequestAsync<ReadLineResponse>(request, System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);

            using var registration = cancellationToken.Register(() =>
                connection.Post(new ReadLineCancelMessage { RequestId = request.RequestId }));

            var response = await pending.ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (response is { Cancelled: true })
                throw new OperationCanceledException(cancellationToken);

            // Null means the pipe dropped; the session detaches, and an empty line lets the script continue.
            return response?.Text ?? string.Empty;
        }
    }
}
