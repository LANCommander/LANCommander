using System;
using LANCommander.SDK.Enums;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Helpers
{
    /// <summary>
    /// Translates the legacy <see cref="GameType"/> values (StandaloneExpansion/StandaloneMod)
    /// into the explicit <see cref="GameInstallLocation"/> and ShowInLibrary fields.
    /// The mapping mirrors the AddGameInstallLocation database migrations.
    /// </summary>
    public static class GameTypeHelper
    {
        public readonly struct Resolved
        {
            public Resolved(GameType type, GameInstallLocation installTo, bool showInLibrary)
            {
                Type = type;
                InstallTo = installTo;
                ShowInLibrary = showInLibrary;
            }

            public GameType Type { get; }
            public GameInstallLocation InstallTo { get; }
            public bool ShowInLibrary { get; }
        }

        /// <summary>
        /// Derives the install location and library visibility that a game of the given
        /// (possibly legacy) type had before the fields existed.
        /// </summary>
        public static Resolved FromLegacyType(GameType type, bool hasBaseGame)
        {
#pragma warning disable CS0618 // Legacy values are exactly what this maps from
            switch (type)
            {
                case GameType.StandaloneExpansion:
                    return new Resolved(GameType.Expansion, GameInstallLocation.OwnDirectory, true);

                case GameType.StandaloneMod:
                    return new Resolved(GameType.Mod, hasBaseGame ? GameInstallLocation.BaseGameDirectory : GameInstallLocation.OwnDirectory, true);
#pragma warning restore CS0618

                case GameType.Expansion:
                case GameType.Mod:
                    return new Resolved(type, hasBaseGame ? GameInstallLocation.BaseGameDirectory : GameInstallLocation.OwnDirectory, false);

                default:
                    return new Resolved(GameType.MainGame, GameInstallLocation.OwnDirectory, true);
            }
        }

        /// <summary>
        /// Remaps legacy types and fills in <see cref="ManifestGame.InstallTo"/> /
        /// <see cref="ManifestGame.ShowInLibrary"/> on manifests written before those fields existed.
        /// Addons are normalized recursively.
        /// </summary>
        public static void Normalize(ManifestGame game) => Normalize(game, false);

        private static void Normalize(ManifestGame game, bool nestedAddon)
        {
            if (game == null)
                return;

            // Entries nested under a manifest's Addons always belong to that manifest's game,
            // even when the entry itself doesn't carry the base game reference
            var hasBaseGame = nestedAddon || game.BaseGameId != Guid.Empty || !String.IsNullOrWhiteSpace(game.BaseGame);
            var resolved = FromLegacyType(game.Type, hasBaseGame);

            game.Type = resolved.Type;

            if (game.Type == GameType.MainGame)
            {
                game.InstallTo = GameInstallLocation.OwnDirectory;
                game.ShowInLibrary = true;
            }
            else
            {
                game.InstallTo ??= resolved.InstallTo;
                game.ShowInLibrary ??= resolved.ShowInLibrary;
            }

            if (game.Addons != null)
                foreach (var addon in game.Addons)
                    Normalize(addon, true);
        }
    }
}
