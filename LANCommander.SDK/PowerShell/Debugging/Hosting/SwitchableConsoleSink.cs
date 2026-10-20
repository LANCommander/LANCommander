#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Debugging.Hosting;

/// <summary>
/// Forwards to an inner sink until <see cref="Detach"/> is called, after which output is discarded and
/// input is answered with an empty line. A Read-Host that is already waiting when the detach happens is
/// released with an empty line too, rather than cancelled, so detaching never aborts the script.
/// </summary>
public sealed class SwitchableConsoleSink : IConsoleSink
{
    private readonly TaskCompletionSource<string?> _detached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile IConsoleSink _inner;

    public SwitchableConsoleSink(IConsoleSink inner) => _inner = inner;

    public bool IsDetached => _detached.Task.IsCompleted;

    public void Detach()
    {
        _inner = NullConsoleSink.Instance;
        _detached.TrySetResult(string.Empty);
    }

    public void Write(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) =>
        _inner.Write(kind, text, foreground, background);

    public void WriteLine(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) =>
        _inner.WriteLine(kind, text, foreground, background);

    public void ReportProgress(long sourceId, ScriptProgress progress) =>
        _inner.ReportProgress(sourceId, progress);

    public async Task<string?> ReadLineAsync(string? prompt, bool secure, CancellationToken cancellationToken)
    {
        if (IsDetached)
            return string.Empty;

        var read = _inner.ReadLineAsync(prompt, secure, cancellationToken);
        var winner = await Task.WhenAny(read, _detached.Task).ConfigureAwait(false);

        return await winner.ConfigureAwait(false);
    }
}
