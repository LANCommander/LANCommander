namespace LANCommander.Server.Plugins.Data;

/// <summary>Where a plugin context is in being made ready for use.</summary>
public enum PluginDataStatus
{
    /// <summary>Not checked yet.</summary>
    Unvalidated,

    /// <summary>Its migrations or model failed a check; the plugin is turned off.</summary>
    Invalid,

    /// <summary>Its migrations passed every check, but haven't been applied, e.g. while the server has no database yet.</summary>
    Validated,

    /// <summary>Its migrations are being applied.</summary>
    Migrating,

    /// <summary>Applying its migrations failed; the plugin is turned off until an administrator retries.</summary>
    MigrationFailed,

    /// <summary>Checked and migrated; the context can be used.</summary>
    Ready,
}

/// <summary>A plugin context's status, with every error that put it there.</summary>
public sealed record PluginDataState(PluginDataStatus Status, IReadOnlyList<string> Errors)
{
    public static PluginDataState Unvalidated { get; } = new(PluginDataStatus.Unvalidated, []);
}

/// <summary>
/// A plugin context was asked for, or tried to connect, before it was ready. Carries the context's
/// state and every error, so the plugin's log shows exactly which check failed.
/// </summary>
public sealed class PluginDataUnavailableException(Type contextType, PluginDataState state)
    : InvalidOperationException(Describe(contextType, state))
{
    public Type ContextType { get; } = contextType;

    public PluginDataState State { get; } = state;

    private static string Describe(Type contextType, PluginDataState state)
        => state.Errors.Count == 0
            ? $"{contextType.Name} isn't available: its data is {state.Status}."
            : $"{contextType.Name} isn't available: its data is {state.Status}. {string.Join(" ", state.Errors)}";
}
