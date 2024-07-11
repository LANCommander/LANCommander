using LANCommander.Client.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class MessageBusService
    {
        public delegate Task OnMediaChangedHandler(Media media);
        public event OnMediaChangedHandler OnMediaChanged;

        public void MediaChanged(Media media)
        {
            OnMediaChanged?.Invoke(media);
        }

        public delegate Task OnGameStartedHander(Game game);
        public event OnGameStartedHander OnGameStarted;

        public void GameStarted(Game game)
        {
            OnGameStarted?.Invoke(game);
        }

        public delegate Task OnGameStoppedHander(Game game);
        public event OnGameStoppedHander OnGameStopped;

        public void GameStopped(Game game)
        {
            OnGameStopped?.Invoke(game);
        }
    }
}
