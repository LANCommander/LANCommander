using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using LANCommander.Interposer;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Clients;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Interposer;

/// <summary>How the Interposer reaches a game.</summary>
public enum InterposerDeployment
{
    /// <summary>The game does not have it; the launcher injects its own copy.</summary>
    Bundled,

    /// <summary>The redistributable installed it beside the game as a DLL the game loads itself.</summary>
    Proxy,

    /// <summary>The redistributable installed it with its injector, which the game's action runs.</summary>
    Injector,
}

/// <summary>
/// Starts games under the LANCommander Interposer with the player's settings for it: the launcher's
/// own copy when the game does not have the Interposer, or the game's when the redistributable
/// installed it. Joins LAN games to the server's network relay. Declines, and the game starts as it
/// always has, when there is nothing to apply, or when it cannot start the game this way: it is not
/// a plain executable, or it needs elevation.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InterposerGameProcessStarter(
    RelayClient relayClient,
    ISettingsProvider settingsProvider,
    IConnectionClient connectionClient,
    ProfileClient profileClient,
    InterposerSessionService sessions,
    ILogger<InterposerGameProcessStarter> logger) : IGameProcessStarter
{
    public async Task<StartedGameProcess?> StartAsync(GameProcessStartContext context, CancellationToken cancellationToken)
    {
        var executable = ResolveExecutable(context.StartInfo);

        if (executable == null)
            return null;

        var settings = settingsProvider.CurrentValue;
        var options = InterposerOptions.For(context.Manifest, settings.Interposer);

        var injector = InjectorCommandLine.TryParse(executable, context.StartInfo.Arguments, context.StartInfo.WorkingDirectory);

        var deployment = injector != null ? InterposerDeployment.Injector
            : InterposerService.IsDeployedAsProxy(executable) ? InterposerDeployment.Proxy
            : InterposerDeployment.Bundled;

        var bundled = deployment == InterposerDeployment.Bundled;

        if (bundled && !settings.Interposer.Enabled)
            return null;

        var session = await GetRelaySessionAsync(context, options, settings, cancellationToken);
        var overlay = options.GetOverlayValues(bundled);

        // A relay turned on with no session and no server of its own to join would only make the game
        // wait, on every address lookup, for an address that never comes
        if (session == null && options.IsEnabled("NetworkRelay.Enabled") && string.IsNullOrWhiteSpace(options.Get("NetworkRelay.Server")))
            overlay["NetworkRelay.Enabled"] = "false";

        // Nothing to apply: the game starts exactly as it would without the launcher's help
        if (session == null && !options.ChangesConfig(overlay, bundled))
            return null;

        AddRichPresenceDefaults(overlay, options, context.Manifest);

        var interposer = new InterposerService
        {
            // What the game is told its user is, and the name other players see in the relay
            Username = await GetPlayerNameAsync() ?? injector?.Username ?? string.Empty,
            ComputerName = injector?.ComputerName ?? string.Empty,
            FastDlUrl = injector?.FastDlUrl ?? string.Empty,
            ConfigOverlay = InterposerConfigOverlay.Build(overlay),

            // The launcher's one copy serves every game, so each keeps its config, registry and logs
            // with its own install. A redistributable's copy keeps them beside itself, where its
            // Install script put Config.yml.
            BaseDirectory = bundled ? GetBaseDirectory(context) : null,
            Relay = session,
        };

        interposer.RelayStateChanged += (_, e) =>
            logger.LogInformation("Relay for game {GameId}: {State} {Address}", context.GameId, e.State, e.VirtualAddress);

        try
        {
            Process process;

            switch (deployment)
            {
                case InterposerDeployment.Injector:
                    process = interposer.Start(new ProcessStartInfo(injector!.Executable, injector.Arguments)
                    {
                        // Where the injector would have started it
                        WorkingDirectory = Path.GetDirectoryName(injector.Executable),
                    }, injector.DllPath);
                    break;

                case InterposerDeployment.Proxy:
                    context.StartInfo.FileName = executable;
                    process = interposer.StartWithProxy(context.StartInfo);
                    break;

                default:
                    context.StartInfo.FileName = executable;
                    process = interposer.Start(context.StartInfo);
                    break;
            }

            logger.LogInformation(
                "Started game {GameId} under the Interposer ({Deployment}){Relay}",
                context.GameId,
                deployment,
                session != null ? $", joining relay network {session.Network} at {session.Server}:{session.Port}" : string.Empty);

            // The rest of the launcher follows the game's relay through this until the game exits
            var registration = sessions.Register(context.GameId, interposer, joinsRelay: session != null,
                renewRelay: session == null ? null : token => GetRelaySessionAsync(context, options, settingsProvider.CurrentValue, token));

            // The InterposerService holds the overrides and pipes the game reads; it lives as long as the game
            return new StartedGameProcess(process, new GameLifetime(registration, interposer));
        }
        catch (Exception ex) when (ex is InjectionException or Win32Exception)
        {
            // Most often a game that requires elevation, which CreateProcess will not start
            interposer.Dispose();

            logger.LogWarning(ex, "Could not start game {GameId} under the Interposer ({Deployment}); starting it without", context.GameId, deployment);

            return null;
        }
    }

    /// <summary>
    /// Where the launcher's copy of the Interposer keeps this game's config, virtual registry, logs and
    /// downloads: beside the game's manifest, so they go when the game is uninstalled.
    /// </summary>
    public static string GetBaseDirectory(GameProcessStartContext context) =>
        Path.Combine(GameClient.GetMetadataDirectoryPath(context.InstallDirectory, context.GameId), "Interposer");

    /// <summary>
    /// A relay session for the game, when it should join one: the player's choice for the game wins;
    /// otherwise games with a LAN mode the relay can carry join it, unless the relay is turned off.
    /// </summary>
    private async Task<RelayOptions?> GetRelaySessionAsync(
        GameProcessStartContext context, InterposerOptions options, SDK.Models.Settings settings, CancellationToken cancellationToken)
    {
        bool? chosen = options.Player.TryGetValue("NetworkRelay.Enabled", out var value) && bool.TryParse(value, out var enabled)
            ? enabled
            : null;

        if (!(chosen ?? (settings.NetworkRelay.Enabled && RelayClient.SupportsRelay(context.Manifest))))
            return null;

        var session = await relayClient.CreateSessionAsync(context.GameId, context.Manifest?.Title, cancellationToken);

        if (session == null)
            return null;

        var host = string.IsNullOrWhiteSpace(session.Host) ? connectionClient.GetServerAddress()?.Host : session.Host;

        if (string.IsNullOrWhiteSpace(host))
            return null;

        return new RelayOptions
        {
            Enabled = true,
            Server = host,
            Port = session.Port,
            Network = session.Network,
            Ticket = session.Ticket,
        };
    }

    /// <summary>
    /// What the launcher's own presence shows -- the game's title, under the game's Discord application or
    /// LANCommander's -- for whatever the player and the server left blank, once the Interposer's rich
    /// presence is on.
    /// </summary>
    internal static void AddRichPresenceDefaults(Dictionary<string, string?> overlay, InterposerOptions options, SDK.Models.Manifest.Game? manifest)
    {
        if (!options.IsEnabled("RichPresence.Discord.Enabled"))
            return;

        void Default(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(options.Get(key)) && !string.IsNullOrWhiteSpace(value))
                overlay[key] = value;
        }

        var gameApplicationId = manifest?.ExternalIds?
            .FirstOrDefault(e => string.Equals(e.Provider, "Discord", StringComparison.OrdinalIgnoreCase))
            ?.ExternalId;

        Default("RichPresence.Discord.ApplicationId", gameApplicationId ?? DiscordClient.DefaultApplicationId);
        Default("RichPresence.Details", manifest?.Title);

        // A game's own application has its art uploaded under "logo"; LANCommander's has none to show
        if (gameApplicationId != null)
        {
            Default("RichPresence.LargeImage", "logo");
            Default("RichPresence.LargeImageText", manifest?.Title);
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

    /// <summary>What has to last as long as the game: its session's registration first, then the service it reads from.</summary>
    private sealed class GameLifetime(IDisposable registration, InterposerService interposer) : IDisposable
    {
        public void Dispose()
        {
            registration.Dispose();
            interposer.Dispose();
        }
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
            // The game then sees the Windows account name
            logger.LogDebug(ex, "Could not get the player's alias for the Interposer");

            return null;
        }
    }
}
