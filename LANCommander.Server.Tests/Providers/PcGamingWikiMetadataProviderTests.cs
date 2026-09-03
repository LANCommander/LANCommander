using System.Text.Json;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.Services.Providers.Metadata;
using Shouldly;

namespace LANCommander.Server.Tests.Providers;

/// <summary>
/// Covers the parts of the PCGamingWiki provider that turn their responses into a manifest.
/// <para>
/// The HTML fixture is the Half-Life article as PCGamingWiki's <c>action=parse</c> returned it,
/// trimmed to the three regions the scraper reads. It exists because the anonymous path depends on
/// the wiki's template markup, which we don't control — if a future migration renames those
/// classes, these fail rather than the provider silently returning empty games.
/// </para>
/// </summary>
public class PcGamingWikiMetadataProviderTests
{
    private const string FixtureFileName = "pcgamingwiki-half-life.html";

    private static Game ParseFixture()
    {
        var html = File.ReadAllText(Path.Combine("Files", FixtureFileName));

        return PcGamingWikiMetadataProvider.ParseGameFromHtml(html, "Half-Life");
    }

    #region Infobox scraping

    [Fact]
    public void ParseGameFromHtmlReadsTitleFromTheInfoboxCaption()
    {
        ParseFixture().Title.ShouldBe("Half-Life");
    }

    [Fact]
    public void ParseGameFromHtmlReadsDevelopersEngineAndTaxonomy()
    {
        var game = ParseFixture();

        game.Developers.Select(d => d.Name).ShouldBe(["Valve Corporation"]);
        game.Engine.ShouldNotBeNull().Name.ShouldBe("GoldSrc");
        game.Genres.Select(g => g.Name).ShouldBe(["Action", "FPS", "Shooter"]);
        game.Tags.Select(t => t.Name).ShouldBe(["Contemporary", "North America", "Sci-fi"]);
        game.Singleplayer.ShouldBeTrue();
    }

    [Fact]
    public void ParseGameFromHtmlTakesTheEarliestReleaseDateAcrossPlatforms()
    {
        // The infobox lists Windows in 1998 and macOS/Linux in 2013.
        ParseFixture().ReleasedOn.ShouldBe(new DateTime(1998, 11, 19));
    }

    #endregion

    #region Multiplayer

    [Fact]
    public void ParseGameFromHtmlReadsMultiplayerModesAndPlayerCounts()
    {
        var modes = ParseFixture().MultiplayerModes;

        // The local row has no players cell at all — the notes cell spans it. The mode still has
        // to survive, otherwise the game looks like it has no local multiplayer.
        modes.Select(m => m.Type).ShouldBe([MultiplayerType.Local, MultiplayerType.LAN, MultiplayerType.Online]);

        modes.Single(m => m.Type == MultiplayerType.LAN).MaxPlayers.ShouldBe(32);
        modes.Single(m => m.Type == MultiplayerType.Online).MaxPlayers.ShouldBe(32);
        modes.Single(m => m.Type == MultiplayerType.Local).MaxPlayers.ShouldBe(0);
    }

    [Fact]
    public void ParseGameFromHtmlReadsMultiplayerNotes()
    {
        var lan = ParseFixture().MultiplayerModes.Single(m => m.Type == MultiplayerType.LAN);

        lan.Description.ShouldNotBeNull().ShouldContain("Sven Co-op");
    }

    #endregion

    #region Save paths

    [Fact]
    public void ParseGameFromHtmlPrefersTheWindowsSaveLocation()
    {
        // The table also lists macOS and Linux rows, which we ignore.
        var savePaths = ParseFixture().SavePaths;

        var savePath = savePaths.ShouldHaveSingleItem();

        savePath.Type.ShouldBe(SavePathType.File);
        savePath.IsRegex.ShouldBeFalse();
        savePath.WorkingDirectory.ShouldBe(@"{InstallDir}\valve");
        savePath.Path.ShouldBe("SAVE");
    }

    #endregion

    #region BuildSavePath

    [Fact]
    public void BuildSavePathSubstitutesKnownTokens()
    {
        var result = PcGamingWikiMetadataProvider.BuildSavePath(@"<APPDATA>\Valve\saves\game.sav").ShouldNotBeNull();

        result.WorkingDirectory.ShouldBe(@"%APPDATA%\Valve\saves");
        result.Path.ShouldBe("game.sav");
        result.IsRegex.ShouldBeFalse();
    }

