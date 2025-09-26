using IGDB;
using IGDB.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Models;
using Company = LANCommander.Server.Data.Models.Company;
using GameType = LANCommander.SDK.Enums.GameType;

namespace LANCommander.Server.Services
{
    public class IGDBService(
        ILogger<IGDBService> logger,
        GameService gameService,
        EngineService engineService,
        CompanyService companyService,
        GenreService genreService,
        TagService tagService) : BaseService(logger), IDisposable
    {
        private const string DefaultFields = "*";
        private IGDBClient Client;

        public bool Authenticated = false;

        private string ClientId { get; set; }
        private string ClientSecret { get; set; }

        public override void Initialize()
        {
            Authenticate();
        }

        public void Authenticate()
        {
            ClientId = _settings.IGDBClientId;
            ClientSecret = _settings.IGDBClientSecret;

            try
            {
                if (String.IsNullOrWhiteSpace(ClientId) || String.IsNullOrWhiteSpace(ClientSecret))
                    throw new Exception("Invalid IGDB credentials");

                Client = new IGDBClient(ClientId, ClientSecret);
                Authenticated = true;
            }
            catch (Exception ex)
            {
                Authenticated = false;
            }
        }

        public async Task<Game> GetAsync(long id, params string[] additionalFields)
        {
            var fields = DefaultFields.Split(',').ToList();

            fields.AddRange(additionalFields);

            var games = await Client.QueryAsync<Game>(IGDBClient.Endpoints.Games, $"fields {String.Join(',', fields)}; where id = {id};");

            if (games == null)
                return null;

            return games.FirstOrDefault();
        }

        public async Task<IEnumerable<Game>> SearchAsync(string input, int limit = 10, int offset = 0, params string[] additionalFields)
        {
            var fields = DefaultFields.Split(',').ToList();

            fields.AddRange(additionalFields);

            var sb = new StringBuilder();

            sb.Append($"search \"{input}\";");
            sb.Append($"fields {String.Join(',', fields)};");
            sb.Append($"limit {limit};");
            sb.Append($"offset {offset};");

            var games = await Client.QueryAsync<Game>(IGDBClient.Endpoints.Games, sb.ToString());

            return games.AsEnumerable();
        }

        public async Task<Data.Models.Game> ImportGameAsync(GameLookupResult result, Data.Models.Game game)
        {
            game.IGDBId = result.IGDBMetadata.Id.GetValueOrDefault();
            game.Title = result.IGDBMetadata.Name;
            game.Description = result.IGDBMetadata.Summary;
            game.ReleasedOn = result.IGDBMetadata.FirstReleaseDate.GetValueOrDefault().UtcDateTime;
            game.MultiplayerModes = result.MultiplayerModes.ToList();

            if (result.IGDBMetadata.GameModes != null && result.IGDBMetadata.GameModes.Values != null)
                game.Singleplayer = result.IGDBMetadata.GameModes.Values.Any(gm => gm.Name == "Single player");
            
            if (game.Id == Guid.Empty)
                game = await gameService.AddAsync(game);
            else
                game = await gameService.UpdateAsync(game);
            
            if (result.IGDBMetadata.ParentGame != null && result.IGDBMetadata.ParentGame.Id.HasValue)
            {
                var baseGame = await gameService.FirstOrDefaultAsync(g => g.IGDBId == result.IGDBMetadata.ParentGame.Id);

                if (baseGame != null)
                    game.BaseGame = baseGame;
            }
            
            #region Engine
            if (result.IGDBMetadata.GameEngines != null && result.IGDBMetadata.GameEngines.Values != null)
            {
                var engineMetadata = result.IGDBMetadata.GameEngines.Values.FirstOrDefault();

                var engine = await engineService.AddMissingAsync(e => e.Name == engineMetadata.Name,
                    new Data.Models.Engine { Name = engineMetadata.Name });

                game.Engine = engine.Value;
            }
            #endregion

            #region Companies
            if (result.IGDBMetadata.InvolvedCompanies != null && result.IGDBMetadata.InvolvedCompanies.Values != null)
            {
                var developers = result.IGDBMetadata.InvolvedCompanies.Values.Where(c => c.Developer.GetValueOrDefault()).Select(c => c.Company.Value.Name);
                var publishers = result.IGDBMetadata.InvolvedCompanies.Values.Where(c => c.Publisher.GetValueOrDefault()).Select(c => c.Company.Value.Name);
                var companies = developers.Concat(publishers).Distinct().ToList();

                foreach (var company in companies)
                {
                    var companyEntity = await companyService.FirstOrDefaultAsync(c => c.Name == company);

                    if (companyEntity == null)
                    {
                        companyEntity = await companyService.AddAsync(new Company()
                        {
                            Name = company,
                        });
                    }

                    if (game.Developers == null)
                        game.Developers = new List<Company>();
                    
                    if (game.Publishers == null)
                        game.Publishers = new List<Company>();
                    
                    if (developers.Contains(company) && !game.Developers.Any(p => p.Id == companyEntity.Id))
                        game.Developers.Add(companyEntity);
                    
                    if (publishers.Contains(company) && !game.Publishers.Any(p => p.Id == companyEntity.Id))
                        game.Publishers.Add(companyEntity);
                }
            }
            #endregion

            #region Genres
            if (result.IGDBMetadata.Genres != null && result.IGDBMetadata.Genres.Values != null)
            {
                var genres = result.IGDBMetadata.Genres.Values.Select(g => g.Name);
                
                if (game.Genres == null)
                    game.Genres = new List<Data.Models.Genre>();

                foreach (var genre in genres)
                {
                    var genreEntity = await genreService.AddMissingAsync(g => g.Name == genre,
                        new Data.Models.Genre { Name = genre });
                    
                    if (!game.Genres.Any(g => g.Id == genreEntity.Value.Id))
                        game.Genres.Add(genreEntity.Value);
                }
            }
            #endregion

            #region Tags
            if (result.IGDBMetadata.Keywords != null && result.IGDBMetadata.Keywords.Values != null)
            {
                var tags = result.IGDBMetadata.Keywords.Values.Select(t => t.Name);
                
                if (game.Tags == null)
                    game.Tags = new List<Data.Models.Tag>();
                
                foreach (var tag in tags)
                {
                    var tagEntity = await tagService.AddMissingAsync(t => t.Name == tag, new Data.Models.Tag { Name = tag });

                    if (!game.Tags.Any(t => t.Id == tagEntity.Value.Id))
                        game.Tags.Add(tagEntity.Value);
                }
            }
            #endregion

            return await gameService.UpdateAsync(game);
        }

        public void Dispose()
        {
            
        }
    }
}
