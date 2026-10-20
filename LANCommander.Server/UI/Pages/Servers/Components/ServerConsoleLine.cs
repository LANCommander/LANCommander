using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.Utilities;

namespace LANCommander.Server.UI.Pages.Servers.Components;

/// <summary>Where a line in a server console came from.</summary>
public enum ServerConsoleLineKind
{
    /// <summary>The server's output, or a line of its log file.</summary>
    Output,

    /// <summary>A command the admin sent from this page, echoed in the accent colour.</summary>
    Command,
}

/// <summary>A line as a server console shows it.</summary>
/// <param name="Timestamp">When it was received, in UTC; null for lines read back from a log file.</param>
/// <param name="Level">Its severity, which picks the label and colours.</param>
/// <param name="Message">The text.</param>
/// <param name="Kind">Output, or an echoed command.</param>
public sealed record ServerConsoleLine(DateTime? Timestamp, ServerLogLevel Level, string Message, ServerConsoleLineKind Kind = ServerConsoleLineKind.Output)
{
    public static ServerConsoleLine From(ServerLogLine line) => new(line.Timestamp, line.Level, line.Message);

    /// <summary>A line with no receipt time, e.g. read back from a log file; its level is inferred from the text.</summary>
    public static ServerConsoleLine Classified(string message, DateTime? timestamp = null) =>
        new(timestamp, ServerLogClassifier.Classify(message), message);

    public static ServerConsoleLine Echo(string command) =>
        new(DateTime.UtcNow, ServerLogLevel.Info, command, ServerConsoleLineKind.Command);
}
