using LANCommander.SDK.Enums;
using LANCommander.Server.Settings.Enums;
using Entities = global::LANCommander.Server.Data.Models;

namespace LANCommander.Server.Tests.Mapping;

/// <summary>
/// Fully-populated Entities.Models graphs used to exercise every mapping. Values are distinctive so
/// differences are easy to spot. Back-references that would trigger AutoMapper's MaxDepth deep
/// self-nesting (e.g. Server.Game) are intentionally left null — the Mapperly design breaks those
/// cycles, and both mappers agree when the back-reference is absent.
/// </summary>
public static class SampleEntities
{
    private static Guid G(int seed) => new Guid(seed, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static DateTime D(int day) => new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day);

    public static Entities.User User(string name = "sampleuser", int seed = 900)
        => new()
        {
            Id = G(seed),
            UserName = name,
            Alias = name + "_alias",
            Email = name + "@example.com",
            CreatedOn = D(1),
            UpdatedOn = D(2),
        };

    public static Entities.Archive Archive(int seed = 1, string version = "1.0.0", int createdDay = 1)
        => new()
        {
            Id = G(seed),
            ObjectKey = "object-key-" + seed,
            Version = version,
            CompressedSize = 1000 + seed,
            UncompressedSize = 5000 + seed,
            StorageLocationId = G(seed + 50),
            CreatedOn = D(createdDay),
            UpdatedOn = D(createdDay + 1),
        };

    public static Entities.Company Company(int seed = 2)
        => new() { Id = G(seed), Name = "Company " + seed, CreatedOn = D(3), UpdatedOn = D(4) };

    public static Entities.Collection Collection(int seed = 3)
        => new() { Id = G(seed), Name = "Collection " + seed, CreatedOn = D(5), UpdatedOn = D(6) };

    public static Entities.Engine Engine(int seed = 4)
        => new() { Id = G(seed), Name = "Engine " + seed, CreatedOn = D(7), UpdatedOn = D(8) };

    public static Entities.Genre Genre(int seed = 5)
        => new() { Id = G(seed), Name = "Genre " + seed, CreatedOn = D(9), UpdatedOn = D(10) };

    public static Entities.Tag Tag(int seed = 6)
        => new() { Id = G(seed), Name = "Tag " + seed, CreatedOn = D(11), UpdatedOn = D(12) };

    public static Entities.Platform Platform(int seed = 7)
        => new() { Id = G(seed), Name = "Platform " + seed, CreatedOn = D(13), UpdatedOn = D(14) };

    public static Entities.Key Key(int seed = 8)
        => new()
        {
            Id = G(seed),
            Value = "KEY-" + seed,
            GameId = G(seed + 100),
            ClaimedByMacAddress = "AA:BB:CC",
            ClaimedByIpv4Address = "10.0.0." + seed,
            ClaimedByComputerName = "PC-" + seed,
            ClaimedOn = D(15),
            CreatedOn = D(16),
            UpdatedOn = D(17),
        };

    public static Entities.Media Media(int seed = 9, MediaType type = MediaType.Background)
        => new()
        {
            Id = G(seed),
            FileId = G(seed + 200),
            Name = "Media " + seed,
            Type = type,
            SourceUrl = "https://example.com/" + seed,
            MimeType = "image/png",
            Crc32 = "crc" + seed,
            SortOrder = seed,
            StorageLocationId = G(seed + 60),
            CreatedOn = D(18),
            UpdatedOn = D(19),
        };

    public static Entities.MultiplayerMode MultiplayerMode(int seed = 10)
        => new()
        {
            Id = G(seed),
            Description = "MP " + seed,
            MinPlayers = 1,
            MaxPlayers = 16,
            Spectators = 4,
            CreatedOn = D(20),
            UpdatedOn = D(21),
        };

    public static Entities.PlaySession PlaySession(int seed = 11)
        => new()
        {
            Id = G(seed),
            UserId = G(seed + 300),
            Start = D(22),
            End = D(23),
            CreatedOn = D(24),
            UpdatedOn = D(25),
        };

    public static Entities.SavePath SavePath(int seed = 12)
        => new()
        {
            Id = G(seed),
            Path = "/saves/" + seed,
            WorkingDirectory = "/wd/" + seed,
            IsRegex = true,
            CreatedOn = D(26),
            UpdatedOn = D(27),
        };

    public static Entities.Script Script(int seed = 13, ScriptType type = ScriptType.Install)
        => new()
        {
            Id = G(seed),
            Name = "Script " + seed,
            Description = "desc " + seed,
            Type = type,
            Contents = "echo " + seed,
            RequiresAdmin = true,
            CreatedOn = D(28),
            UpdatedOn = D(29),
        };

