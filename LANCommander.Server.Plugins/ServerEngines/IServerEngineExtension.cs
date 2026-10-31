namespace LANCommander.Server.Plugins;

/// <summary>
/// Runs game servers somewhere other than as a process on the LANCommander server's machine, such as
/// in a container. Each engine is offered on a server's General page alongside the built-in Local and
/// Remote engines. Register an implementation in <c>IPlugin.ConfigureServices</c>.
/// </summary>
/// <remarks>
/// <para>
/// The server keeps everything an engine doesn't: a server's name, game, address, autostart, consoles,
/// HTTP paths, actions and scripts. It runs the server's Before Start and After Stop scripts around
/// <see cref="StartAsync"/> and <see cref="StopAsync"/>, and sends RCON commands itself. What the
/// engine needs to know about each server it keeps in <see cref="ServerEngineServer.Settings"/>,
/// which its <see cref="EditorComponent"/> edits and the server stores with the server.
/// </para>
/// <para>
/// The engine is a singleton and is called concurrently. Every call names the server it is about, so
/// the engine needs no record of its own of which servers it runs.
/// </para>
/// </remarks>
public interface IServerEngineExtension
{
    /// <summary>
    /// Identifies the engine. Stored with every server that uses it, so it must never change, e.g.
    /// <c>com.mycompany.myplugin.engine</c>.
    /// </summary>
    string Id { get; }

    /// <summary>The engine's name, as offered on a server's General page.</summary>
    string Name { get; }

    /// <summary>What the engine can do beyond starting and stopping a server.</summary>
    ServerEngineFeatures Features => ServerEngineFeatures.None;

    /// <summary>
    /// A Razor component shown on a server's General page while the engine is selected, in place of
    /// the Local engine's executable, arguments and process options. It takes a
    /// <see cref="ServerEngineEditorContext"/> as its <c>Context</c> parameter, and is saved with the
    /// rest of the page. Null shows nothing.
    /// </summary>
    Type? EditorComponent => null;

    /// <summary>
    /// Further pages of a server's editor, listed under the engine's name in its menu once the server
    /// has been saved with this engine.
    /// </summary>
    IReadOnlyList<ServerEngineSection> Sections => [];

