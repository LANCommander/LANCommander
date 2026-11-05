namespace LANCommander.Server.Plugins;

/// <summary>
/// Given to an <see cref="IServerEngineExtension.SettingsComponent"/> on Settings › Servers, so the
/// engine's configuration is saved along with the rest of the page.
/// </summary>
public sealed class ServerEngineSettingsContext
{
    private readonly Func<Func<CancellationToken, Task>, IDisposable> _register;
    private readonly Func<Func<Task>, IDisposable>? _registerAdd;

    /// <param name="register">Adds a handler to run when the page is saved; disposing the result removes it.</param>
    public ServerEngineSettingsContext(Func<Func<CancellationToken, Task>, IDisposable> register)
        : this(register, null)
    {
    }

    /// <param name="register">Adds a handler to run when the page is saved; disposing the result removes it.</param>
    /// <param name="registerAdd">
    /// Sets the handler to run when the engine is chosen from the page's Add menu; disposing the result
    /// removes it. Null when the page has no Add menu.
    /// </param>
    public ServerEngineSettingsContext(Func<Func<CancellationToken, Task>, IDisposable> register, Func<Func<Task>, IDisposable>? registerAdd)
    {
        _register = register;
        _registerAdd = registerAdd;
    }

    /// <summary>
    /// Runs <paramref name="save"/> whenever the administrator saves the page. Register it when the
    /// component is initialized, and dispose the result when it is disposed: the component is only
    /// created while its panel is open. Throw to report that the configuration couldn't be saved;
    /// the page shows the message, and the rest of the page is saved regardless.
    /// </summary>
    public IDisposable OnSave(Func<CancellationToken, Task> save)
    {
        ArgumentNullException.ThrowIfNull(save);

        return _register(save);
    }

    /// <summary>
    /// Runs <paramref name="add"/> when the administrator chooses the engine from the page's Add menu,
    /// listed with the built-in engines, e.g. to start adding a host. The engine's panel is opened
    /// first, so register when the component is initialized; if the engine was chosen before then,
    /// <paramref name="add"/> runs as soon as it is registered. Dispose the result when the component
    /// is disposed. Without it, choosing the engine just opens its panel.
    /// </summary>
    public IDisposable OnAdd(Func<Task> add)
    {
        ArgumentNullException.ThrowIfNull(add);

        return _registerAdd?.Invoke(add) ?? NoRegistration.Instance;
    }

    private sealed class NoRegistration : IDisposable
    {
        public static readonly NoRegistration Instance = new();

        public void Dispose() { }
    }
}