    public static Entities.ServerConsole ServerConsole(int seed = 14)
        => new()
        {
            Id = G(seed),
            Name = "Console " + seed,
            Path = "/console/" + seed,
            Host = "host" + seed,
            Port = 2000 + seed,
            Password = "pw" + seed,
            ServerId = G(seed + 400),
            CreatedOn = D(30),
            UpdatedOn = D(31),
        };

    public static Entities.ServerHttpPath ServerHttpPath(int seed = 15)
        => new()
        {
            Id = G(seed),
            LocalPath = "/local/" + seed,
            Path = "/http/" + seed,
            ServerId = G(seed + 400),
            CreatedOn = D(32),
            UpdatedOn = D(33),
        };

    public static Entities.GameCustomField GameCustomField(int seed = 16)
        => new() { Id = G(seed), Name = "Field " + seed, Value = "Value " + seed, CreatedOn = D(34), UpdatedOn = D(35) };

    public static Entities.GameExternalId GameExternalId(int seed = 17)
        => new() { Id = G(seed), Provider = "provider" + seed, ExternalId = "ext" + seed, CreatedOn = D(36), UpdatedOn = D(37) };

    public static Entities.Action Action(int seed = 18, bool primary = true)
        => new()
        {
            Id = G(seed),
            Name = "Action " + seed,
            Arguments = "--arg" + seed,
            Path = "/path/" + seed,
            WorkingDirectory = "/awd/" + seed,
            PrimaryAction = primary,
            SortOrder = seed,
            OptionOverrides = "opt" + seed,
            GameId = G(seed + 500),
            CreatedOn = D(38),
            UpdatedOn = D(39),
        };

    public static Entities.GameSave GameSave(int seed = 19)
        => new()
        {
            Id = G(seed),
            StorageLocationId = G(seed + 70),
            Size = 12345 + seed,
            UserId = G(seed + 600),
            User = User("saveowner", seed + 600),
            CreatedOn = D(40),
            UpdatedOn = D(41),
        };

    public static Entities.Server Server(int seed = 20)
        => new()
        {
            Id = G(seed),
            Name = "Server " + seed,
            Engine = ServerEngine.Local,
            Path = "/server/" + seed,
            Arguments = "-srv" + seed,
            WorkingDirectory = "/swd/" + seed,
            OnStartScriptPath = "/start",
            OnStopScriptPath = "/stop",
            ContainerId = "container" + seed,
            Host = "srvhost" + seed,
            Port = 27000 + seed,
            AutostartDelay = 5,
            CreatedOn = D(42),
            UpdatedOn = D(43),
            ServerConsoles = new List<Entities.ServerConsole> { ServerConsole(seed + 1) },
            HttpPaths = new List<Entities.ServerHttpPath> { ServerHttpPath(seed + 2) },
            Scripts = new List<Entities.Script> { Script(seed + 3), Script(seed + 4, ScriptType.Package) },
            Actions = new List<Entities.Action> { Action(seed + 5) },
        };

    public static Entities.Tool Tool(int seed = 30)
        => new()
        {
            Id = G(seed),
            Name = "Tool " + seed,
            Description = "tool desc " + seed,
            Notes = "tool notes " + seed,
            CreatedOn = D(44),
            UpdatedOn = D(45),
            Actions = new List<Entities.Action> { Action(seed + 1) },
            Archives = new List<Entities.Archive> { Archive(seed + 2) },
            Scripts = new List<Entities.Script> { Script(seed + 3), Script(seed + 4, ScriptType.Package) },
        };

    public static Entities.GameVersion GameVersion(int seed = 40, bool withArchive = true)
        => new()
        {
            Id = G(seed),
            Version = "v" + seed,
            Changelog = "changelog " + seed,
            SortOrder = seed,
            GameId = G(seed + 700),
            Archive = withArchive ? Archive(seed + 1, "arch-" + seed) : null,
            CreatedOn = D(46),
            UpdatedOn = D(47),
        };

    public static Entities.Redistributable Redistributable(int seed = 50)
        => new()
        {
            Id = G(seed),
            Name = "Redist " + seed,
            Description = "redist desc",
            Notes = "redist notes",
            OptionSchema = "schema",
            CreatedOn = D(48),
            UpdatedOn = D(49),
            Archives = new List<Entities.Archive>
            {
                Archive(seed + 1, "1.0.0", 1),
                Archive(seed + 2, "2.0.0", 5), // latest by CreatedOn
            },
            Scripts = new List<Entities.Script> { Script(seed + 3), Script(seed + 4, ScriptType.Package) },
        };

