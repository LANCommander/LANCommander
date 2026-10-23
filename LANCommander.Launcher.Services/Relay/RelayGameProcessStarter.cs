using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using LANCommander.Interposer;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Relay;

/// <summary>
/// Starts games with LAN multiplayer under the Interposer, joined to the server's network relay, so
/// players on other networks see them as if they shared a LAN. Declines, and the game starts as it
/// always has, whenever that cannot be done: the relay is turned off here or not offered by the
/// server, the game has no LAN mode, or it is not an executable the Interposer can start.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RelayGameProcessStarter(
    RelayClient relayClient,
    ISettingsProvider settingsProvider,
    IConnectionClient connectionClient,
    ProfileClient profileClient,
    ILogger<RelayGameProcessStarter> logger) : IGameProcessStarter
{
    public async Task<StartedGameProcess?> StartAsync(GameProcessStartContext context, CancellationToken cancellationToken)
    {
        if (!settingsProvider.CurrentValue.NetworkRelay.Enabled || !RelayClient.SupportsRelay(context.Manifest))
            return null;

        var executable = ResolveExecutable(context.StartInfo);

        if (executable == null)
            return null;

        var session = await relayClient.CreateSessionAsync(context.GameId, context.Manifest?.Title, cancellationToken);

        if (session == null)
            return null;

        var host = string.IsNullOrWhiteSpace(session.Host) ? connectionClient.GetServerAddress()?.Host : session.Host;

        if (string.IsNullOrWhiteSpace(host))
            return null;

        var interposer = new InterposerService
        {
            // What the game is told its user is, and the name other players see in the relay
            Username = await GetPlayerNameAsync() ?? string.Empty,
            Relay = new RelayOptions
            {
                Enabled = true,
                Server = host,
                Port = session.Port,
                Network = session.Network,
                Ticket = session.Ticket,
            },
        };

        interposer.RelayStateChanged += (_, e) =>
            logger.LogInformation("Relay for game {GameId}: {State} {Address}", context.GameId, e.State, e.VirtualAddress);

        try
        {
            context.StartInfo.FileName = executable;

            // A game that loads the Interposer itself, as a proxy DLL, must not have it injected as well
            var proxied = InterposerService.IsDeployedAsProxy(executable);

            var process = proxied
                ? interposer.StartWithProxy(context.StartInfo)
                : interposer.Start(context.StartInfo);

            logger.LogInformation(
                "Started game {GameId} {How}, joining relay network {Network} at {Host}:{Port}",
                context.GameId, proxied ? "with its Interposer proxy" : "with the Interposer injected", session.Network, host, session.Port);

            // The InterposerService holds the overrides and pipes the game reads; it lives as long as the game
            return new StartedGameProcess(process, interposer);
        }
        catch (Exception ex) when (ex is InjectionException or Win32Exception)
        {
            // Most often a game that requires elevation, which CreateProcess will not start
            interposer.Dispose();

            logger.LogWarning(ex, "Could not start game {GameId} under the Interposer; starting it without the relay", context.GameId);

            return null;
        }
    }

    /// <summary>
    /// The full path of the executable the action runs, when the Interposer can start it: a plain
    /// .exe, not elevated, a document or a URL, which only the shell can open.
    /// </summary>
    internal static string? ResolveExecutable(ProcessStartInfo startInfo)
    {
        if (!string.IsNullOrEmpty(startInfo.Verb) || string.IsNullOrWhiteSpace(startInfo.FileName))
            return null;

        string path;

        try
        {
            path = Path.IsPathRooted(startInfo.FileName) || string.IsNullOrWhiteSpace(startInfo.WorkingDirectory)
                ? Path.GetFullPath(startInfo.FileName)
                : Path.GetFullPath(Path.Combine(startInfo.WorkingDirectory, startInfo.FileName));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // steam://rungameid/..., and other things that are not paths
            return null;
        }

        if (!string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return null;

        return path;
    }

    private async Task<string?> GetPlayerNameAsync()
    {
        try
        {
            var name = await profileClient.GetAliasAsync();

            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception ex)
        {
            // The relay then shows the Windows account name
            logger.LogDebug(ex, "Could not get the player's alias for the relay");

            return null;
        }
    }
}
