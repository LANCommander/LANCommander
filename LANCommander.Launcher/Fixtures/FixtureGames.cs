#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.SDK.Enums;

namespace LANCommander.Launcher.Fixtures;

/// <summary>A game as fixtures know it. Ids and art are derived from the title, so both are stable.</summary>
public sealed record FixtureGame(
    string Title,
    int Year,
    string Genres,
    string Developers,
    string Publishers,
    string Description)
{
    public string Collections { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;
    public bool Singleplayer { get; init; } = true;
    public int LocalPlayers { get; init; }
    public int LanPlayers { get; init; }
    public int OnlinePlayers { get; init; }
    public bool InLibrary { get; init; }
    public bool Installed { get; init; }
    public bool UpdateAvailable { get; init; }
    public GameType Type { get; init; } = GameType.MainGame;

    public Guid Id => FixtureGames.IdFor(Title);

    public DateTime ReleasedOn => new(Year, 6, 1);

    public int MaxPlayers => Math.Max(LocalPlayers, Math.Max(LanPlayers, OnlinePlayers));

    public string CoverPath => FixtureArt.Cover(Title);

    public string BackgroundPath => FixtureArt.Background(Title);

    public string IconPath => FixtureArt.Icon(Title);

    public string LogoPath => FixtureArt.Logo(Title);

    /// <param name="withArt">False renders the no-cover placeholder.</param>
    /// <param name="libraryBadge">Whether an in-library game shows its badge (the depot does, the library doesn't).</param>
    public GameItemViewModel ToItem(bool withArt = true, bool libraryBadge = true) => new()
    {
        Id = Id,
        Title = Title,
        SortTitle = Title.StartsWith("The ", StringComparison.Ordinal) ? Title[4..] : Title,
        Description = Description,
        ReleasedOn = ReleasedOn,
        Type = Type,
        Singleplayer = Singleplayer,
        Genres = Genres,
        Collections = Collections,
        Developers = Developers,
        Publishers = Publishers,
        Tags = Tags,
        HasLocalMultiplayer = LocalPlayers > 0,
        HasLanMultiplayer = LanPlayers > 0,
        HasOnlineMultiplayer = OnlinePlayers > 0,
        MaxPlayers = MaxPlayers,
        CoverPath = withArt ? CoverPath : null,
        HasCover = withArt,
        IconPath = withArt ? IconPath : null,
        HeroPath = withArt ? BackgroundPath : null,
        LogoPath = withArt ? LogoPath : null,
        IsInstalled = Installed,
        IsUpdateAvailable = UpdateAvailable,
        InLibrary = InLibrary,
        ShowInLibraryBadge = InLibrary && libraryBadge,
    };

    /// <summary>"Single-player" / "2–16 LAN" lines, as the details card shows them.</summary>
    public IEnumerable<string> PlayerModes()
    {
        if (Singleplayer)
            yield return "Single-player";

        if (LocalPlayers > 0)
            yield return $"2–{LocalPlayers} Local";

        if (LanPlayers > 0)
            yield return $"2–{LanPlayers} LAN";

        if (OnlinePlayers > 0)
            yield return $"2–{OnlinePlayers} Online";
    }
}

/// <summary>The canned catalog every fixture draws from.</summary>
public static class FixtureGames
{
    public static readonly FixtureGame AgeOfEmpires2 = new(
        "Age of Empires II", 1999, "Strategy, Real-Time Strategy", "Ensemble Studios", "Microsoft",
        "Guide a civilization from the Dark Age to the Imperial Age, raising armies and wonders across a thousand years of history.")
    {
        Collections = "LAN Party Classics, Strategy Night",
        Tags = "Classic, Historical, Base Building, Multiplayer, Competitive",
        LanPlayers = 8, OnlinePlayers = 8,
        InLibrary = true, Installed = true, UpdateAvailable = true,
    };

    public static readonly FixtureGame Battlefield1942 = new(
        "Battlefield 1942", 2002, "Action, First-Person Shooter", "DICE", "Electronic Arts",
        "Fight across the major theaters of the Second World War on foot, in tanks, in planes and aboard battleships.")
    {
        Collections = "LAN Party Classics",
        Tags = "Classic, Vehicles, Large Battles, World War II, Multiplayer, Team-Based",
        LanPlayers = 64, OnlinePlayers = 64,
        InLibrary = true, Installed = true,
    };

    public static readonly FixtureGame CounterStrike = new(
        "Counter-Strike", 2000, "Action, First-Person Shooter", "Valve", "Valve",
        "Terrorists and counter-terrorists face off in tight, round-based matches where every bullet counts.")
    {
        Collections = "LAN Party Classics, Shooters",
        Tags = "Tactical, Competitive, Team-Based, Multiplayer",
        Singleplayer = false, LanPlayers = 32, OnlinePlayers = 32,
        InLibrary = true, Installed = true,
    };

    public static readonly FixtureGame Diablo2 = new(
        "Diablo II", 2000, "Role-Playing, Action", "Blizzard North", "Blizzard Entertainment",
        "Hunt the Lord of Terror across four acts of loot, skill trees and relentless hordes.")
    {
        Collections = "Co-op",
        Tags = "Dungeon Crawler, Loot, Co-op, Dark Fantasy",
        LanPlayers = 8, OnlinePlayers = 8,
        InLibrary = true,
    };

    public static readonly FixtureGame Doom2 = new(
        "Doom II", 1994, "Action, First-Person Shooter", "id Software", "GT Interactive",
        "Hell has come to Earth, and only a lone marine stands between the demons and what's left of humanity.")
    {
        Collections = "Shooters",
        Tags = "Classic, Fast-Paced, Demons, Deathmatch",
        LanPlayers = 4,
        InLibrary = true, Installed = true,
    };

    public static readonly FixtureGame HalfLife = new(
        "Half-Life", 1998, "Action, First-Person Shooter", "Valve", "Sierra Studios",
        "A theoretical physicist fights his way out of a research facility after an experiment tears open a door to another world.")
    {
        Collections = "Shooters",
        Tags = "Classic, Sci-fi, Story Rich, Single-player",
        LanPlayers = 16,
    };

    public static readonly FixtureGame Halo = new(
        "Halo: Combat Evolved", 2003, "Action, First-Person Shooter", "Bungie", "Microsoft",
        "Master Chief lands on a mysterious ring world and uncovers the secret the Covenant would destroy everything to control.")
    {
        Collections = "LAN Party Classics, Shooters",
        Tags = "Sci-fi, Vehicles, Co-op",
        LanPlayers = 16,
        InLibrary = true,
    };

    public static readonly FixtureGame NaturalSelection2 = new(
        "Natural Selection 2", 2012, "Action, Strategy", "Unknown Worlds Entertainment", "Unknown Worlds Entertainment",
        "Marines and aliens battle for control of a derelict station, with a commander directing each side from above.")
    {
        Tags = "Asymmetric, Team-Based, Sci-fi",
        Singleplayer = false, LanPlayers = 24, OnlinePlayers = 24,
    };

    public static readonly FixtureGame Quake3 = new(
        "Quake III Arena", 1999, "Action, First-Person Shooter", "id Software", "Activision",
        "Pure arena combat: rocket jumps, railguns and the fastest deathmatches ever put on a LAN.")
    {
        Collections = "LAN Party Classics, Shooters",
        Tags = "Arena, Fast-Paced, Deathmatch, Competitive",
        LanPlayers = 16, OnlinePlayers = 16,
        InLibrary = true, Installed = true,
    };

    public static readonly FixtureGame RedAlert2 = new(
        "Command & Conquer: Red Alert 2", 2000, "Strategy, Real-Time Strategy", "Westwood Pacific", "Electronic Arts",
        "The Soviets invade the United States and only the Allied commander can turn back the tide.")
    {
        Collections = "Strategy Night",
        Tags = "Classic, Base Building, Cold War",
        LanPlayers = 8,
        InLibrary = true, Installed = true,
    };

    public static readonly FixtureGame Soldat = new(
        "Soldat", 2002, "Action, Platformer", "Michał Marcinkowski", "Michał Marcinkowski",
        "A frantic side-scrolling shooter with jet boots, ragdoll physics and twelve-player free-for-alls.")
    {
        Tags = "2D, Fast-Paced, Free to Play",
        LanPlayers = 32, OnlinePlayers = 32,
    };

    public static readonly FixtureGame StarCraft = new(
        "StarCraft", 1998, "Strategy, Real-Time Strategy", "Blizzard Entertainment", "Blizzard Entertainment",
        "Terran, Zerg and Protoss wage war across the Koprulu sector in the real-time strategy game that defined a genre.")
    {
        Collections = "LAN Party Classics, Strategy Night",
        Tags = "Classic, Sci-fi, Competitive, Base Building",
        LanPlayers = 8,
        InLibrary = true,
    };

    public static readonly FixtureGame Battlefront2 = new(
        "Star Wars: Battlefront II", 2005, "Action, Third-Person Shooter", "Pandemic Studios", "LucasArts",
        "Clone troopers and droids clash on the ground and in space in the galaxy's largest battles.")
    {
        Tags = "Sci-fi, Vehicles, Space",
        LanPlayers = 24,
    };

    public static readonly FixtureGame TeamFortress = new(
        "Team Fortress Classic", 1999, "Action, First-Person Shooter", "Valve", "Valve",
        "Nine classes, two teams and one flag: the original class-based multiplayer shooter.")
    {
        Collections = "Shooters",
        Tags = "Team-Based, Classes, Capture the Flag",
        Singleplayer = false, LanPlayers = 32,
    };

    public static readonly FixtureGame UnrealTournament2004 = new(
        "Unreal Tournament 2004", 2004, "Action, First-Person Shooter", "Epic Games", "Atari",
        "Onslaught, Assault and Deathmatch: the tournament returns with vehicles and more modes than ever.")
    {
        Collections = "LAN Party Classics, Shooters",
        Tags = "Arena, Vehicles, Fast-Paced, Mods",
        LanPlayers = 32, OnlinePlayers = 32,
        InLibrary = true, Installed = true,
    };

    public static readonly FixtureGame Warcraft3 = new(
        "Warcraft III: Reign of Chaos", 2002, "Strategy, Real-Time Strategy", "Blizzard Entertainment", "Blizzard Entertainment",
        "Heroes lead the Human Alliance, the Orcish Horde, the Night Elves and the Undead Scourge in a war for Azeroth.")
    {
        Collections = "Strategy Night",
        Tags = "Fantasy, Heroes, Custom Maps",
        LanPlayers = 12,
        InLibrary = true,
    };

    public static readonly FixtureGame Worms = new(
        "Worms Armageddon", 1999, "Strategy, Artillery", "Team17", "MicroProse",
        "Turn-based warfare between teams of heavily armed worms, featuring the holy hand grenade.")
    {
        Collections = "Party",
        Tags = "Turn-Based, Comedy, Hot-Seat",
        LocalPlayers = 6, LanPlayers = 6,
    };

    public static readonly FixtureGame Battlefield1942RoadToRome = new(
        "Battlefield 1942: The Road to Rome", 2003, "Action, First-Person Shooter", "DICE", "Electronic Arts",
        "The Italian campaign, with new vehicles and armies.")
    {
        Type = GameType.Expansion,
        LanPlayers = 64,
    };

    /// <summary>Every game, expansions included (the collection views filter those out themselves).</summary>
    public static IReadOnlyList<FixtureGame> All { get; } =
    [
        AgeOfEmpires2, Battlefield1942, Battlefield1942RoadToRome, CounterStrike, Diablo2, Doom2, HalfLife, Halo,
        NaturalSelection2, Quake3, RedAlert2, Soldat, StarCraft, Battlefront2, TeamFortress, UnrealTournament2004,
        Warcraft3, Worms,
    ];

    public static IEnumerable<FixtureGame> Library => All.Where(g => g.InLibrary);

    public static Guid IdFor(string title) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes("fixture:" + title)));
}
#endif