    public static Entities.ChatMessage ChatMessage(int seed = 60, Entities.User? author = null)
        => new()
        {
            Id = G(seed),
            Content = "message " + seed,
            CreatedOn = D(50 + seed % 5),
            UpdatedOn = D(51),
            CreatedBy = author ?? User("author", seed + 800),
        };

    public static Entities.ChatThread ChatThread(int seed = 70, bool withMessages = true)
        => new()
        {
            Id = G(seed),
            Name = "Thread " + seed,
            CreatedOn = D(52),
            UpdatedOn = D(60),
            Participants = new List<Entities.User> { User("p1", seed + 1), User("p2", seed + 2) },
            Messages = withMessages
                ? new List<Entities.ChatMessage> { ChatMessage(seed + 3), ChatMessage(seed + 4) }
                : null,
        };

    /// <summary>A minimal addon game (no nested collections) used as a dependent game.</summary>
    public static Entities.Game AddonGame(int seed, GameType type)
        => new()
        {
            Id = G(seed),
            Title = "Addon " + seed,
            Type = type,
            Collections = new List<Entities.Collection>(),
            DependentGames = new List<Entities.Game>(),
            CreatedOn = D(53),
            UpdatedOn = D(54),
        };

    public static Entities.Game Game(int seed = 100)
    {
        var baseGame = new Entities.Game
        {
            Id = G(seed + 1),
            Title = "Base Game",
            Type = GameType.MainGame,
            Collections = new List<Entities.Collection>(),
            DependentGames = new List<Entities.Game>(),
            CreatedOn = D(55),
            UpdatedOn = D(56),
        };

        return new Entities.Game
        {
            Id = G(seed),
            Title = "Sample Game",
            SortTitle = "Sample Game, The",
            DirectoryName = "sample-game",
            Description = "A sample game description",
            Notes = "Some notes",
            ReleasedOn = D(100),
            Type = GameType.MainGame,
            Singleplayer = true,
            BaseGameId = baseGame.Id,
            BaseGame = baseGame,
            EngineId = G(seed + 2),
            Engine = Engine(seed + 2),
            CreatedOn = D(57),
            UpdatedOn = D(58),
            Actions = new List<Entities.Action> { Action(seed + 10) },
            Archives = new List<Entities.Archive>
            {
                Archive(seed + 11, "1.0.0", 1),
                Archive(seed + 12, "1.5.0", 6), // latest by CreatedOn
            },
            Versions = new List<Entities.GameVersion>
            {
                GameVersion(seed + 13),
                GameVersion(seed + 14),
            },
            Collections = new List<Entities.Collection> { Collection(seed + 15) },
            Genres = new List<Entities.Genre> { Genre(seed + 16) },
            Tags = new List<Entities.Tag> { Tag(seed + 17) },
            Platforms = new List<Entities.Platform> { Platform(seed + 18) },
            MultiplayerModes = new List<Entities.MultiplayerMode> { MultiplayerMode(seed + 19) },
            Keys = new List<Entities.Key> { Key(seed + 20) },
            Developers = new List<Entities.Company> { Company(seed + 21) },
            Publishers = new List<Entities.Company> { Company(seed + 22) },
            CustomFields = new List<Entities.GameCustomField> { GameCustomField(seed + 23) },
            ExternalIds = new List<Entities.GameExternalId> { GameExternalId(seed + 24) },
            PlaySessions = new List<Entities.PlaySession> { PlaySession(seed + 25) },
            SavePaths = new List<Entities.SavePath> { SavePath(seed + 26) },
            Media = new List<Entities.Media> { Media(seed + 27, MediaType.Background), Media(seed + 28, MediaType.Cover) },
            Scripts = new List<Entities.Script> { Script(seed + 29), Script(seed + 30, ScriptType.Package) },
            Servers = new List<Entities.Server> { Server(seed + 31) },
            Tools = new List<Entities.Tool> { Tool(seed + 32) },
            Redistributables = new List<Entities.Redistributable> { Redistributable(seed + 33) },
            GameSaves = new List<Entities.GameSave> { GameSave(seed + 34) },
            DependentGames = new List<Entities.Game>
            {
                AddonGame(seed + 35, GameType.Expansion),
                AddonGame(seed + 36, GameType.Mod),
            },
        };
    }
}
