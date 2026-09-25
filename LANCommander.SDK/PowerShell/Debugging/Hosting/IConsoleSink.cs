#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Debugging.Hosting;

/// <summary>
/// The engine-to-UI contract for console output and input.
/// <para>
/// Every method here is called on the pipeline thread. Implementations must not block: queue and
/// return. The one exception is <see cref="ReadLineAsync"/>, which is awaited by a pipeline thread that
/// is deliberately blocked while the UI stays live.
/// </para>
/// </summary>
public interface IConsoleSink
{
    /// <summary>Append text without a trailing newline.</summary>
    void Write(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null);

    /// <summary>Append a complete line.</summary>
    void WriteLine(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null);

    /// <summary>Write-Progress. Rendered as a transient status line, not console history.</summary>
    void ReportProgress(long sourceId, ScriptProgress progress);

    /// <summary>
    /// Read a line of user input. Called on the pipeline thread from <c>PSHostUserInterface.ReadLine</c>,
    /// which must block; the returned task is completed by the UI when the user presses Enter.
    /// </summary>
    Task<string?> ReadLineAsync(string? prompt, bool secure, CancellationToken cancellationToken);
}