    /// <summary>
    /// Called once, when the server starts, before any other call. <paramref name="host"/> is how the
    /// engine reports what its servers do; keep it.
    /// </summary>
    Task InitializeAsync(IServerEngineHost host, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called with every server using the engine when the engine is initialized and whenever one is
    /// added, changed or deleted, e.g. to follow the output of those that are running.
    /// </summary>
    Task ServersChangedAsync(IReadOnlyList<ServerEngineServer> servers, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Starts a server, returning once it has started rather than when it stops. Throw to report that
    /// it could not be started; the server shows the message.
    /// </summary>
    Task StartAsync(ServerEngineServer server, CancellationToken cancellationToken);

    /// <summary>Stops a server, returning once it has stopped.</summary>
    Task StopAsync(ServerEngineServer server, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a server is running. Called every couple of seconds while anyone is looking at the
    /// server, so it should be cheap.
    /// </summary>
    Task<ServerEngineStatus> GetStatusAsync(ServerEngineServer server, CancellationToken cancellationToken);

    /// <summary>
    /// Samples a running server's usage, for <see cref="ServerEngineFeatures.ProcessInfo"/>. Null
    /// when it isn't running or can't be sampled.
    /// </summary>
    Task<ServerEngineProcessInfo?> GetProcessInfoAsync(ServerEngineServer server, CancellationToken cancellationToken)
        => Task.FromResult<ServerEngineProcessInfo?>(null);

    /// <summary>
    /// Whether <see cref="SendInputAsync"/> can deliver a line to the server now, for
    /// <see cref="ServerEngineFeatures.Input"/>.
    /// </summary>
    bool CanSendInput(ServerEngineServer server) => false;

    /// <summary>Writes a line to the server's input. Returns false if that isn't possible.</summary>
    Task<bool> SendInputAsync(ServerEngineServer server, string line, CancellationToken cancellationToken)
        => Task.FromResult(false);

    /// <summary>
    /// The consoles the engine offers for a server, for <see cref="ServerEngineFeatures.Consoles"/>,
    /// e.g. its log and a shell. Called whenever the server's pages are drawn, so it should be cheap and
    /// shouldn't reach out to anything; work it out from the server's settings. A console that can't be
    /// opened right now, e.g. because the server isn't running, should still be listed, and fail to open
    /// with a message saying why.
    /// </summary>
    IReadOnlyList<ServerEngineConsole> GetConsoles(ServerEngineServer server) => [];

    /// <summary>
    /// Reads one of the server's <see cref="ServerEngineConsoleKind.Log"/> consoles, oldest line first.
    /// Throw to report that it can't be read; the server shows the message.
    /// </summary>
    IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(ServerEngineServer server, string consoleId, ServerEngineLogRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException($"The {Name} engine has no log console \"{consoleId}\".");

    /// <summary>
    /// Opens one of the server's <see cref="ServerEngineConsoleKind.Terminal"/> consoles at the
    /// administrator's terminal's size. Throw to report that it can't be opened; the server shows the
    /// message.
    /// </summary>
    Task<IServerEngineTerminal> OpenTerminalAsync(ServerEngineServer server, string consoleId, ServerEngineTerminalSize size, CancellationToken cancellationToken)
        => throw new NotSupportedException($"The {Name} engine has no terminal console \"{consoleId}\".");
}

/// <summary>What an <see cref="IServerEngineExtension"/> can do beyond starting and stopping a server.</summary>
[Flags]
public enum ServerEngineFeatures
{
    None = 0,

    /// <summary>
    /// The engine reports its servers' output through <see cref="IServerEngineHost.WriteOutput"/>,
    /// shown on their Monitor page.
    /// </summary>
    Output = 1,

    /// <summary>The engine can write lines to its servers' input, from their Monitor page.</summary>
    Input = 2,

    /// <summary>The engine samples its servers' CPU and memory use and uptime.</summary>
    ProcessInfo = 4,

    /// <summary>
    /// The engine offers consoles of its own for its servers, e.g. a log or a terminal, listed under
    /// their Monitor; see <see cref="IServerEngineExtension.GetConsoles"/>.
    /// </summary>
    Consoles = 8,
}

/// <summary>Whether a game server is running.</summary>
public enum ServerEngineStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,

    /// <summary>The server failed, e.g. it exited on its own with an error.</summary>
    Error,
}

/// <summary>A sample of a running server's usage. Any member may be null when the engine can't tell.</summary>
/// <param name="Pid">The server's process id, where there is one the administrator would recognize.</param>
/// <param name="StartTime">When the server started, in UTC.</param>
/// <param name="CpuPercent">CPU use as a share of the whole machine, from 0 to 100.</param>
/// <param name="MemoryBytes">Memory in use.</param>
public sealed record ServerEngineProcessInfo(int? Pid, DateTime? StartTime, double? CpuPercent, long? MemoryBytes);

/// <summary>A page of a server's editor added by an <see cref="IServerEngineExtension"/>.</summary>
/// <param name="Slug">
/// The page's address under the server, <c>/Servers/{id}/Engine/{Slug}</c>: letters, digits and
/// <c>-</c>, unique within the engine.
/// </param>
/// <param name="Title">The page's name in the menu and its title.</param>
/// <param name="Component">
/// A Razor component rendered as the page, taking a <see cref="ServerEngineServer"/> as its
/// <c>Server</c> parameter.
/// </param>
public sealed record ServerEngineSection(string Slug, string Title, Type Component);

/// <summary>How an <see cref="IServerEngineExtension"/> reports what its servers do. Thread safe.</summary>
public interface IServerEngineHost
{
    /// <summary>
    /// Reports that a server's status changed on its own, e.g. it exited, so those watching it see so
    /// at once rather than at their next look. Changes made through the engine's own methods needn't be
    /// reported.
    /// </summary>
    void ReportStatus(Guid serverId, ServerEngineStatus status, string? error = null);

    /// <summary>Reports a line of a server's output, for <see cref="ServerEngineFeatures.Output"/>.</summary>
    void WriteOutput(Guid serverId, string line);
}
