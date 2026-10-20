#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Debugging.Hosting;

/// <summary>
/// Discards output and answers every Read-Host with an empty line. Used once a debugger detaches, so a
/// script that was being debugged can run to completion without anyone watching it.
/// </summary>
public sealed class NullConsoleSink : IConsoleSink
{
    public static NullConsoleSink Instance { get; } = new();

    public void Write(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) { }

    public void WriteLine(ConsoleOutputKind kind, string text, ConsoleColor? foreground = null, ConsoleColor? background = null) { }

    public void ReportProgress(long sourceId, ScriptProgress progress) { }

    public Task<string?> ReadLineAsync(string? prompt, bool secure, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(string.Empty);
}
