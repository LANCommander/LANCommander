namespace LANCommander.Server.Services.Models;

/// <summary>Outcome of <see cref="ServerManager.SendCommandAsync"/>.</summary>
/// <param name="Sent">Whether the command was delivered.</param>
/// <param name="Response">The RCON response, if any. Stdin commands have no direct response; their output arrives as log lines.</param>
/// <param name="Error">Why the command could not be sent.</param>
public sealed record ServerCommandResult(bool Sent, string? Response = null, string? Error = null)
{
    public static ServerCommandResult Failed(string error) => new(false, null, error);
}
