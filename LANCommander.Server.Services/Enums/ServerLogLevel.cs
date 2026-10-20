namespace LANCommander.Server.Services.Enums;

/// <summary>
/// Severity of a line of game server output. Servers print plain text, so this is inferred from
/// the words in the line by <see cref="Utilities.ServerLogClassifier"/>.
/// </summary>
public enum ServerLogLevel
{
    Info,
    Ready,
    Warn,
    Error,
    Trace
}
