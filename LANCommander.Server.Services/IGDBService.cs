using IGDB;
using IGDB.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Services.Factories;
using LANCommander.Server.Services.Models;
using Microsoft.EntityFrameworkCore;
using Company = LANCommander.Server.Data.Models.Company;

namespace LANCommander.Server.Services
{
    public class IGDBService : BaseService, IDisposable
    {
        private const string DefaultFields = "*";
        private IGDBClient Client;
        private readonly DatabaseContext DatabaseContext;
        private readonly GameService GameService;
        private readonly EngineService EngineService;
        private readonly CompanyService CompanyService;
        private readonly GenreService GenreService;
        private readonly TagService TagService;

        public bool Authenticated = false;

        private string ClientId { get; set; }
        private string ClientSecret { get; set; }

        public IGDBService(
            ILogger<IGDBService> logger,
            DatabaseContext databaseContext,
            GameService gameService,
            EngineService engineService,
            CompanyService companyService,
            GenreService genreService,
            TagService tagService) : base(logger)
        {
            DatabaseContext = databaseContext;
            GameService = gameService;
            EngineService = engineService;
            CompanyService = companyService;
            GenreService = genreService;
            TagService = tagService;
            
            Authenticate();
        }

        public void Authenticate()
        {
            ClientId = Settings.IGDBClientId;
            ClientSecret = Settings.IGDBClientSecret;

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

            int[] categories = new int[]
            {
                (int)Category.MainGame,
                (int)Category.Port,
                (int)Category.StandaloneExpansion,
                (int)Category.Expansion,
                (int)Category.Mod,
                (int)Category.Remake,
                (int)Category.Remaster
            };

            var sb = new StringBuilder();

            sb.Append($"search \"{input}\";");
            sb.Append($"fields {String.Join(',', fields)};");
            sb.Append($"limit {limit};");
            sb.Append($"offset {offset};");
            sb.Append($"where category = ({String.Join(',', categories)});");

            var games = await Client.QueryAsync<Game>(IGDBClient.Endpoints.Games, sb.ToString());

            return games.AsEnumerable();
        }

        public async Task<Data.Models.Game> ImportGameAsync(GameLookupResult result, Data.Models.Game game)
        {
            var categoryMap = new Dictionary<IGDB.Models.Category, GameType>()
            {
                { IGDB.Models.Category.MainGame, GameType.MainGame },
                { IGDB.Models.Category.Expansion, GameType.Expansion },
                { IGDB.Models.Category.StandaloneExpansion, GameType.StandaloneExpansion },
                { IGDB.Models.Category.Mod, GameType.Mod }
            };

            game.IGDBId = result.IGDBMetadata.Id.GetValueOrDefault();
            game.Title = result.IGDBMetadata.Name;
            game.Description = result.IGDBMetadata.Summary;
            game.ReleasedOn = result.IGDBMetadata.FirstReleaseDate.GetValueOrDefault().UtcDateTime;
            game.MultiplayerModes = result.MultiplayerModes.ToList();

            if (categoryMap.Keys.Contains(result.IGDBMetadata.Category.GetValueOrDefault()))
                game.Type = categoryMap[result.IGDBMetadata.Category.GetValueOrDefault()];

            if (result.IGDBMetadata.GameModes != null && result.IGDBMetadata.GameModes.Values != null)
                game.Singleplayer = result.IGDBMetadata.GameModes.Values.Any(gm => gm.Name == "Single player");
            
            if (game.Id == Guid.Empty)
                game = await GameService.AddAsync(game);
            else
                game = await GameService.UpdateAsync(game);
            
            if (result.IGDBMetadata.ParentGame != null && result.IGDBMetadata.ParentGame.Id.HasValue)
            {
                var baseGame = await GameService.FirstOrDefaultAsync(g => g.IGDBId == result.IGDBMetadata.ParentGame.Id);

                if (baseGame != null)
                    game.BaseGame = baseGame;
            }
            
            #region Engine
            if (result.IGDBMetadata.GameEngines != null && result.IGDBMetadata.GameEngines.Values != null)
            {
                var engineMetadata = result.IGDBMetadata.GameEngines.Values.FirstOrDefault();

                var engine = await EngineService.AddMissingAsync(e => e.Name == engineMetadata.Name,
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
                    var companyEntity = await DatabaseContext
                        .Companies
                        .FirstOrDefaultAsync(c => c.Name == company);

                    if (companyEntity == null)
                    {
                        var entityResult = await DatabaseContext
                            .Companies
                            .AddAsync(new Company()
                            {
                                Name = company,
                            });
                        
                        companyEntity = entityResult.Entity;
                        
                        await DatabaseContext.SaveChangesAsync();
                    }
                    
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

                foreach (var genre in genres)
                {
                    var genreEntity = await GenreService.AddMissingAsync(g => g.Name == genre,
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
                
                foreach (var tag in tags)
                {
                    var tagEntity = await TagService.AddMissingAsync(t => t.Name == tag, new Data.Models.Tag { Name = tag });

                    if (game.Tags.Any(t => t.Id == tagEntity.Value.Id))
                        game.Tags.Add(tagEntity.Value);
                }
            }
            #endregion

            return await GameService.UpdateAsync(game);
        }

        public void Dispose()
        {
            
        }
    }
}
