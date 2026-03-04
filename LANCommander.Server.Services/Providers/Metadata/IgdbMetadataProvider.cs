using System.Text;
using IGDB;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.Providers.Metadata;

public class IgdbMetadataProvider(
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<IgdbMetadataProvider> logger) : IMetadataProvider
{
    private IGDBClient? _client;
    private const string DefaultFields = "*";

    private void Authenticate()
    {
        if (_client is not null)
            return;
        
        var clientId = settingsProvider.CurrentValue.Server.IGDB.ClientId;
        var clientSecret =  settingsProvider.CurrentValue.Server.IGDB.ClientSecret;
        
        // Client will throw exceptions if no credentials are configured
        _client = new IGDBClient(clientId, clientSecret);
    }

    public string ProviderName => "IGDB";
    
    public async Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0)
    {
        Authenticate();
        
        var fields = DefaultFields.Split(',').ToList();

        var sb = new StringBuilder();

        sb.Append($"search \"{input}\";");
        sb.Append($"fields {String.Join(',', fields)};");
        sb.Append($"limit {limit};");
        sb.Append($"offset {offset};");
        
        var results = await _client!.QueryAsync<IGDB.Models.Game>(IGDBClient.Endpoints.Games, sb.ToString());

        return new MetadataSearchResultsCollection<Game>(results
                .Where(g => g is not null && g.Id is not null)
                .Select(g => new MetadataSearchResult<Game>(g?.Id?.ToString() ?? string.Empty, ConvertGame(g))).ToList(),
            results.Length == limit);
    }

    public async Task<Game?> GetGameAsync(string gameId)
    {
        var fields = DefaultFields.Split(',').ToList();
        
        var games = await _client.QueryAsync<IGDB.Models.Game>(IGDBClient.Endpoints.Games, $"fields {String.Join(',', fields)}; where id = {gameId};");

        if (games is null)
            return null;
        
        return ConvertGame(games.FirstOrDefault());
    }

    private Game ConvertGame(IGDB.Models.Game? igdbGame)
    {
        var game = new Game
        {
            IGDBId = (long?)igdbGame.Id,
            Title = igdbGame.Name,
            Description = igdbGame.Summary,
            ReleasedOn = igdbGame.FirstReleaseDate.GetValueOrDefault().UtcDateTime,
        };
        
        if (igdbGame.GameModes?.Values?.Any() ?? false)
            game.Singleplayer = igdbGame.GameModes.Values.Any(gm => gm.Name == "Single player");

        if (igdbGame.InvolvedCompanies?.Values?.Any() ?? false)
        {
            game.Developers = igdbGame.InvolvedCompanies.Values
                .Where(c => c.Developer.GetValueOrDefault())
                .Select(c => new Company { Name = c.Company?.Value?.Name })
                .ToList();
                
            game.Publishers = igdbGame.InvolvedCompanies.Values
                .Where(c => c.Publisher.GetValueOrDefault())
                .Select(c => new Company { Name = c.Company?.Value?.Name })
                .ToList();
        }

        if (igdbGame.GameEngines?.Values?.Any() ?? false)
        {
            game.Engine = new Engine
            {
                Name = igdbGame.GameEngines.Values.FirstOrDefault()?.Name ?? string.Empty
            };
        }

        if (igdbGame.Genres?.Values?.Any() ?? false)
        {
            game.Genres = igdbGame.Genres.Values
                .Select(g => new Genre { Name = g.Name })
                .ToList();
        }
        
        if (igdbGame.Keywords?.Values?.Any() ?? false)
        {
            game.Tags = igdbGame.Keywords.Values
                .Select(k => new Tag { Name = k.Name })
                .ToList();
        }

        return game;
    }
}