using LANCommander.Client.Data.Models;
using LANCommander.Client.Enums;
using LANCommander.Client.Models;
using LANCommander.SDK.Exceptions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class DownloadService : BaseService
    {
        private readonly SDK.Client Client;
        private readonly GameService GameService;

        private Stopwatch Stopwatch { get; set; }

        public ObservableCollection<IDownloadQueueItem> Queue { get; set; }

        public delegate void OnQueueChangedHandler();
        public event OnQueueChangedHandler OnQueueChanged;

        public delegate void OnInstallCompleteHandler();
        public event OnInstallCompleteHandler OnInstallComplete;

        public DownloadService(SDK.Client client, GameService gameService) : base()
        {
            Client = client;
            GameService = gameService;
            Stopwatch = new Stopwatch();

            Queue = new ObservableCollection<IDownloadQueueItem>();

            Queue.CollectionChanged += (sender, e) =>
            {
                OnQueueChanged?.Invoke();
            };
        }

        public async Task Add(Game game)
        {
            var gameInfo = await Client.Games.GetAsync(game.Id);

            // Check to see if we need to install the base game (this game is probably a mod or expansion)
            if (gameInfo.BaseGame != null)
            {
                var baseGame = await GameService.Get(gameInfo.BaseGame.Id);

                if (baseGame != null && !baseGame.Installed)
                {
                    // Install game
                    // Where does this get executed? In Playnite it's triggering the InstallController
                    // Probably because it does a bunch of UI things
                }
            }

            if (!Queue.Any(i => i.Id == game.Id || (i.Status != DownloadStatus.Canceled || i.Status != DownloadStatus.Failed)))
            {
                var queueItem = new DownloadQueueGame(gameInfo);

                if (Queue.Any(i => i.State))
                    Queue.Add(queueItem);
                else
                {
                    queueItem.Status = DownloadStatus.Downloading;

                    Queue.Add(queueItem);

                    Install();
                }

                OnQueueChanged?.Invoke();

                game.Title = gameInfo.Title;
                game.Installed = false;

                await GameService.Update(game);
            }
        }

        public async Task CancelInstall()
        {

        }

        public async Task Install()
        {
            var currentItem = Queue.FirstOrDefault(i => i.State);

            if (currentItem == null)
                return;

            var gameInfo = await Client.Games.GetAsync(currentItem.Id);

            if (gameInfo == null)
                return;

            currentItem.Status = DownloadStatus.Downloading;

            OnQueueChanged?.Invoke();

            string installDirectory;

            try
            {
                installDirectory = await Task.Run(() => Client.Games.Install(gameInfo.Id));
            }
            catch (InstallCanceledException ex)
            {
                // OnInstallCancelled?.Invoke(currentItem);

                Queue.Remove(currentItem);

                return;
            }
            catch (InstallException ex)
            {
                Queue.Remove(currentItem);
                return;
            }
            catch (Exception ex)
            {
                Queue.Remove(currentItem);
                return;
            }

            currentItem.Progress = 1;
            currentItem.BytesDownloaded = currentItem.TotalBytes;
            currentItem.CompletedOn = DateTime.Now;
            currentItem.Status = DownloadStatus.Complete;

            OnQueueChanged?.Invoke();

            if (currentItem is DownloadQueueGame)
            {
                var game = await GameService.Get(currentItem.Id);

                game.Installed = true;
                game.InstalledVersion = currentItem.Version;
                game.InstallDirectory = installDirectory;

                await GameService.Update(game);

                OnInstallComplete?.Invoke();
            }
        }
    }
}
