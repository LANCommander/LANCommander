using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class LibraryService : BaseService
    {
        private readonly SDK.Client Client;
        private readonly GameService GameService;
        private readonly RedistributableService RedistributableService;

        public LibraryService(SDK.Client client, GameService gameService, RedistributableService redistributableService) : base()
        {
            Client = client;
            GameService = gameService;
            RedistributableService = redistributableService;
        }

        public async Task ImportAsync()
        {
            await ImportGamesAsync();
            await ImportRedistributables();
        }

        public async Task ImportGamesAsync()
        {
            var localGames = await GameService.Get();
            var remoteGames = await Client.Games.GetAsync();

            foreach (var remoteGame in remoteGames)
            {
                var localGame = localGames.FirstOrDefault(g => g.Id == remoteGame.Id);

                if (localGame == null)
                    localGame = new Data.Models.Game();

                localGame.Title = remoteGame.Title;
                localGame.SortTitle = remoteGame.SortTitle;
                localGame.Description = remoteGame.Description;
                localGame.Notes = remoteGame.Notes;
                localGame.ReleasedOn = remoteGame.ReleasedOn;
                localGame.Type = (Data.Enums.GameType)(int)remoteGame.Type;
                localGame.BaseGameId = remoteGame.BaseGame?.Id;
                localGame.Singleplayer = remoteGame.Singleplayer;
                
                // Have to handle IEnumerables of other models

                if (localGame.Id == Guid.Empty)
                {
                    localGame.Id = remoteGame.Id;
                    localGame = await GameService.Add(localGame);
                }
                else
                    localGame = await GameService.Update(localGame);
            }

            foreach (var localGame in localGames)
            {
                var remoteGame = remoteGames.FirstOrDefault(g => g.Id == localGame.Id);

                if (remoteGame == null && !localGame.Installed)
                {
                    await GameService.Delete(localGame);
                }
            }
        }

        public async Task ImportRedistributables()
        {

        }
    }
}
