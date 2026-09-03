using Microsoft.Extensions.Logging.Console;

namespace LANCommander.Server.Logging;

/// <summary>
/// Wraps the framework <see cref="ConsoleLoggerProvider"/> so the console honours the per-provider
/// MinimumLevel from Settings.yml, the same way <c>FileLoggerProvider</c> and
/// <see cref="SignalRLoggerProvider"/> do.
/// </summary>
/// <remarks>
/// The framework console provider has no self-gate and relies entirely on the shared
/// Microsoft.Extensions.Logging filter rules. Those rules are winner-take-all: a provider-scoped
/// rule (<c>AddFilter&lt;ConsoleLoggerProvider&gt;</c>) always beats a provider-agnostic category
/// rule, which would resurrect the Hangfire/AntDesign/ASP.NET noise on the console. Gating inside
/// the logger AND-combines with the shared rules instead of replacing them.
/// </remarks>
[ProviderAlias("Console")]
public sealed class MinimumLevelConsoleLoggerProvider(ConsoleLoggerProvider inner, LogLevel minimumLevel)
    : ILoggerProvider, ISupportExternalScope
{
    public ILogger CreateLogger(string categoryName) =>
        new MinimumLevelLogger(inner.CreateLogger(categoryName), minimumLevel);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => inner.SetScopeProvider(scopeProvider);

    public void Dispose() => inner.Dispose();

    private sealed class MinimumLevelLogger(ILogger inner, LogLevel minimumLevel) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel && inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel < minimumLevel)
                return;

            inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
