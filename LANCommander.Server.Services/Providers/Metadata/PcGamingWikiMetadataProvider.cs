using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services.Providers.Metadata;

/// <summary>
/// Reads game metadata from PCGamingWiki's MediaWiki API.
/// <para>
/// There are two paths through here. Without credentials we use the anonymous
/// <c>opensearch</c>/<c>parse</c> endpoints and scrape the rendered page, which is how this
/// provider has always worked. With a bot password configured we query Cargo instead, which
/// returns the same information as structured data and doesn't break when the wiki's templates
/// change. Cargo is an upgrade rather than a requirement, so anything it can't answer falls back
/// to scraping instead of failing the lookup.
/// </para>
/// </summary>
public class PcGamingWikiMetadataProvider(
    IHttpClientFactory httpClientFactory,
    SettingsProvider<Settings.Settings> settingsProvider,
    PcGamingWikiSession session,
    IFusionCache cache,
    ILogger<PcGamingWikiMetadataProvider> logger) : IMetadataProvider
{
    public const string HttpClientName = "PCGamingWiki";

    public string ProviderName => "PCGamingWiki";

    /// <summary>
    /// PCGamingWiki explicitly asks API consumers to cache what they can. Metadata for a released
    /// game rarely changes, and a lookup is a deliberate user action rather than a hot path, so a
    /// long window costs us nothing and keeps us well clear of their rate limit.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    internal const string WikiUrlPrefix = "https://www.pcgamingwiki.com/wiki/";

    private HttpClient CreateClient() => httpClientFactory.CreateClient(HttpClientName);

    public async Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0)
    {
        // "redirects=resolve" collapses redirect pages onto their targets. Without it a search for
        // "Half-Life 1" returns a page that has no content of its own.
        var url = $"w/api.php?action=opensearch&format=json&formatversion=2&redirects=resolve&search={HttpUtility.UrlEncode(input)}&limit={limit}";

        var response = await GetStringAsync($"pcgw:search:{input}:{limit}", url);

        if (response is null)
            return null;

        return JsonSerializer.Deserialize<MetadataSearchResultsCollection<Game>>(response, new JsonSerializerOptions
        {
            Converters = { new PcgwGameSearchResultConverter() }
        });
    }

    public async Task<Game?> GetGameAsync(string gameId)
    {
        var game = await GetGameFromCargoAsync(gameId);

        if (game is not null)
            return game;

        return await GetGameFromHtmlAsync(gameId);
    }

    /// <summary>
    /// Resolves a page from an ID on another store, so a game that already carries a Steam AppID
    /// doesn't have to be matched on title. Backed by PCGamingWiki's <c>idlookup</c> action, which
    /// is still open to anonymous callers.
    /// </summary>
    public async Task<MetadataSearchResult<Game>?> ResolveByExternalIdAsync(string provider, string externalId)
    {
        var field = provider?.ToLowerInvariant() switch
        {
            "steam" => "steamappid",
            "gog" => "gogid",
            _ => null,
        };

        if (field is null || string.IsNullOrWhiteSpace(externalId))
            return null;

        var url = $"w/api.php?action=idlookup&format=json&formatversion=2&field={field}&value={HttpUtility.UrlEncode(externalId)}";

        var response = await GetStringAsync($"pcgw:idlookup:{field}:{externalId}", url);

        if (response is null)
            return null;

        try
        {
            using var document = JsonDocument.Parse(response);

            if (!document.RootElement.TryGetProperty("idlookup", out var lookup)
                || lookup.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var entry in lookup.EnumerateArray())
            {
                if (entry.TryGetProperty("title", out var title)
                    && title.TryGetProperty("Page", out var page)
                    && page.GetString() is { Length: > 0 } pageName)
                    return new MetadataSearchResult<Game>(pageName, new Game { Title = pageName });
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse the PCGamingWiki idlookup response for {Field} {Value}.", field, externalId);
        }

        return null;
    }

    #region Cargo

    /// <summary>
    /// Builds a game from Cargo. Returns null when no bot password is configured, when the query
    /// is rejected, or when the page simply isn't in the Game table — all of which mean the caller
    /// should fall back to scraping.
    /// </summary>
    private async Task<Game?> GetGameFromCargoAsync(string gameId)
    {
        var credentials = settingsProvider.CurrentValue.Server.PcGamingWiki;

        if (string.IsNullOrWhiteSpace(credentials.Username) || string.IsNullOrWhiteSpace(credentials.BotPassword))
            return null;

        var where = HttpUtility.UrlEncode($"Game._pageName=\"{EscapeForCargo(gameId)}\"");

        var coreRows = await CargoQueryAsync(
            $"pcgw:cargo:game:{gameId}",
            "tables=Game"
            + "&fields=Game._pageName=Page,Game.Developers=Developers,Game.Publishers=Publishers,"
            + "Game.Engines=Engines,Game.Released=Released,Game.Genres=Genres,Game.Themes=Themes,"
            + "Game.Modes=Modes,Game.Steam_AppID=SteamAppId,Game.GOGcom_ID=GogId"
            + $"&where={where}&limit=1");

        var core = coreRows?.FirstOrDefault();

        if (core is null)
            return null;

        var game = new Game
        {
            Title = GetValue(core, "Page") ?? gameId,
            Developers = SplitList(GetValue(core, "Developers")).Select(name => new Company { Name = name }).ToList(),
            Publishers = SplitList(GetValue(core, "Publishers")).Select(name => new Company { Name = name }).ToList(),
            Genres = SplitList(GetValue(core, "Genres")).Select(name => new Genre { Name = name }).ToList(),
            Tags = SplitList(GetValue(core, "Themes")).Select(name => new Tag { Name = name }).ToList(),
            Singleplayer = SplitList(GetValue(core, "Modes"))
                .Any(mode => mode.Equals("Singleplayer", StringComparison.OrdinalIgnoreCase)),
        };

        var engine = SplitList(GetValue(core, "Engines")).FirstOrDefault();

        if (engine is not null)
            game.Engine = new Engine { Name = engine };

        // Cargo date columns come back as "1998-11-19 00:00:00".
        if (DateTime.TryParse(GetValue(core, "Released"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var released))
            game.ReleasedOn = released;

        var externalIds = new List<GameExternalId>();

        foreach (var appId in SplitList(GetValue(core, "SteamAppId")))
            externalIds.Add(new GameExternalId { Provider = "Steam", ExternalId = appId });

        foreach (var gogId in SplitList(GetValue(core, "GogId")))
            externalIds.Add(new GameExternalId { Provider = "GOG", ExternalId = gogId });

        if (externalIds.Count > 0)
            game.ExternalIds = externalIds;

        game.MultiplayerModes = await GetMultiplayerModesFromCargoAsync(gameId, where);
        game.SavePaths = await GetSavePathsFromCargoAsync(gameId, where);

        // Cargo joins are inner joins, so a page with no Multiplayer or GameData row drops out
        // entirely rather than coming back with empty columns. The rendered page still carries
        // both tables, so fill the gaps from HTML rather than reporting the game as having neither.
        if (game.MultiplayerModes.Count == 0 || game.SavePaths.Count == 0)
        {
            var scraped = await GetGameFromHtmlAsync(gameId);

            if (scraped is not null)
            {
                if (game.MultiplayerModes.Count == 0)
                    game.MultiplayerModes = scraped.MultiplayerModes;

                if (game.SavePaths.Count == 0)
                    game.SavePaths = scraped.SavePaths;
            }
        }

        return game;
    }

    private async Task<ICollection<MultiplayerMode>> GetMultiplayerModesFromCargoAsync(string gameId, string encodedWhere)
    {
        var rows = await CargoQueryAsync(
            $"pcgw:cargo:multiplayer:{gameId}",
            "tables=Game,Multiplayer"
            + "&fields=Multiplayer.Local=Local,Multiplayer.Local_players=LocalPlayers,"
            + "Multiplayer.LAN=Lan,Multiplayer.LAN_players=LanPlayers,"
            + "Multiplayer.Online=Online,Multiplayer.Online_players=OnlinePlayers"
            + "&join_on=" + HttpUtility.UrlEncode("Game._pageID=Multiplayer._pageID")
            + $"&where={encodedWhere}&limit=1");

        var row = rows?.FirstOrDefault();

        if (row is null)
            return [];

        var modes = new List<MultiplayerMode>();

        AddMode(MultiplayerType.Local, GetValue(row, "Local"), GetValue(row, "LocalPlayers"));
        AddMode(MultiplayerType.LAN, GetValue(row, "Lan"), GetValue(row, "LanPlayers"));
        AddMode(MultiplayerType.Online, GetValue(row, "Online"), GetValue(row, "OnlinePlayers"));

        return modes;

        void AddMode(MultiplayerType type, string? support, string? players)
        {
            // The support column carries the wiki's rating for that mode; "unknown" and "false"
            // both mean there's nothing worth recording.
            if (string.IsNullOrWhiteSpace(support)
                || support.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                || support.Equals("false", StringComparison.OrdinalIgnoreCase)
                || support.Equals("n/a", StringComparison.OrdinalIgnoreCase))
                return;

            var mode = new MultiplayerMode { Type = type };

            if (int.TryParse(players, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxPlayers))
                mode.MaxPlayers = maxPlayers;

            modes.Add(mode);
        }
    }

    private async Task<ICollection<SavePath>> GetSavePathsFromCargoAsync(string gameId, string encodedWhere)
    {
        var rows = await CargoQueryAsync(
            $"pcgw:cargo:gamedata:{gameId}",
            "tables=Game,GameData"
            + "&fields=GameData.Type=Type,GameData.Platform=Platform,GameData.Paths=Paths"
            + "&join_on=" + HttpUtility.UrlEncode("Game._pageID=GameData._pageID")
            + $"&where={encodedWhere}&limit=50");

        if (rows is null || rows.Count == 0)
            return [];

        var windowsPaths = new List<SavePath>();
        var dosPaths = new List<SavePath>();

        foreach (var row in rows)
        {
            var type = GetValue(row, "Type");

            // The table also carries configuration file locations, which aren't save data.
            if (type is null || !type.Contains("save", StringComparison.OrdinalIgnoreCase))
                continue;

            var platform = GetValue(row, "Platform") ?? string.Empty;

            var target = platform.Equals("Windows", StringComparison.OrdinalIgnoreCase) ? windowsPaths
                : platform.Equals("DOS", StringComparison.OrdinalIgnoreCase) ? dosPaths
                : null;

            if (target is null)
                continue;

            foreach (var raw in SplitPaths(GetValue(row, "Paths")))
            {
                var result = BuildSavePath(raw);

                if (result is null)
                    continue;

                target.Add(new SavePath
                {
                    Type = SavePathType.File,
                    Path = result.Value.Path,
                    WorkingDirectory = result.Value.WorkingDirectory,
                    IsRegex = result.Value.IsRegex
                });
            }
        }

        // Prefer Windows; fall back to DOS if no Windows paths were found.
        return windowsPaths.Count > 0 ? windowsPaths : dosPaths;
    }

    /// <summary>
    /// Runs a Cargo query, logging in first and retrying once if the wiki tells us our session
    /// isn't good enough. Returns null on any failure so the caller can fall back to scraping.
    /// </summary>
    private async Task<List<Dictionary<string, string>>?> CargoQueryAsync(string cacheKey, string query)
    {
        var credentials = settingsProvider.CurrentValue.Server.PcGamingWiki;
        var url = $"w/api.php?action=cargoquery&format=json&formatversion=1&{query}";

        return await cache.GetOrSetAsync<List<Dictionary<string, string>>?>(
            cacheKey,
            async (ctx, ct) =>
            {
                var rows = await ExecuteCargoQueryAsync(url, credentials, allowRetry: true, ct);

                // Don't cache a failure; the next lookup should get a fresh attempt.
                if (rows is null)
                    ctx.Options.Duration = TimeSpan.Zero;

                return rows;
            },
            options => options.SetDuration(CacheDuration));
    }

    private async Task<List<Dictionary<string, string>>?> ExecuteCargoQueryAsync(
        string url,
        Settings.Models.PcGamingWikiSettings credentials,
        bool allowRetry,
        CancellationToken cancellationToken)
    {
        var client = CreateClient();

        if (!await session.EnsureAuthenticatedAsync(client, credentials.Username, credentials.BotPassword, logger, cancellationToken))
            return null;

        string body;

        try
        {
            body = await client.GetStringAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "PCGamingWiki Cargo request failed.");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (TryGetError(document.RootElement, out var code, out var info))
            {
                // Either the session expired or it was never good enough for Cargo. Drop it and
                // let a fresh login decide which.
                if (allowRetry && code is "permissiondenied" or "assertuserfailed" or "readapidenied")
                {
                    session.Invalidate();
                    return await ExecuteCargoQueryAsync(url, credentials, allowRetry: false, cancellationToken);
                }

                logger.LogWarning(
                    "PCGamingWiki rejected a Cargo query ({Code}): {Info}. Falling back to scraping the page.",
                    code, info);

                return null;
            }

            if (!document.RootElement.TryGetProperty("cargoquery", out var results)
                || results.ValueKind != JsonValueKind.Array)
                return null;

            var rows = new List<Dictionary<string, string>>();

            foreach (var result in results.EnumerateArray())
            {
                if (!result.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.Object)
                    continue;

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var field in title.EnumerateObject())
                {
                    var value = field.Value.ValueKind switch
                    {
                        JsonValueKind.String => field.Value.GetString(),
                        JsonValueKind.Null => null,
                        _ => field.Value.ToString(),
                    };

                    if (!string.IsNullOrWhiteSpace(value))
                        row[field.Name] = value;
                }

                if (row.Count > 0)
                    rows.Add(row);
            }

            return rows;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse a PCGamingWiki Cargo response.");
            return null;
        }
    }

    /// <summary>
    /// Cargo returns aliased field names with underscores replaced by spaces, so look the value up
    /// both ways rather than depending on which form comes back.
    /// </summary>
    internal static string? GetValue(Dictionary<string, string> row, string field)
    {
        if (row.TryGetValue(field, out var value))
            return value;

        return row.TryGetValue(field.Replace('_', ' '), out value) ? value : null;
    }

    /// <summary>Splits a Cargo list column into its values.</summary>
    internal static IEnumerable<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Splits a GameData Paths value. Paths are newline separated and routinely contain commas
    /// inside Windows path segments, so this deliberately doesn't split on the Cargo list
    /// delimiter the way <see cref="SplitList"/> does.
    /// </summary>
    internal static IEnumerable<string> SplitPaths(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => entry.Length > 0);
    }

    private static string EscapeForCargo(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    #endregion

    #region Anonymous endpoints

    private async Task<Game?> GetGameFromHtmlAsync(string gameId)
    {
        // "redirects=1" is what makes a redirect page resolve to the article it points at. Without
        // it the parse returns the stub and we hand back a game with nothing but a title.
        var url = $"w/api.php?action=parse&page={HttpUtility.UrlEncode(gameId)}&prop=text&format=json&redirects=1&disablelimitreport=1&disableeditsection=1";

        var response = await GetStringAsync($"pcgw:page:{gameId}", url);

        if (response is null)
            return null;

        using var doc = JsonDocument.Parse(response);

        if (!doc.RootElement.TryGetProperty("parse", out var parse))
            return null;

        var html = parse.GetProperty("text").GetProperty("*").GetString();

        if (html is null)
            return null;

        return ParseGameFromHtml(html, gameId);
    }

    /// <summary>
    /// Fetches and caches a response body, surfacing MediaWiki's errors.
    /// <para>
    /// MediaWiki reports failures as HTTP 200 with an error object in the body, so without this an
    /// expired session or a malformed request looks exactly like a game that has no metadata.
    /// </para>
    /// </summary>
    private async Task<string?> GetStringAsync(string cacheKey, string url)
    {
        return await cache.GetOrSetAsync<string?>(
            cacheKey,
            async (ctx, ct) =>
            {
                string body;

                try
                {
                    body = await CreateClient().GetStringAsync(url, ct);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "PCGamingWiki request failed for {Url}.", url);
                    ctx.Options.Duration = TimeSpan.Zero;
                    return null;
                }

                if (TryGetMediaWikiError(body, out var code, out var info))
                {
                    logger.LogWarning("PCGamingWiki returned an error for {Url} ({Code}): {Info}", url, code, info);
                    ctx.Options.Duration = TimeSpan.Zero;
                    return null;
                }

                return body;
            },
            options => options.SetDuration(CacheDuration));
    }

    internal static bool TryGetMediaWikiError(string body, out string? code, out string? info)
    {
        code = null;
        info = null;

        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            using var document = JsonDocument.Parse(body);

            // opensearch returns a bare array, which can never carry an error object.
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && TryGetError(document.RootElement, out code, out info);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetError(JsonElement root, out string? code, out string? info)
    {
        code = null;
        info = null;

        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
            return false;

        if (error.TryGetProperty("code", out var codeElement))
            code = codeElement.GetString();

        if (error.TryGetProperty("info", out var infoElement))
            info = infoElement.GetString();

        return true;
    }

    #endregion

    #region HTML scraping

    internal static Game ParseGameFromHtml(string html, string fallbackTitle)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        var game = new Game { Title = fallbackTitle };

        var infobox = htmlDoc.DocumentNode.SelectSingleNode("//table[@id='infobox-game']");
        if (infobox is null)
            return game;

        var caption = infobox.SelectSingleNode(".//caption");
        if (caption is not null)
            game.Title = HtmlEntity.DeEntitize(caption.InnerText.Trim());

        var developers = new List<Company>();
        var publishers = new List<Company>();
        var genres = new List<Genre>();
        var tags = new List<Tag>();
        DateTime? earliestRelease = null;
        string? currentSection = null;

        var rows = infobox.SelectNodes(".//tr");
        if (rows is null) return game;

        foreach (HtmlNode row in rows)
        {
            var headerCell = row.SelectSingleNode("th[@class='template-infobox-header']");
            if (headerCell is not null)
            {
                currentSection = headerCell.InnerText.Trim();
                continue;
            }

            var typeCell = row.SelectSingleNode("td[@class='template-infobox-type']");
            var infoCell = row.SelectSingleNode("td[@class='template-infobox-info']");
            if (typeCell is null || infoCell is null)
                continue;

            var type = typeCell.InnerText.Trim();

            switch (currentSection)
            {
                case "Developers" when string.IsNullOrEmpty(type):
                {
                    var name = GetCellText(infoCell);

                    if (!string.IsNullOrEmpty(name))
                        developers.Add(new Company { Name = name });

                    break;
                }
                case "Publishers" when string.IsNullOrEmpty(type):
                {
                    var name = GetCellText(infoCell);

                    if (!string.IsNullOrEmpty(name))
                        publishers.Add(new Company { Name = name });

                    break;
                }
                case "Engines" when string.IsNullOrEmpty(type):
                {
                    game.Engine ??= new Engine { Name = GetCellText(infoCell) };
                    break;
                }
                case "Release dates":
                {
                    var dateText = GetCellText(infoCell);

                    if (DateTime.TryParse(dateText, out var date))
                        if (earliestRelease is null || date < earliestRelease)
                            earliestRelease = date;

                    break;
                }
                case "Taxonomy":
                {
                    if (type == "Genres")
                        foreach (var name in GetAbbrTexts(infoCell))
                            genres.Add(new Genre { Name = name });
                    else if (type == "Modes")
                        game.Singleplayer = GetAbbrTexts(infoCell)
                            .Any(m => m.Equals("Singleplayer", StringComparison.OrdinalIgnoreCase));
                    else if (type == "Themes")
                        foreach (var name in GetAbbrTexts(infoCell))
                            tags.Add(new Tag { Name = name });

                    break;
                }
            }
        }

        if (developers.Count > 0)
            game.Developers = developers;

        if (publishers.Count > 0)
            game.Publishers = publishers;

        if (genres.Count > 0)
            game.Genres = genres;

        if (tags.Count > 0)
            game.Tags = tags;

        if (earliestRelease.HasValue)
            game.ReleasedOn = earliestRelease.Value;

        var multiplayerModes = ParseMultiplayerModes(htmlDoc);

        if (multiplayerModes.Count > 0)
            game.MultiplayerModes = multiplayerModes;

        var savePaths = ParseSavePaths(htmlDoc);

        if (savePaths.Count > 0)
            game.SavePaths = savePaths;

        return game;
    }

    // Maps PCGamingWiki path tokens (as decoded text) to LANCommander path variables.
    private static readonly Dictionary<string, string> PathVariableMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["<path-to-game>"] = "{InstallDir}",
        ["<APPDATA>"] = "%APPDATA%",
        ["<LOCALAPPDATA>"] = "%LOCALAPPDATA%",
        ["<USERPROFILE>"] = "%USERPROFILE%",
        ["<PUBLIC>"] = "%PUBLIC%",
        ["<WINDIR>"] = "%WINDIR%",
        ["<PROGRAMFILES>"] = "%PROGRAMFILES%",
        ["<PROGRAMFILES(X86)>"] = "%PROGRAMFILES(X86)%",
    };

    private static List<SavePath> ParseSavePaths(HtmlDocument htmlDoc)
    {
        var saveHeading = htmlDoc.DocumentNode.SelectSingleNode("//span[@id='Save_game_data_location']");

        if (saveHeading is null)
            return [];

        // Walk up to the enclosing <h3>
        var h3 = saveHeading.ParentNode;

        while (h3 is not null && h3.Name != "h3")
            h3 = h3.ParentNode;

        if (h3 is null)
            return [];

        // The save data table is inside the immediately following container div
        var table = h3.SelectSingleNode("following-sibling::div[1]//table");

        if (table is null)
            return [];

        var rows = table.SelectNodes(".//tr[contains(@class, 'table-gamedata-body-row')]");

        if (rows is null)
            return [];

        var windowsPaths = new List<SavePath>();
        var dosPaths = new List<SavePath>();

        foreach (var row in rows)
        {
            var systemCell = row.SelectSingleNode("th[@class='table-gamedata-body-system']");
            var locationCell = row.SelectSingleNode("td[@class='table-gamedata-body-location']");

            if (systemCell is null || locationCell is null)
                continue;

            var system = systemCell.InnerText.Trim();

            bool isWindows = system.Equals("Windows", StringComparison.OrdinalIgnoreCase);
            bool isDos = system.Equals("DOS", StringComparison.OrdinalIgnoreCase);

            if (!isWindows && !isDos)
                continue;

            var pathSpans = locationCell.SelectNodes(".//span[contains(@class, 'template-infotable-monospace')]");

            if (pathSpans is null)
                continue;

            var target = isWindows ? windowsPaths : dosPaths;

            foreach (var pathSpan in pathSpans)
            {
                var result = BuildSavePathFromNode(pathSpan);

                if (result is null)
                    continue;

                target.Add(new SavePath
                {
                    Type = SavePathType.File,
                    Path = result.Value.Path,
                    WorkingDirectory = result.Value.WorkingDirectory,
                    IsRegex = result.Value.IsRegex
                });
            }
        }

        // Prefer Windows; fall back to DOS if no Windows paths were found
        return windowsPaths.Count > 0 ? windowsPaths : dosPaths;
    }

    private static (string Path, string? WorkingDirectory, bool IsRegex)? BuildSavePathFromNode(HtmlNode pathSpan)
    {
        // Clone and strip footnote <sup> elements
        var clone = pathSpan.Clone();
        var sups = clone.SelectNodes(".//sup");

        if (sups is not null)
            foreach (var sup in sups.ToList())
                sup.Remove();

        return BuildSavePath(HtmlEntity.DeEntitize(clone.InnerText));
    }

    /// <summary>
    /// Turns a PCGamingWiki save location into a LANCommander save path. Shared by the scraped
    /// table and the Cargo GameData column, which use the same token syntax.
    /// </summary>
    internal static (string Path, string? WorkingDirectory, bool IsRegex)? BuildSavePath(string rawPath)
    {
        var path = rawPath.Trim();

        foreach (var (token, variable) in PathVariableMap)
            path = path.Replace(token, variable, StringComparison.OrdinalIgnoreCase);

        // A trailing separator means the location is a folder. Drop it before the split below,
        // which would otherwise leave the folder in WorkingDirectory and Path empty. Most of the
        // wiki's save locations are written this way, so this is the common case rather than an
        // edge case.
        path = path.TrimEnd('\\', '/');

        // Skip paths that still contain an unresolved <variable> token
        if (Regex.IsMatch(path, @"<[a-zA-Z]"))
            return null;

        if (string.IsNullOrWhiteSpace(path))
            return null;

        // No WorkingDirectory is supplied for these paths, so assume it from
        // everything up to the second-to-last node, leaving the final node as
        // the path itself.
        string? workingDirectory = null;
        var separatorIndex = path.LastIndexOfAny(['\\', '/']);

        if (separatorIndex > 0)
        {
            workingDirectory = path[..separatorIndex];
            path = path[(separatorIndex + 1)..];
        }

        if (!path.Contains('#'))
            return (path, workingDirectory, false);

        // Convert # wildcards to \d+ regex, escaping everything else.
        // Split on {Variable} tokens first so they are preserved verbatim.
        var parts = Regex.Split(path, @"(\{[^}]+\})");
        var regexPath = string.Concat(parts.Select(part =>
            part.StartsWith('{') && part.EndsWith('}')
                ? part
                // Escape around the wildcards rather than replacing them afterwards: Regex.Escape
                // turns "#" into "\#", so a later Replace("#", ...) would leave the backslash
                // behind and produce "\\d+", which matches a literal backslash.
                : string.Join(@"\d+", part.Split('#').Select(Regex.Escape))));

        return (regexPath, workingDirectory, true);
    }

    private static List<MultiplayerMode> ParseMultiplayerModes(HtmlDocument htmlDoc)
    {
        var modes = new List<MultiplayerMode>();

        var table = htmlDoc.GetElementbyId("table-network-multiplayer");
        if (table is null)
            return modes;

        var rows = table.SelectNodes(".//tr[contains(@class, 'table-network-multiplayer-body-row')]");
        if (rows is null)
            return modes;

        foreach (var row in rows)
        {
            var abbrNodes = row.SelectNodes(".//abbr");

            if (abbrNodes is null)
                continue;

            // The players cell is omitted entirely when the wiki has no count for that mode — the
            // notes cell just spans it instead. Requiring it would drop the mode along with it.
            var playerNodes = row.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-players')]");

            var typeText = abbrNodes[0].InnerText.Trim();
            var type = typeText switch
            {
                "Local play" => MultiplayerType.Local,
                "LAN play" => MultiplayerType.LAN,
                "Online play" => MultiplayerType.Online,
                _ => (MultiplayerType?)null
            };

            if (type is null)
                continue;

            var mode = new MultiplayerMode { Type = type.Value };

            if (playerNodes is not null && int.TryParse(playerNodes[0].InnerText.Trim(), out var maxPlayers))
                mode.MaxPlayers = maxPlayers;

            var noteNodes = row.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-notes')]");

            if (noteNodes is not null && noteNodes.Count == 1 && noteNodes[0].ChildNodes.Count > 0)
            {
                var notes = string.Join("\n", noteNodes[0].ChildNodes.Select(n => n.InnerText.TrimEnd('.')));
                notes = notes.Replace("\n\n", ". ").ReplaceLineEndings("").Trim();
                notes = HttpUtility.HtmlDecode(notes);
                if (!string.IsNullOrEmpty(notes))
                    mode.Description = notes;
            }

            modes.Add(mode);
        }

        return modes;
    }

    // Strip footnote <sup> elements then return decoded plain text.
    private static string GetCellText(HtmlNode cell)
    {
        var clone = cell.Clone();
        var sups = clone.SelectNodes(".//sup");
        if (sups is not null)
            foreach (var sup in sups.ToList())
                sup.Remove();
        return HtmlEntity.DeEntitize(clone.InnerText.Trim());
    }

    private static IEnumerable<string> GetAbbrTexts(HtmlNode cell)
    {
        var abbrs = cell.SelectNodes(".//abbr");

        if (abbrs is null)
            return Enumerable.Empty<string>();

        return abbrs
            .Select(a => HtmlEntity.DeEntitize(a.InnerText.Trim()))
            .Where(s => !string.IsNullOrEmpty(s));
    }

    #endregion

    internal sealed class PcgwGameSearchResultConverter : JsonConverter<MetadataSearchResultsCollection<Game>>
    {
        public override MetadataSearchResultsCollection<Game> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected root array.");

            reader.Read();
            string searchTerm = reader.GetString()
                                ?? throw new JsonException("Missing search term.");

            reader.Read();
            var titles = JsonSerializer.Deserialize<string[]>(ref reader, options)
                         ?? throw new JsonException("Missing titles.");

            reader.Read();
            var descriptions = JsonSerializer.Deserialize<string[]>(ref reader, options)
                               ?? throw new JsonException("Missing descriptions.");

            reader.Read();
            var urls = JsonSerializer.Deserialize<string[]>(ref reader, options)
                       ?? throw new JsonException("Missing urls.");

            if (titles.Length != urls.Length)
                throw new JsonException("Titles and URLs length mismatch.");

            var entries = new List<GameEntry>(titles.Length);

            for (int i = 0; i < titles.Length; i++)
            {
                entries.Add(new GameEntry
                {
                    Title = titles[i],
                    Description = string.IsNullOrWhiteSpace(descriptions[i])
                        ? null
                        : descriptions[i],
                    Url = urls[i].Replace(WikiUrlPrefix, "")
                });
            }

            // Move past EndArray
            reader.Read();

            return new MetadataSearchResultsCollection<Game>(entries.Select(e => new MetadataSearchResult<Game>(e.Url,
                new Game
                {
                    Title = e.Title,
                    Description = e.Description,
                })).ToList(), false);
        }

        public override void Write(Utf8JsonWriter writer, MetadataSearchResultsCollection<Game> value, JsonSerializerOptions options)
            => throw new NotImplementedException();

        private sealed class GameEntry
        {
            public required string Url { get; init; }
            public required string Title { get; init; }
            public string? Description { get; init; }
        }
    }
}
