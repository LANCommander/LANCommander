namespace LANCommander.Server.UI.Fixtures.Data;

/// <summary>
/// Names and Ids of the entities <see cref="FixtureDatabaseSeeder"/> creates, for tests to navigate
/// to and assert against. Titles are fictional so fixtures never depend on real products.
/// </summary>
public static class FixtureData
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Password1234!";
    public const string PlayerPassword = "Player1234!";

    public static readonly string[] PlayerUserNames = ["alex", "jordan", "sam"];

    public const string PlayersRoleName = "Players";

    public static class Games
    {
        public const string ArenaBlitz = "Arena Blitz";
        public const string ArenaBlitzMapPack = "Arena Blitz: Map Pack";
        public const string StarfallTactics = "Starfall Tactics";
        public const string StarfallTacticsFrozenFront = "Starfall Tactics: Frozen Front";
        public const string NeonDrift = "Neon Drift";
        public const string DungeonDelvers = "Dungeon Delvers";
        public const string FrontierFortress = "Frontier Fortress";
        public const string RetroRumble = "Retro Rumble";

        public static Guid Id(string title) => FixtureIds.For("game:" + title);

        /// <summary>
        /// Generated titles added beside the named games, so list pages are judged against a
        /// library of realistic size rather than a handful of rows.
        /// </summary>
        public const int CatalogueSize = 2409;

        /// <summary>Every game the seeder creates: the named ones and the catalogue.</summary>
        public const int Total = 8 + CatalogueSize;
    }

    public static class Redistributables
    {
        public const string DirectX = "DirectX End-User Runtime";
        public const string VisualCpp = "Visual C++ Redistributable";

        public static Guid Id(string name) => FixtureIds.For("redistributable:" + name);
    }

    public static class Tools
    {
        public const string MapEditor = "Arena Blitz Map Editor";
        public const string ServerBrowser = "LAN Server Browser";

        public static Guid Id(string name) => FixtureIds.For("tool:" + name);
    }

    public static class Servers
    {
        public const string ArenaBlitzDedicated = "Arena Blitz Dedicated";
        public const string StarfallHost = "Starfall Tactics Host";

        public static Guid Id(string name) => FixtureIds.For("server:" + name);
    }
}
