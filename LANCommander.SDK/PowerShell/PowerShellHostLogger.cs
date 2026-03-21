using System;
using System.Management.Automation.Host;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell;

/// <summary>
/// An ILogger&lt;TCategoryName&gt; implementation that writes to the PowerShell host (runspace or cmdlet).
/// </summary>
public sealed class PowerShellHostLogger<T> : ILogger<T>
{
    private readonly PowerShellHostLogger _inner;

    public PowerShellHostLogger(PSHostUserInterface ui)
    {
        _inner = new PowerShellHostLogger(ui, typeof(T).FullName ?? typeof(T).Name);
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _inner.Log(logLevel, eventId, state, exception, formatter);
}

/// <summary>
/// An ILogger implementation that writes to the PowerShell host (runspace or cmdlet).
/// Maps log levels to WriteDebugLine, WriteVerboseLine, WriteWarningLine, WriteErrorLine, and WriteLine.
/// </summary>
public sealed class PowerShellHostLogger : ILogger
{
    private readonly PSHostUserInterface _ui;
    private readonly string _categoryName;

    public PowerShellHostLogger(PSHostUserInterface ui, string categoryName)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _categoryName = categoryName ?? string.Empty;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || formatter == null)
            return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null)
            return;

        var prefix = string.IsNullOrEmpty(_categoryName) ? "" : $"[{_categoryName}] ";
        var text = prefix + message;
        if (exception != null && !message.Contains(exception.Message))
            text += Environment.NewLine + exception.ToString();

        try
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    _ui.WriteDebugLine(text);
                    break;
                case LogLevel.Information:
                    _ui.WriteLine(text);
                    break;
                case LogLevel.Warning:
                    _ui.WriteWarningLine(text);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    _ui.WriteErrorLine(text);
                    break;
                default:
                    _ui.WriteLine(text);
                    break;
            }
        }
        catch
        {
            // Host may not support all methods or may be disposed; ignore
        }
    }
}