    [Fact]
    public void BuildSavePathTreatsATrailingSeparatorAsAFolder()
    {
        // Without trimming the separator the split leaves the whole location in
        // WorkingDirectory and an empty Path, which is unusable downstream.
        var result = PcGamingWikiMetadataProvider.BuildSavePath(@"<path-to-game>\valve\SAVE\").ShouldNotBeNull();

        result.WorkingDirectory.ShouldBe(@"{InstallDir}\valve");
        result.Path.ShouldBe("SAVE");
    }

    [Fact]
    public void BuildSavePathRejectsUnresolvedTokens()
    {
        // PCGamingWiki has tokens we have no equivalent for. Importing them verbatim would create
        // a save path that silently never matches.
        PcGamingWikiMetadataProvider.BuildSavePath(@"<Steam-folder>\userdata\<user-id>\saves").ShouldBeNull();
    }

    [Fact]
    public void BuildSavePathRejectsEmptyInput()
    {
        PcGamingWikiMetadataProvider.BuildSavePath("   ").ShouldBeNull();
    }

    [Fact]
    public void BuildSavePathConvertsWildcardsToRegexAndPreservesVariables()
    {
        var result = PcGamingWikiMetadataProvider.BuildSavePath(@"<path-to-game>\save#.dat").ShouldNotBeNull();

        result.IsRegex.ShouldBeTrue();
        result.WorkingDirectory.ShouldBe("{InstallDir}");
        result.Path.ShouldBe(@"save\d+\.dat");
    }

    [Fact]
    public void BuildSavePathOnlyConvertsWildcardsInTheFileName()
    {
        // Known limitation: SavePath has no regex support for WorkingDirectory, so a wildcard in a
        // directory segment stays literal and the path isn't marked as a regex.
        var result = PcGamingWikiMetadataProvider.BuildSavePath(@"<APPDATA>\slot#\save.dat").ShouldNotBeNull();

        result.IsRegex.ShouldBeFalse();
        result.Path.ShouldBe("save.dat");
        result.WorkingDirectory.ShouldBe(@"%APPDATA%\slot#");
    }

    #endregion

    #region Error handling

    [Fact]
    public void TryGetMediaWikiErrorDetectsAnErrorReturnedWithHttp200()
    {
        // MediaWiki reports failures in the body with a 200 status, so this is the only signal we
        // get that a Cargo query was refused.
        const string body = """
            {"error":{"code":"permissiondenied","info":"You don't have permission to run arbitrary Cargo queries."}}
            """;

        PcGamingWikiMetadataProvider.TryGetMediaWikiError(body, out var code, out var info).ShouldBeTrue();

        code.ShouldBe("permissiondenied");
        info.ShouldBe("You don't have permission to run arbitrary Cargo queries.");
    }

    [Fact]
    public void TryGetMediaWikiErrorIgnoresSuccessfulResponses()
    {
        PcGamingWikiMetadataProvider.TryGetMediaWikiError("""{"parse":{"title":"Half-Life"}}""", out _, out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void TryGetMediaWikiErrorIgnoresTheBareArrayOpensearchReturns()
    {
        PcGamingWikiMetadataProvider.TryGetMediaWikiError("""["Half-Life",[],[],[]]""", out _, out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void TryGetMediaWikiErrorIgnoresNonJsonBodies()
    {
        PcGamingWikiMetadataProvider.TryGetMediaWikiError("<html>Forbidden</html>", out _, out _).ShouldBeFalse();
    }

    #endregion

    #region Search results

    [Fact]
    public void SearchConverterReadsTheOpensearchArrayAndStripsTheWikiPrefix()
    {
        const string response = """
            ["Half-Life",
             ["Half-Life","Half-Life 2"],
             ["","A sequel"],
             ["https://www.pcgamingwiki.com/wiki/Half-Life","https://www.pcgamingwiki.com/wiki/Half-Life_2"]]
            """;

        var results = JsonSerializer.Deserialize<MetadataSearchResultsCollection<Game>>(response, new JsonSerializerOptions
        {
            Converters = { new PcGamingWikiMetadataProvider.PcgwGameSearchResultConverter() }
        }).ShouldNotBeNull();

        // The id is the page title, which is what action=parse takes.
        results.Results.Select(r => r.Id).ShouldBe(["Half-Life", "Half-Life_2"]);
        results.Results.Select(r => r.Data.Title).ShouldBe(["Half-Life", "Half-Life 2"]);

        // Blank descriptions become null rather than empty strings so the merge panel can tell
        // there's nothing to import.
        results.Results.First().Data.Description.ShouldBeNull();
        results.Results.Last().Data.Description.ShouldBe("A sequel");
    }

    #endregion

    #region Cargo helpers

    [Fact]
    public void GetValueMatchesFieldsWhetherCargoReturnsUnderscoresOrSpaces()
    {
        // Cargo hands back aliased column names with underscores replaced by spaces.
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Steam AppID"] = "70" };

        PcGamingWikiMetadataProvider.GetValue(row, "Steam_AppID").ShouldBe("70");
        PcGamingWikiMetadataProvider.GetValue(row, "Missing").ShouldBeNull();
    }

    [Fact]
    public void SplitListSeparatesCargoListColumns()
    {
        PcGamingWikiMetadataProvider.SplitList("Action, FPS ,Shooter").ShouldBe(["Action", "FPS", "Shooter"]);
        PcGamingWikiMetadataProvider.SplitList("Valve,valve").ShouldBe(["Valve"]);
        PcGamingWikiMetadataProvider.SplitList(null).ShouldBeEmpty();
    }

    [Fact]
    public void SplitPathsKeepsCommasThatBelongToAPath()
    {
        // Save locations legitimately contain commas, so unlike a list column these split only on
        // newlines.
        const string paths = "<path-to-game>\\Rockstar Games, Inc\\saves\n<APPDATA>\\other\n";

        PcGamingWikiMetadataProvider.SplitPaths(paths)
            .ShouldBe([@"<path-to-game>\Rockstar Games, Inc\saves", @"<APPDATA>\other"]);
    }

    #endregion
}
