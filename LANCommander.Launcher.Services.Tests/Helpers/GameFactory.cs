using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Enums;

namespace LANCommander.Launcher.Services.Tests.Helpers;

internal static class GameFactory
{
    public static Game Make(
        string title,
        Guid? id = null,
        bool installed = false,
        bool singleplayer = false,
        Engine? engine = null,
        IEnumerable<Genre>? genres = null,
        IEnumerable<Tag>? tags = null,
        IEnumerable<Company>? developers = null,
        IEnumerable<Company>? publishers = null,
        IEnumerable<Platform>? platforms = null,
        IEnumerable<MultiplayerMode>? multiplayerModes = null,
        GameType type = GameType.MainGame)
    {
        return new Game
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            SortTitle = title,
            // Empty (not null) so ListItem(game)'s ManifestHelper.GetPath / Path.Combine never NPEs.
            InstallDirectory = string.Empty,
            Installed = installed,
            Singleplayer = singleplayer,
            Engine = engine,
            Type = type,
            Genres = genres?.ToList() ?? new List<Genre>(),
            Tags = tags?.ToList() ?? new List<Tag>(),
            Developers = developers?.ToList() ?? new List<Company>(),
            Publishers = publishers?.ToList() ?? new List<Company>(),
            Platforms = platforms?.ToList() ?? new List<Platform>(),
            MultiplayerModes = multiplayerModes?.ToList() ?? new List<MultiplayerMode>(),
        };
    }

    public static ListItem AsListItem(this Game game) => new(game);
}
