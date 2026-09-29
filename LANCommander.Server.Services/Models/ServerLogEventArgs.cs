using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Utilities;

namespace LANCommander.Server.Services.Models;

public class ServerLogEventArgs : EventArgs
{
    public Guid ServerId { get; private set; }
    public string Line { get; private set; }

    /// <summary>The log file/RCON console the line came from; null for the process's own output.</summary>
    public ServerConsole? Log { get; private set; }

    /// <summary>When the line was received, in UTC.</summary>
    public DateTime Timestamp { get; private set; }

    /// <summary>Severity inferred from the line's text.</summary>
    public ServerLogLevel Level { get; private set; }

    public ServerLogEventArgs(string line, ServerConsole console)
        : this(console?.ServerId ?? Guid.Empty, line, console)
    {
    }

    public ServerLogEventArgs(Guid serverId, string line, ServerConsole? console = null)
    {
        ServerId = serverId;
        Line = line;
        Log = console;
        Timestamp = DateTime.UtcNow;
        Level = ServerLogClassifier.Classify(line);
    }

    public ServerLogLine ToLogLine() => new(ServerId, Log?.Id, Timestamp, Level, Line);
}

/// <summary>
/// A classified line of server output, as pushed to <c>GameServerHub</c> clients ("LogLine") and
/// kept in <see cref="ServerManager"/>'s recent-output buffer.
/// </summary>
/// <param name="ServerId">The server that produced the line.</param>
/// <param name="ConsoleId">The log file console it was read from; null for process stdout/stderr.</param>
/// <param name="Timestamp">Receipt time in UTC.</param>
/// <param name="Level">Inferred severity.</param>
/// <param name="Message">The line text.</param>
public sealed record ServerLogLine(Guid ServerId, Guid? ConsoleId, DateTime Timestamp, ServerLogLevel Level, string Message);
