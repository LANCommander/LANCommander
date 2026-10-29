namespace LANCommander.Server.Plugins;

/// <summary>
/// The server an <see cref="IServerEngineExtension.EditorComponent"/> edits, on its General page.
/// Changes are kept with the page's other unsaved changes and saved with them.
/// </summary>
public sealed class ServerEngineEditorContext
{
    private readonly Func<ServerEngineServer> _read;
    private readonly Action<Func<ServerEngineServer, ServerEngineServer>> _write;

    /// <param name="read">The server as it stands on the page, unsaved changes included.</param>
    /// <param name="write">Applies a change to the server on the page.</param>
    public ServerEngineEditorContext(Func<ServerEngineServer> read, Action<Func<ServerEngineServer, ServerEngineServer>> write)
    {
        _read = read;
        _write = write;
    }

    /// <summary>The server, with the page's unsaved changes. Its id is empty while it is being added.</summary>
    public ServerEngineServer Server => _read();

    /// <summary>Whether the server is being added, so has no id yet.</summary>
    public bool IsNew => Server.Id == Guid.Empty;

    /// <summary>The engine's settings for the server; see <see cref="ServerEngineServer.GetSettings{T}"/>.</summary>
    public T GetSettings<T>() where T : class, new() => Server.GetSettings<T>();

    /// <summary>Replaces the engine's settings for the server.</summary>
    public void SetSettings<T>(T settings) => _write(server => server with { Settings = ServerEngineSettings.Write(settings) });

    /// <summary>
    /// Sets the address players connect to, e.g. from a port the engine publishes. The administrator
    /// can still change it.
    /// </summary>
    public void SetAddress(string? host, int? port) => _write(server => server with
    {
        Host = host ?? server.Host,
        Port = port ?? server.Port,
    });
}
