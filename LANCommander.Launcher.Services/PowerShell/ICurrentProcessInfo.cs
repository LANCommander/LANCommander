namespace LANCommander.Launcher.Services;

/// <summary>
/// Exposes information about the currently running launcher process that the
/// <see cref="ElevatedScriptInterceptor"/> needs in order to decide whether a script must be
/// re-launched with elevated privileges. Abstracted so the elevation decision can be tested without
/// depending on the real process token.
/// </summary>
public interface ICurrentProcessInfo
{
    /// <summary>
    /// The full path to the executable backing the current process. This is the "minimal launcher"
    /// that gets re-invoked (elevated) to actually run the script.
    /// </summary>
    string ExecutablePath { get; }

    /// <summary>
    /// True if the current process is already running with administrator/root privileges.
    /// </summary>
    bool IsElevated { get; }
}
