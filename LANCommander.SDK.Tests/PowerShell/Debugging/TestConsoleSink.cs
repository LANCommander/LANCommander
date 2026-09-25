using System.Collections.Concurrent;
using LANCommander.SDK.PowerShell.Debugging.Hosting;

namespace LANCommander.SDK.Tests.PowerShell.Debugging;

/// <summary>
/// Records everything the engine writes and answers Read-Host from a queue. Stands in for the debugger
/// console so the engine can be driven with no UI at all.
/// </summary>
public sealed class TestConsoleSink : IConsoleSink
{
    private readonly ConcurrentQueue<(ConsoleOutputKind Kind, string Text)> _lines = new();

    /// <summary>Queued answers for Read-Host, consumed in order.</summary>
    public ConcurrentQueue<string> InputAnswers { get; } = new();

    public IReadOnlyList<(ConsoleOutputKind Kind, string Text)> Lines => _lines.ToArray();

    public IEnumerable<string> TextOf(ConsoleOutputKind kind) =>
        _lines.Where(l => l.Kind == kind).Select(l => l.Text);

    public void Clear() => _lines.Clear();

    public void Write(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) =>
        _lines.Enqueue((kind, text));

    public void WriteLine(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) =>
        _lines.Enqueue((kind, text));

    public void ReportProgress(long sourceId, ScriptProgress progress) { }

    public Task<string?> ReadLineAsync(string? prompt, bool secure, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
            return completion.Task;
        }

        cancellationToken.Register(() => completion.TrySetCanceled());

        if (InputAnswers.TryDequeue(out var answer))
            completion.TrySetResult(answer);

        // Otherwise leave it pending: that is the "script is blocked in Read-Host" case.
        return completion.Task;
    }
}
