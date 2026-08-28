using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Migrations;
using Semver;

namespace LANCommander.Server.Migrations;

/// <summary>
/// Moves the settings-based storage directories (Update, Launcher, Backups, Snippets, Modules) from
/// their previously-written raw locations (resolved verbatim relative to the working directory) to the
/// unified location produced by <see cref="AppPaths.ResolveStorageLocationPath(string, string[])"/>.
/// This aligns runtime writes with the same resolution rule used everywhere else so relative paths land
/// under the config directory instead of next to the binary.
/// </summary>
public class AlignSettingsStoragePathsMigration(
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<AlignSettingsStoragePathsMigration> logger) : FileSystemMigration(logger)
{
    public override SemVersion Version => new(2, 1, 0);

    private IEnumerable<string> GetConfiguredPaths()
    {
        var settings = settingsProvider.CurrentValue;

        yield return settings.Server.Update.StoragePath;
        yield return settings.Server.Launcher.StoragePath;
        yield return settings.Server.Backups.StoragePath;
        yield return settings.Server.Scripts.Snippets.StoragePath;
        yield return settings.Server.Scripts.Modules.StoragePath;
    }

    private static (string Source, string Destination)? GetMove(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        // Rooted paths already resolve verbatim, so there is nothing to move.
        if (Path.IsPathRooted(configuredPath))
            return null;

        var source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
        var destination = AppPaths.ResolveStorageLocationPath(configuredPath);

        if (string.Equals(source, destination, StringComparison.Ordinal))
            return null;

        return (source, destination);
    }

    public override async Task ExecuteAsync()
    {
        foreach (var configuredPath in GetConfiguredPaths())
        {
            var move = GetMove(configuredPath);

            if (move == null)
                continue;

            var (source, destination) = move.Value;

            try
            {
                if (!Directory.Exists(source))
                    continue;

                Logger.LogInformation("Moving storage directory from \"{Source}\" to \"{Destination}\"", source, destination);

                DirectoryHelper.MoveContents(source, destination);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while moving storage directory from \"{Source}\" to \"{Destination}\"", source, destination);
            }
        }
    }

    public override Task<bool> ShouldExecuteAsync()
    {
        foreach (var configuredPath in GetConfiguredPaths())
        {
            var move = GetMove(configuredPath);

            if (move != null && Directory.Exists(move.Value.Source))
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
