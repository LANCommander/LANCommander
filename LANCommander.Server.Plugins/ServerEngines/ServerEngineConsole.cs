namespace LANCommander.Server.Plugins;

/// <summary>
/// A console an <see cref="IServerEngineExtension"/> offers for one of its servers, listed under the
/// server's Monitor beside the process output and the server's own log file consoles.
/// </summary>
/// <param name="Id">
/// Identifies the console among the server's, e.g. <c>logs</c> or <c>shell</c>. It appears in the
/// console's address, so keep it short; it is passed back to the engine when the console is opened.
/// </param>
/// <param name="Name">The console's name in the menu and the page's header.</param>
/// <param name="Kind">How the console is shown, which decides which of the engine's methods opens it.</param>
public sealed record ServerEngineConsole(string Id, string Name, ServerEngineConsoleKind Kind);

/// <summary>How a <see cref="ServerEngineConsole"/> is shown.</summary>
public enum ServerEngineConsoleKind
{
    /// <summary>
    /// Lines the server has written, read back and then followed, e.g. a container's log, shown like
    /// the process output with the server's command bar under it. Opened with
    /// <see cref="IServerEngineExtension.ReadLogAsync"/>.
    /// </summary>
    Log,

    /// <summary>
    /// An interactive terminal, e.g. the server's own console or a shell beside it. Keystrokes go to
    /// the engine as they are typed and what it writes back is drawn as a terminal draws it, escape
    /// sequences included. Opened with <see cref="IServerEngineExtension.OpenTerminalAsync"/>.
    /// </summary>
    Terminal,
}

/// <summary>What of a <see cref="ServerEngineConsoleKind.Log"/> console to read.</summary>
/// <param name="Tail">How many of the latest lines to read back before following; null for all of them.</param>
/// <param name="Follow">
/// Whether to go on returning lines as they are written, until the reader cancels or the log ends,
/// e.g. because the server stopped. When false, return what is there and finish.
/// </param>
public sealed record ServerEngineLogRequest(int? Tail = 1000, bool Follow = true);

/// <summary>A line of a <see cref="ServerEngineConsoleKind.Log"/> console.</summary>
/// <param name="Text">The line, without its line ending.</param>
/// <param name="Timestamp">When it was written, in UTC, where the engine knows; otherwise when it was read.</param>
/// <param name="IsError">Whether it came from the server's error output, so is shown as an error.</param>
public sealed record ServerEngineLogLine(string Text, DateTime? Timestamp = null, bool IsError = false);

/// <summary>A terminal's size, in characters.</summary>
public readonly record struct ServerEngineTerminalSize(int Columns, int Rows);

/// <summary>
/// An open <see cref="ServerEngineConsoleKind.Terminal"/> console. The server reads it and writes to it
/// at the same time, from different threads; it is disposed when the administrator leaves the page,
/// which should end the session.
/// </summary>
public interface IServerEngineTerminal : IAsyncDisposable
{
    /// <summary>
    /// Reads what the terminal writes, as UTF-8 that may split a character across reads. Returns 0
    /// once the session has ended, e.g. the shell exited.
    /// </summary>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>Writes what the administrator typed or pasted, as UTF-8, escape sequences included.</summary>
    ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken);

    /// <summary>
    /// The administrator's terminal changed size. Engines that can't resize a session after opening it
    /// can ignore this.
    /// </summary>
    Task ResizeAsync(ServerEngineTerminalSize size, CancellationToken cancellationToken) => Task.CompletedTask;
}
