using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GameAction = LANCommander.Server.Data.Models.Action;
using GameServer = LANCommander.Server.Data.Models.Server;
using static LANCommander.Server.UI.Fixtures.Data.FixtureData;

namespace LANCommander.Server.UI.Fixtures.Data;

/// <summary>
/// Fills an empty database with a known, well-populated data set: users and roles, taxonomy, games
/// with archives, keys, actions, scripts and DLC, redistributables, tools, servers, issues, pages
/// and play sessions.
/// </summary>
/// <remarks>
/// Everything is written through the real services, so the data is shaped exactly like data the
/// UI creates. Ids come from <see cref="FixtureIds.For"/> and, once seeding finishes, every audit
/// timestamp is rewritten from the entity's Id, so two seeded databases are identical.
///
/// Extend this as pages are migrated: a page that is hard to exercise without data should get the
/// data here, not in the test.
/// </remarks>
public sealed class FixtureDatabaseSeeder(IServiceProvider services)
{
    /// <summary>Seeds the database. <paramref name="storageRoot"/> holds the storage location folders.</summary>
    public async Task SeedAsync(string storageRoot)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        await SeedUsersAsync(provider);
        var storage = await SeedStorageLocationsAsync(provider, storageRoot);
        var taxonomy = await SeedTaxonomyAsync(provider);
        await SeedGamesAsync(provider, storage, taxonomy);
        await SeedRedistributablesAsync(provider, storage);
        await SeedToolsAsync(provider, storage);
        await SeedServersAsync(provider);
        await SeedIssuesAsync(provider);
        await SeedPagesAsync(provider);
        await SeedPlaySessionsAsync(provider);
        await SeedLibraryAsync(provider, storage);
        await SeedCatalogueAsync(provider, taxonomy);

