using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Interposer;

/// <summary>
/// What the Interposer will do for a game in the library, asked before it is launched -- before the
/// launcher has written the player's values into the manifest, so they are read from the library.
/// </summary>
public sealed class InterposerLaunchService(ISettingsProvider settingsProvider, ILogger<InterposerLaunchService> logger)
{
    public InterposerOptions GetOptions(Data.Models.Game game) =>
        InterposerOptions.For(GetManifest(game), settingsProvider.CurrentValue.Interposer);

    /// <summary>
    /// Whether the game joins the server's network relay when the player has made no choice for it:
    /// when it has a LAN mode the relay carries and the relay is turned on in the launcher's settings.
    /// </summary>
    public bool JoinsRelayByDefault(Data.Models.Game game) =>
        settingsProvider.CurrentValue.NetworkRelay.Enabled && SupportsRelay(game);

    /// <summary>Whether the game has a LAN mode the relay carries.</summary>
    public bool SupportsRelay(Data.Models.Game game) => SDK.Services.RelayClient.SupportsRelay(GetManifest(game));

    /// <summary>The settings the launcher keeps for itself, which every game starts from.</summary>
    public SDK.Models.Settings Settings => settingsProvider.CurrentValue;

    private SDK.Models.Manifest.Game GetManifest(Data.Models.Game game)
    {
        SDK.Models.Manifest.Game? manifest = null;

        if (game.Installed && !string.IsNullOrWhiteSpace(game.InstallDirectory))
        {
            try
            {
                manifest = ManifestHelper.Read<SDK.Models.Manifest.Game>(game.InstallDirectory, game.Id);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not read the manifest of {GameTitle} ({GameId})", game.Title, game.Id);
            }
        }

        manifest ??= new SDK.Models.Manifest.Game { Id = game.Id, Title = game.Title };

        // The library is the authority for the player's values; the manifest's copy is from the last launch
        manifest.RedistributableOptions = RedistributableOptionValues.Parse(game.RedistributableOptions);

        return manifest;
    }

    /// <summary>
    /// Whether the Interposer, rather than the launcher, shows the game's rich presence. The launcher
    /// stays out of the way then: two clients would take turns replacing each other's activity.
    /// </summary>
    public bool HandlesRichPresence(Data.Models.Game game)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var options = GetOptions(game);

        return options.IsEnabled("RichPresence.Discord.Enabled")
            && (options.HasRedistributable || settingsProvider.CurrentValue.Interposer.Enabled);
    }
}
