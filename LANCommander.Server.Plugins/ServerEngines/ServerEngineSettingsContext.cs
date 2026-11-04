namespace LANCommander.Server.Plugins;

/// <summary>
/// Given to an <see cref="IServerEngineExtension.SettingsComponent"/> on Settings › Servers, so the
/// engine's configuration is saved along with the rest of the page.
/// </summary>
public sealed class ServerEngineSettingsContext
{
    private readonly Func<Func<CancellationToken, Task>, IDisposable> _register;

    /// <param name="register">Adds a handler to run when the page is saved; disposing the result removes it.</param>
    public ServerEngineSettingsContext(Func<Func<CancellationToken, Task>, IDisposable> register)
    {
        _register = register;
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
}