        await NormalizeTimestampsAsync(provider);
    }

    /// <summary>True when the database already holds fixture data (or any games at all).</summary>
    public async Task<bool> IsSeededAsync()
    {
        using var scope = services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Games!.AnyAsync();
    }

    private static async Task SeedUsersAsync(IServiceProvider provider)
    {
        var roleService = provider.GetRequiredService<RoleService>();
        var userService = provider.GetRequiredService<UserService>();

        if (await roleService.GetAsync(RoleService.AdministratorRoleName) == null)
            await roleService.AddAsync(new Role { Id = FixtureIds.For("role:admin"), Name = RoleService.AdministratorRoleName });

        await roleService.AddAsync(new Role { Id = FixtureIds.For("role:players"), Name = PlayersRoleName });

        await AddUserAsync(userService, AdminUserName, AdminPassword, RoleService.AdministratorRoleName);

        foreach (var name in PlayerUserNames)
            await AddUserAsync(userService, name, PlayerPassword, PlayersRoleName);
    }

    private static async Task AddUserAsync(UserService userService, string userName, string password, string role)
    {
        if (await userService.GetAsync(userName) != null)
            return;

        await userService.AddAsync(new User
        {
            Id = FixtureIds.For("user:" + userName),
            UserName = userName,
            Alias = char.ToUpperInvariant(userName[0]) + userName[1..],
            Email = $"{userName}@lan.example",
            Approved = true,
            ApprovedOn = FixtureIds.Epoch.AddDays(-30),
        });

        await userService.ChangePassword(userName, password);
        await userService.AddToRoleAsync(userName, role);
    }

    private sealed record Storage(StorageLocation Archives, StorageLocation Saves, StorageLocation Media);

    private static async Task<Storage> SeedStorageLocationsAsync(IServiceProvider provider, string root)
    {
        var storageLocationService = provider.GetRequiredService<StorageLocationService>();

        async Task<StorageLocation> Add(StorageLocationType type, string folder)
        {
            var path = Path.Combine(root, folder);

            Directory.CreateDirectory(path);

            return await storageLocationService.AddAsync(new StorageLocation
            {
                Id = FixtureIds.For("storage:" + folder),
                Path = path,
                Type = type,
                Default = true,
            });
        }

        return new Storage(
            await Add(StorageLocationType.Archive, "Archives"),
            await Add(StorageLocationType.Save, "Saves"),
            await Add(StorageLocationType.Media, "Media"));
    }

    private sealed record Taxonomy(
        Dictionary<string, Genre> Genres,
        Dictionary<string, Tag> Tags,
        Dictionary<string, Platform> Platforms,
        Dictionary<string, Company> Companies,
        Dictionary<string, Engine> Engines,
        Dictionary<string, Collection> Collections);

    private static async Task<Taxonomy> SeedTaxonomyAsync(IServiceProvider provider)
    {
        async Task<Dictionary<string, T>> Add<T>(BaseDatabaseService<T> service, string kind, params string[] names)
            where T : BaseTaxonomyModel, new()
        {
            var result = new Dictionary<string, T>();

            foreach (var name in names)
                result[name] = await service.AddAsync(new T { Id = FixtureIds.For($"{kind}:{name}"), Name = name });

            return result;
        }

        return new Taxonomy(
            await Add(provider.GetRequiredService<GenreService>(), "genre",
                "Action", "First-Person Shooter", "Racing", "Real-Time Strategy", "Role-Playing", "Tower Defense"),
            await Add(provider.GetRequiredService<TagService>(), "tag",
                "LAN Classic", "Co-op", "Split-screen", "Mod Support", "Dedicated Server"),
            await Add(provider.GetRequiredService<PlatformService>(), "platform",
                "Windows", "DOS", "Linux"),
            await Add(provider.GetRequiredService<CompanyService>(), "company",
                "Pixel Forge", "Northwind Interactive", "Blue Harbor Games", "Lantern Publishing"),
            await Add(provider.GetRequiredService<EngineService>(), "engine",
                "Forge Engine", "Northwind Tech"),
            await Add(provider.GetRequiredService<CollectionService>(), "collection",
                "LAN Party Essentials", "Retro Night"));
    }

    private sealed record GameSpec(
        string Title,
        string Description,
        int ReleaseYear,
        string[] Genres,
        string[] Tags,
        string Developer,
        string Publisher,
        string? Engine,
        string[] Collections,
        int MaxPlayers,
        int KeyCount = 0,
        int ClaimedKeys = 0,
        GameType Type = GameType.MainGame,
        string? BaseGame = null);

    private static readonly GameSpec[] GameSpecs =
    [
        new(Games.ArenaBlitz,
            "A fast arena shooter built for sixteen players on one switch.",
            1999, ["Action", "First-Person Shooter"], ["LAN Classic", "Dedicated Server", "Mod Support"],
            "Pixel Forge", "Lantern Publishing", "Forge Engine", ["LAN Party Essentials"], 16, KeyCount: 8, ClaimedKeys: 3),
        new(Games.ArenaBlitzMapPack,
            "Twelve new arenas for Arena Blitz.",
            2000, ["Action", "First-Person Shooter"], [],
            "Pixel Forge", "Lantern Publishing", "Forge Engine", [], 16, Type: GameType.Expansion, BaseGame: Games.ArenaBlitz),
        new(Games.StarfallTactics,
            "Command fleets across a collapsing star cluster.",
            2002, ["Real-Time Strategy"], ["LAN Classic", "Co-op"],
            "Northwind Interactive", "Northwind Interactive", "Northwind Tech", ["LAN Party Essentials"], 8, KeyCount: 4, ClaimedKeys: 4),
        new(Games.StarfallTacticsFrozenFront,
            "A new campaign and two factions set on the ice worlds.",
            2003, ["Real-Time Strategy"], [],
            "Northwind Interactive", "Northwind Interactive", "Northwind Tech", [], 8, Type: GameType.StandaloneExpansion, BaseGame: Games.StarfallTactics),
        new(Games.NeonDrift,
            "Split-screen street racing under neon lights.",
            2004, ["Racing"], ["Split-screen", "Co-op"],
            "Blue Harbor Games", "Lantern Publishing", null, ["LAN Party Essentials"], 4),
        new(Games.DungeonDelvers,
            "Co-operative dungeon crawling for up to six adventurers.",
            2001, ["Role-Playing", "Action"], ["Co-op", "Mod Support"],
            "Blue Harbor Games", "Blue Harbor Games", null, [], 6, KeyCount: 2),
        new(Games.FrontierFortress,
            "Hold the line against waves of raiders with your friends.",
            2006, ["Tower Defense", "Real-Time Strategy"], ["Co-op"],
            "Pixel Forge", "Pixel Forge", "Forge Engine", [], 4),
        new(Games.RetroRumble,
            "Couch brawler from the golden age of DOS.",
            1994, ["Action"], ["Split-screen", "LAN Classic"],
            "Lantern Publishing", "Lantern Publishing", null, ["Retro Night"], 4),
    ];

    private static async Task SeedGamesAsync(IServiceProvider provider, Storage storage, Taxonomy taxonomy)
    {
        var gameService = provider.GetRequiredService<GameService>();
        var archiveService = provider.GetRequiredService<ArchiveService>();
        var keyService = provider.GetRequiredService<KeyService>();
        var actionService = provider.GetRequiredService<ActionService>();
        var scriptService = provider.GetRequiredService<ScriptService>();
        var savePathService = provider.GetRequiredService<SavePathService>();
        var multiplayerModeService = provider.GetRequiredService<MultiplayerModeService>();

        var playerIds = PlayerUserNames.Select(n => FixtureIds.For("user:" + n)).ToArray();

        foreach (var spec in GameSpecs)
        {
            var gameId = Games.Id(spec.Title);
            var isMainGame = spec.Type == GameType.MainGame;

            await gameService.AddAsync(new Game
            {
                Id = gameId,
                Title = spec.Title,
                SortTitle = spec.Title,
                DirectoryName = spec.Title.Replace(":", ""),
                Description = spec.Description,
                ReleasedOn = new DateTime(spec.ReleaseYear, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                Type = spec.Type,
                BaseGameId = spec.BaseGame == null ? null : Games.Id(spec.BaseGame),
                Singleplayer = true,
                KeyAllocationMethod = KeyAllocationMethod.UserAccount,
                EngineId = spec.Engine == null ? null : taxonomy.Engines[spec.Engine].Id,
                Genres = spec.Genres.Select(g => taxonomy.Genres[g]).ToList(),
                Tags = spec.Tags.Select(t => taxonomy.Tags[t]).ToList(),
                Platforms = [taxonomy.Platforms[spec.ReleaseYear < 1996 ? "DOS" : "Windows"]],
                Developers = [taxonomy.Companies[spec.Developer]],
                Publishers = spec.Developer == spec.Publisher
                    ? [taxonomy.Companies[spec.Developer]]
                    : [taxonomy.Companies[spec.Publisher]],
                Collections = spec.Collections.Select(c => taxonomy.Collections[c]).ToList(),
            });

            // Two archive versions for main games so version lists and "latest" logic have something to show
            var versions = isMainGame ? new[] { "1.0.0", "1.1.2" } : ["1.0.0"];

            for (var v = 0; v < versions.Length; v++)
            {
                await archiveService.AddAsync(new Archive
                {
                    Id = FixtureIds.For($"archive:{spec.Title}:{versions[v]}"),
                    GameId = gameId,
                    Version = versions[v],
                    ObjectKey = FixtureIds.For($"object:{spec.Title}:{versions[v]}").ToString(),
                    StorageLocationId = storage.Archives.Id,
                    CompressedSize = (spec.ReleaseYear % 7 + 1) * 180_000_000L + v * 12_000_000L,
                    UncompressedSize = (spec.ReleaseYear % 7 + 1) * 410_000_000L + v * 30_000_000L,
                });
            }

            if (isMainGame)
            {
                var executable = spec.Title.Replace(" ", "").Replace(":", "") + ".exe";

                await actionService.AddAsync(new GameAction
                {
                    Id = FixtureIds.For($"action:{spec.Title}:play"),
                    GameId = gameId,
                    Name = "Play",
                    Path = executable,
                    WorkingDirectory = "{InstallDir}",
                    PrimaryAction = true,
                    SortOrder = 0,
                });

                await actionService.AddAsync(new GameAction
                {
                    Id = FixtureIds.For($"action:{spec.Title}:settings"),
                    GameId = gameId,
                    Name = "Settings",
                    Path = executable,
                    Arguments = "-config",
                    WorkingDirectory = "{InstallDir}",
                    SortOrder = 1,
                });

                await multiplayerModeService.AddAsync(new MultiplayerMode
                {
                    Id = FixtureIds.For($"multiplayer:{spec.Title}:lan"),
                    GameId = gameId,
                    Type = MultiplayerType.LAN,
                    MinPlayers = 2,
                    MaxPlayers = spec.MaxPlayers,
                    Description = "Local network play",
                });

                await savePathService.AddAsync(new SavePath
                {
                    Id = FixtureIds.For($"savepath:{spec.Title}"),
                    GameId = gameId,
                    Type = SavePathType.File,
                    Path = "Saves",
                    WorkingDirectory = "{InstallDir}",
                });

                await scriptService.AddAsync(new Script
                {
                    Id = FixtureIds.For($"script:{spec.Title}:namechange"),
                    GameId = gameId,
                    Name = "Set player name",
                    Type = ScriptType.NameChange,
                    Contents = "$NewPlayerAlias = $args[0]\nWrite-Host \"Setting player name to $NewPlayerAlias\"",
                });
            }

            for (var k = 0; k < spec.KeyCount; k++)
            {
                var claimed = k < spec.ClaimedKeys;

                await keyService.AddAsync(new Key
                {
                    Id = FixtureIds.For($"key:{spec.Title}:{k}"),
                    GameId = gameId,
                    Value = $"{spec.Title[..3].ToUpperInvariant()}-{1000 + k * 137:D4}-{7000 - k * 211:D4}",
                    AllocationMethod = KeyAllocationMethod.UserAccount,
                    ClaimedByUserId = claimed ? playerIds[k % playerIds.Length] : null,
                    ClaimedOn = claimed ? FixtureIds.Epoch.AddDays(-k - 1) : null,
                });
            }
        }
    }

    private static async Task SeedRedistributablesAsync(IServiceProvider provider, Storage storage)
    {
        var redistributableService = provider.GetRequiredService<RedistributableService>();
        var archiveService = provider.GetRequiredService<ArchiveService>();
        var scriptService = provider.GetRequiredService<ScriptService>();

        async Task Add(string name, string description, string[] games)
        {
            var id = Redistributables.Id(name);

            await redistributableService.AddAsync(new Redistributable
            {
                Id = id,
                Name = name,
                Description = description,
                Games = games.Select(g => new Game { Id = Games.Id(g) }).ToList(),
            });

            await archiveService.AddAsync(new Archive
            {
                Id = FixtureIds.For("archive:" + name),
                RedistributableId = id,
                Version = "1.0",
                ObjectKey = FixtureIds.For("object:" + name).ToString(),
                StorageLocationId = storage.Archives.Id,
                CompressedSize = 95_000_000,
                UncompressedSize = 140_000_000,
            });

            await scriptService.AddAsync(new Script
            {
                Id = FixtureIds.For("script:" + name + ":detect"),
                RedistributableId = id,
                Name = "Detect install",
                Type = ScriptType.DetectInstall,
                Contents = "return $false",
            });
        }

        await Add(Redistributables.DirectX, "Legacy DirectX runtime components.",
            [Games.ArenaBlitz, Games.StarfallTactics, Games.NeonDrift]);
        await Add(Redistributables.VisualCpp, "Microsoft Visual C++ runtime libraries.",
            [Games.FrontierFortress, Games.DungeonDelvers]);
    }

    private static async Task SeedToolsAsync(IServiceProvider provider, Storage storage)
    {
        var toolService = provider.GetRequiredService<ToolService>();
        var archiveService = provider.GetRequiredService<ArchiveService>();

        await toolService.AddAsync(new Tool
        {
            Id = Tools.Id(Tools.MapEditor),
            Name = Tools.MapEditor,
            Description = "Official level editor for Arena Blitz.",
            Games = [new Game { Id = Games.Id(Games.ArenaBlitz) }],
        });

        await archiveService.AddAsync(new Archive
        {
            Id = FixtureIds.For("archive:" + Tools.MapEditor),
            ToolId = Tools.Id(Tools.MapEditor),
            Version = "2.0",
            ObjectKey = FixtureIds.For("object:" + Tools.MapEditor).ToString(),
            StorageLocationId = storage.Archives.Id,
            CompressedSize = 24_000_000,
            UncompressedSize = 61_000_000,
        });

        await toolService.AddAsync(new Tool
        {
            Id = Tools.Id(Tools.ServerBrowser),
            Name = Tools.ServerBrowser,
            Description = "Finds game servers on the local network.",
            AlwaysInstall = true,
        });
    }

    private static async Task SeedServersAsync(IServiceProvider provider)
    {
        var serverService = provider.GetRequiredService<ServerService>();
        var httpPathService = provider.GetRequiredService<ServerHttpPathService>();

        await serverService.AddAsync(new GameServer
        {
            Id = Servers.Id(Servers.ArenaBlitzDedicated),
            Name = Servers.ArenaBlitzDedicated,
            GameId = Games.Id(Games.ArenaBlitz),
            Path = @"C:\Servers\ArenaBlitz\ArenaBlitzServer.exe",
            WorkingDirectory = @"C:\Servers\ArenaBlitz",
            Arguments = "+maxplayers 16 +map arena01",
            Host = "0.0.0.0",
            Port = 27960,
        });

        await httpPathService.AddAsync(new ServerHttpPath
        {
            Id = FixtureIds.For("httppath:" + Servers.ArenaBlitzDedicated),
            ServerId = Servers.Id(Servers.ArenaBlitzDedicated),
            LocalPath = @"C:\Servers\ArenaBlitz\maps",
            Path = "/maps",
        });

        await serverService.AddAsync(new GameServer
        {
            Id = Servers.Id(Servers.StarfallHost),
            Name = Servers.StarfallHost,
            GameId = Games.Id(Games.StarfallTactics),
            Path = @"C:\Servers\Starfall\StarfallHost.exe",
            WorkingDirectory = @"C:\Servers\Starfall",
            Host = "0.0.0.0",
            Port = 6112,
        });
    }

    private static async Task SeedIssuesAsync(IServiceProvider provider)
    {
        var issueService = provider.GetRequiredService<IssueService>();

        await issueService.AddAsync(new Issue
        {
            Id = FixtureIds.For("issue:neon-drift-controller"),
            GameId = Games.Id(Games.NeonDrift),
            Description = "Second controller is not detected in split-screen mode.",
        });

        await issueService.AddAsync(new Issue
        {
            Id = FixtureIds.For("issue:arena-blitz-crash"),
            GameId = Games.Id(Games.ArenaBlitz),
            Description = "Crash when loading arena07 with more than 12 players.",
        });

        // Resolved through the service so the resolver is recorded; the time is pinned afterwards
        await issueService.ResolveAsync(FixtureIds.For("issue:arena-blitz-crash"), AdminId);
    }

    private static async Task SeedPagesAsync(IServiceProvider provider)
    {
        var pageService = provider.GetRequiredService<PageService>();

        var welcome = await pageService.AddAsync(new Page
        {
            Id = FixtureIds.For("page:welcome"),
            Title = "Welcome",
            Slug = "",
            Contents = "# Welcome to the LAN\n\nGrab a seat, plug in, and check the house rules.",
            SortOrder = 0,
        });

        await pageService.AddAsync(new Page
        {
            Id = FixtureIds.For("page:house-rules"),
            Title = "House Rules",
            Slug = "",
            Contents = "1. No spawn camping.\n2. Headphones on.\n3. Winner stays on.",
            SortOrder = 0,
        });

        // As the page editor does, so the child's route is nested under its parent's
        await pageService.ChangeParentAsync(FixtureIds.For("page:house-rules"), welcome.Id);
    }

    private static async Task SeedPlaySessionsAsync(IServiceProvider provider)
    {
        var playSessionService = provider.GetRequiredService<PlaySessionService>();

        string[] played = [Games.ArenaBlitz, Games.StarfallTactics, Games.NeonDrift, Games.DungeonDelvers];

        for (var p = 0; p < PlayerUserNames.Length; p++)
        {
            for (var g = 0; g < played.Length; g++)
            {
                // Varied but fixed session lengths so dashboard charts rank games and players distinctly
                var sessions = (p + g) % 3 + 1;

                for (var s = 0; s < sessions; s++)
                {
                    var start = FixtureIds.Epoch.AddDays(-(p * 5 + g * 2 + s + 1)).AddHours(-(p + g));

                    await playSessionService.AddAsync(new PlaySession
                    {
                        Id = FixtureIds.For($"session:{PlayerUserNames[p]}:{played[g]}:{s}"),
                        UserId = FixtureIds.For("user:" + PlayerUserNames[p]),
                        GameId = Games.Id(played[g]),
                        Start = start,
                        End = start.AddMinutes(35 + 25 * ((p * 3 + g * 5 + s) % 7)),
                    });
                }
            }
        }
    }

    /// <summary>The administrator's own library and cloud saves, for the Profile pages.</summary>
    private static async Task SeedLibraryAsync(IServiceProvider provider, Storage storage)
    {
        var libraryService = provider.GetRequiredService<LibraryService>();
        var gameSaveService = provider.GetRequiredService<GameSaveService>();

        foreach (var title in new[] { Games.ArenaBlitz, Games.StarfallTactics, Games.NeonDrift })
            await libraryService.AddToLibraryAsync(AdminId, Games.Id(title));

        (string Game, int Index, long Size)[] saves =
        [
            (Games.ArenaBlitz, 0, 184_320),
            (Games.ArenaBlitz, 1, 196_608),
            (Games.StarfallTactics, 0, 2_411_724),
        ];

        foreach (var save in saves)
        {
            await gameSaveService.AddAsync(new GameSave
            {
                Id = FixtureIds.For($"save:{AdminUserName}:{save.Game}:{save.Index}"),
                GameId = Games.Id(save.Game),
                UserId = AdminId,
                StorageLocationId = storage.Saves.Id,
                Size = save.Size,
            });
        }
    }

    /// <summary>
    /// Services stamp CreatedOn/UpdatedOn with the wall clock; replace them with values derived from
    /// each entity's Id so captures of seeded data never change between runs.
    /// </summary>
    private static readonly Guid AdminId = FixtureIds.For("user:" + AdminUserName);

    private static readonly string[] CatalogueAdjectives =
    [
        "Iron", "Crimson", "Silent", "Hollow", "Neon", "Frozen", "Burning", "Shattered", "Golden", "Rogue",
        "Midnight", "Savage", "Electric", "Lost", "Broken", "Hidden", "Ancient", "Orbital", "Rusted", "Wild",
        "Phantom", "Solar", "Lunar", "Distant", "Last", "Twin", "Velvet", "Obsidian", "Scarlet", "Arctic",
        "Echo", "Storm", "Emerald", "Sunken", "Cobalt", "Feral", "Grim", "Radiant", "Hyper", "Silver",
        "Quantum", "Bitter", "Endless", "Ghost", "Sable", "Thunder", "Paper", "Glass", "Stone", "Copper",
    ];

    private static readonly string[] CatalogueNouns =
    [
        "Harbor", "Circuit", "Frontier", "Legion", "Outpost", "Protocol", "Kingdom", "Drift", "Siege", "Horizon",
        "Garrison", "Vanguard", "Relic", "Citadel", "Wasteland", "Armada", "Colony", "Tactics", "Rally", "Dominion",
        "Station", "Reactor", "Carnival", "Labyrinth", "Expanse", "Crusade", "Syndicate", "Arena", "Voyage", "Uprising",
        "Hunters", "Raiders", "Tempest", "Engine", "Signal", "Descent", "Empire", "Bastion", "Rebellion", "Odyssey",
        "Nexus", "Canyon", "Forge", "Pursuit", "Overdrive", "Paradox", "Summit", "Assault", "Chronicle", "Squadron",
    ];

    private static readonly string[] CatalogueSuffixes = ["", "", "", " II", " III", ": Remastered", " Online", " Deluxe", ": Gold Edition", " 2000"];

    private static readonly string[] CatalogueGenres = ["Adventure", "Puzzle", "Simulation", "Sports", "Fighting", "Platformer", "Stealth", "Survival"];

    private static readonly string[] CatalogueCompanies =
    [
        "Redline Software", "Quiet Owl Studios", "Hexagon Works", "Tundra Interactive", "Meridian Games",
        "Brightside Digital", "Cinder Labs", "Longbow Entertainment", "Pocket Comet", "Stormglass",
    ];

    /// <summary>
    /// A deterministic, generated library of <see cref="Games.CatalogueSize"/> titles spread across
    /// the taxonomy. Written straight to the context rather than through GameService one game at a
    /// time: these rows only need to exist and relate, and thousands of service round trips would
    /// make every seeded test run slow.
    /// </summary>
    private static async Task SeedCatalogueAsync(IServiceProvider provider, Taxonomy taxonomy)
    {
        var contextFactory = provider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        var genres = await context.Set<Genre>().ToListAsync();
        var tags = await context.Set<Tag>().ToListAsync();
        var platforms = await context.Set<Platform>().ToListAsync();
        var companies = await context.Set<Company>().ToListAsync();
        var engines = await context.Set<Engine>().ToListAsync();
        var collections = await context.Set<Collection>().ToListAsync();

        foreach (var name in CatalogueGenres)
        {
            var genre = new Genre { Id = FixtureIds.For("genre:" + name), Name = name };
            context.Add(genre);
            genres.Add(genre);
        }

        foreach (var name in CatalogueCompanies)
        {
            var company = new Company { Id = FixtureIds.For("company:" + name), Name = name };
            context.Add(company);
            companies.Add(company);
        }

        // Stable order, so the same index always picks the same entity
        genres = genres.OrderBy(g => g.Name).ToList();
        tags = tags.OrderBy(t => t.Name).ToList();
        platforms = platforms.OrderBy(p => p.Name).ToList();
        companies = companies.OrderBy(c => c.Name).ToList();
        engines = engines.OrderBy(e => e.Name).ToList();
        collections = collections.OrderBy(c => c.Name).ToList();

        var titles = new HashSet<string>(GameSpecs.Select(s => s.Title));
        var index = 0;

        for (var n = 0; titles.Count < GameSpecs.Length + Games.CatalogueSize; n++)
        {
            var hash = FixtureIds.For("catalogue:" + n).ToByteArray();
            int Pick(int offset, int count) => hash[offset] % count;

            var title = CatalogueAdjectives[Pick(0, CatalogueAdjectives.Length)] + " "
                        + CatalogueNouns[Pick(1, CatalogueNouns.Length)]
                        + CatalogueSuffixes[Pick(2, CatalogueSuffixes.Length)];

            if (!titles.Add(title))
                continue;

            var year = 1990 + Pick(3, 26);
            var developer = companies[Pick(4, companies.Count)];
            var publisher = hash[5] % 3 == 0 ? developer : companies[Pick(6, companies.Count)];
            var game = new Game
            {
                Id = FixtureIds.For("catalogue-game:" + title),
                Title = title,
                SortTitle = title,
                DirectoryName = title.Replace(":", ""),
                Description = $"{title} is a {year} release from {developer.Name}.",
                ReleasedOn = new DateTime(year, 1 + Pick(7, 12), 1 + Pick(8, 28), 0, 0, 0, DateTimeKind.Utc),
                Type = GameType.MainGame,
                Singleplayer = hash[9] % 5 != 0,
                KeyAllocationMethod = KeyAllocationMethod.UserAccount,
                EngineId = hash[10] % 3 == 0 ? engines[Pick(11, engines.Count)].Id : null,
                Genres = new[] { genres[Pick(12, genres.Count)], genres[Pick(13, genres.Count)] }.Distinct().ToList(),
                Tags = hash[14] % 2 == 0 ? [tags[Pick(15, tags.Count)]] : [],
                Platforms = [platforms[year < 1996 ? platforms.FindIndex(p => p.Name == "DOS") : Pick(1, 7) == 0 ? platforms.FindIndex(p => p.Name == "Linux") : platforms.FindIndex(p => p.Name == "Windows")]],
                Developers = [developer],
                Publishers = [publisher],
                Collections = hash[2] % 11 == 0 ? [collections[Pick(3, collections.Count)]] : [],
                MultiplayerModes = hash[4] % 3 == 0
                    ? [new MultiplayerMode
                    {
                        Id = FixtureIds.For("catalogue-multiplayer:" + title),
                        Type = (MultiplayerType)(hash[6] % 3),
                        MinPlayers = 2,
                        MaxPlayers = 2 + hash[7] % 15,
                    }]
                    : [],
            };

            context.Add(game);

            // Save in batches to keep the change tracker small
            if (++index % 500 == 0)
                await context.SaveChangesAsync();
        }

        await context.SaveChangesAsync();
    }

    private static async Task NormalizeTimestampsAsync(IServiceProvider provider)
    {
        var contextFactory = provider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var idProperty = entityType.FindProperty("Id");
            var createdOn = entityType.FindProperty("CreatedOn");

            if (idProperty?.ClrType != typeof(Guid) || createdOn == null || entityType.IsOwned())
                continue;

            var updatedOn = entityType.FindProperty("UpdatedOn");
            var createdBy = entityType.FindProperty("CreatedById");
            var updatedBy = entityType.FindProperty("UpdatedById");

            var set = (IQueryable<object>)typeof(DbContext)
                .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(context, null)!;

            var resolvedOn = entityType.FindProperty("ResolvedOn");

            foreach (var entity in await set.ToListAsync())
            {
                var entry = context.Entry(entity);
                var id = (Guid)entry.Property("Id").CurrentValue!;

                entry.Property("CreatedOn").CurrentValue = FixtureIds.CreatedOn(id);

                if (updatedOn != null)
                    entry.Property("UpdatedOn").CurrentValue = FixtureIds.UpdatedOn(id);

                // Seeding runs without a signed-in user; attribute everything to the administrator
                if (createdBy != null)
                    entry.Property("CreatedById").CurrentValue = AdminId;

                if (updatedBy != null)
                    entry.Property("UpdatedById").CurrentValue = AdminId;

                if (resolvedOn != null && entry.Property("ResolvedOn").CurrentValue != null)
                    entry.Property("ResolvedOn").CurrentValue = FixtureIds.Epoch.AddDays(-3);
            }
        }

        await context.SaveChangesAsync();
    }
}
