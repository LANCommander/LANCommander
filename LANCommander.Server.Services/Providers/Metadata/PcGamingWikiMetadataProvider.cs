using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.Services.Providers.Metadata;

public class PcGamingWikiMetadataProvider : IMetadataProvider
{
    public string ProviderName => "PCGamingWiki";

    private HttpClient _client;

    public PcGamingWikiMetadataProvider()
    {
        _client = new HttpClient();
        _client.BaseAddress = new Uri("https://pcgamingwiki.com/");
        _client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Name}/{Assembly.GetExecutingAssembly().GetName().Version?.ToString()}");
    }

    public async Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0)
    {
        var response = await _client.GetStringAsync($"w/api.php?action=opensearch&format=json&formatversion=2&search={HttpUtility.UrlEncode(input)}&limit={limit}");

        var results = JsonSerializer.Deserialize<MetadataSearchResultsCollection<Game>>(response, new JsonSerializerOptions
        {
            Converters = { new PcgwGameSearchResultConverter() }
        });

        return results;
    }

    public async Task<Game?> GetGameAsync(string gameId)
    {
        var response = await _client.GetStringAsync(
            $"w/api.php?action=parse&page={HttpUtility.UrlEncode(gameId)}&prop=text&format=json&disablelimitreport=1&disableeditsection=1");

        using var doc = JsonDocument.Parse(response);
        if (!doc.RootElement.TryGetProperty("parse", out var parse))
            return null;

        var html = parse.GetProperty("text").GetProperty("*").GetString();
        if (html is null)
            return null;

        return ParseGameFromHtml(html, gameId);
    }

    private static Game ParseGameFromHtml(string html, string fallbackTitle)
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
        ["<path-to-game>"]      = "{InstallDir}",
        ["<APPDATA>"]           = "%APPDATA%",
        ["<LOCALAPPDATA>"]      = "%LOCALAPPDATA%",
        ["<USERPROFILE>"]       = "%USERPROFILE%",
        ["<PUBLIC>"]            = "%PUBLIC%",
        ["<WINDIR>"]            = "%WINDIR%",
        ["<PROGRAMFILES>"]      = "%PROGRAMFILES%",
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
                var result = BuildSavePath(pathSpan);
                
                if (result is null)
                    continue;

                target.Add(new SavePath
                {
                    Type = SavePathType.File,
                    Path = result.Value.Path,
                    IsRegex = result.Value.IsRegex
                });
            }
        }

        // Prefer Windows; fall back to DOS if no Windows paths were found
        return windowsPaths.Count > 0 ? windowsPaths : dosPaths;
    }

    private static (string Path, bool IsRegex)? BuildSavePath(HtmlNode pathSpan)
    {
        // Clone and strip footnote <sup> elements
        var clone = pathSpan.Clone();
        var sups = clone.SelectNodes(".//sup");
        
        if (sups is not null)
            foreach (var sup in sups.ToList())
                sup.Remove();

        var path = HtmlEntity.DeEntitize(clone.InnerText.Trim());

        foreach (var (token, variable) in PathVariableMap)
            path = path.Replace(token, variable, StringComparison.OrdinalIgnoreCase);

        // Skip paths that still contain an unresolved <variable> token
        if (Regex.IsMatch(path, @"<[a-zA-Z]"))
            return null;

        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!path.Contains('#'))
            return (path, false);

        // Convert # wildcards to \d+ regex, escaping everything else.
        // Split on {Variable} tokens first so they are preserved verbatim.
        var parts = Regex.Split(path, @"(\{[^}]+\})");
        var regexPath = string.Concat(parts.Select(part =>
            part.StartsWith('{') && part.EndsWith('}')
                ? part
                : Regex.Escape(part).Replace("#", @"\d+")));

        return (regexPath, true);
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
            var playerNodes = row.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-players')]");

            if (abbrNodes is null || playerNodes is null)
                continue;

            var typeText = abbrNodes[0].InnerText.Trim();
            var type = typeText switch
            {
                "Local play"  => MultiplayerType.Local,
                "LAN play"    => MultiplayerType.LAN,
                "Online play" => MultiplayerType.Online,
                _             => (MultiplayerType?)null
            };

            if (type is null)
                continue;

            var mode = new MultiplayerMode { Type = type.Value };

            if (int.TryParse(playerNodes[0].InnerText.Trim(), out var maxPlayers))
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

    private sealed class PcgwGameSearchResultConverter : JsonConverter<MetadataSearchResultsCollection<Game>>
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
                    Url = urls[i].Replace("https://www.pcgamingwiki.com/wiki/", "")
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
