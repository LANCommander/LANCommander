using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Settings.Enums;
using ServerModel = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.UI.Pages.Servers.Components;

/// <summary>
/// The fields the server's General and Autostart sections edit, captured so the editor can tell
/// whether anything changed since the server was loaded or saved. Compares by value, text ignoring
/// the difference between empty and missing.
/// </summary>
public sealed record ServerEditSnapshot(
    string? Name,
    Guid? GameId,
    ServerEngine Engine,
    Guid? DockerHostId,
    string? ContainerId,
    Guid? RemoteHostId,
    Guid? RemoteServerId,
    string? Path,
    string? Arguments,
    string? WorkingDirectory,
    string? Host,
    int Port,
    bool UseShellExecute,
    ProcessTerminationMethod ProcessTerminationMethod,
    bool Autostart,
    ServerAutostartMethod AutostartMethod,
    int AutostartDelay)
{
    public static ServerEditSnapshot Of(ServerModel server) => new(
        Text(server.Name),
        server.GameId,
        server.Engine,
        server.DockerHostId,
        Text(server.ContainerId),
        server.RemoteHostId,
        server.RemoteServerId,
        Text(server.Path),
        Text(server.Arguments),
        Text(server.WorkingDirectory),
        Text(server.Host),
        server.Port,
        server.UseShellExecute,
        server.ProcessTerminationMethod,
        server.Autostart,
        server.AutostartMethod,
        server.AutostartDelay);

    internal static string? Text(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
