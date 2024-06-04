using LANCommander.Client.Data;
using LANCommander.Client.Data.Models;
using LANCommander.Client.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class LibraryService : BaseService
    {
        private readonly SDK.Client Client;
        private readonly DownloadService DownloadService;
        private readonly CollectionService CollectionService;
        private readonly GameService GameService;
        private readonly PlaySessionService PlaySessionService;
        private readonly RedistributableService RedistributableService;

        public Dictionary<Guid, Process> RunningProcesses = new Dictionary<Guid, Process>();

        public delegate void OnLibraryChangedHandler();
        public event OnLibraryChangedHandler OnLibraryChanged;

        public LibraryService(
            SDK.Client client,
            DownloadService downloadService,
            CollectionService collectionService,
            GameService gameService,
            PlaySessionService playSessionService,
            RedistributableService redistributableService) : base()
        {
            Client = client;
            DownloadService = downloadService;
            CollectionService = collectionService;
            GameService = gameService;
            PlaySessionService = playSessionService;
            RedistributableService = redistributableService;

            DownloadService.OnInstallComplete += DownloadService_OnInstallComplete;
        }

        private async Task DownloadService_OnInstallComplete()
        {
            OnLibraryChanged?.Invoke();
        }

        public async Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync()
        {
            var items = new List<LibraryItem>();

            var collections = await CollectionService.Get();

            items.AddRange(collections.Select(c => new LibraryItem(c)));

            var redistributables = await RedistributableService.Get();

            foreach (var redistributable in redistributables)
                items.Add(new LibraryItem(redistributable));

            return items;
        }

        public async Task<LibraryItem> GetLibraryItemAsync(LibraryItem libraryItem)
        {
            // Assume for now it's a game
            var game = await GameService.Get(libraryItem.Key);

            return new LibraryItem(game);
        }

        public async Task Install(LibraryItem libraryItem)
        {
            var game = libraryItem.DataItem as Game;

            await DownloadService.Add(game);
        }

        public async Task Uninstall(LibraryItem libraryItem)
        {
            var game = libraryItem.DataItem as Game;

            await Task.Run(() => Client.Games.Uninstall(game.InstallDirectory, game.Id));

            game.InstallDirectory = null;
            game.Installed = false;
            game.InstalledVersion = null;

            await GameService.Update(game);

            OnLibraryChanged?.Invoke();
        }

        public async Task Run(Game game, SDK.Models.Action action)
        {
            var process = new Process();

            Client.Actions.AddVariable("DisplayWidth", ((int)DeviceDisplay.Current.MainDisplayInfo.Width).ToString());
            Client.Actions.AddVariable("DisplayHeight", ((int)DeviceDisplay.Current.MainDisplayInfo.Height).ToString());
            Client.Actions.AddVariable("DisplayRefreshRate", ((int)DeviceDisplay.Current.MainDisplayInfo.RefreshRate).ToString());

            process.StartInfo.Arguments = Client.Actions.ExpandVariables(action.Arguments, game.InstallDirectory, skipSlashes: true);
            process.StartInfo.FileName = Client.Actions.ExpandVariables(action.Path, game.InstallDirectory);
            process.StartInfo.WorkingDirectory = Client.Actions.ExpandVariables(action.WorkingDirectory, game.InstallDirectory);
            process.StartInfo.UseShellExecute = true;

            if (String.IsNullOrWhiteSpace(action.WorkingDirectory))
                process.StartInfo.WorkingDirectory = game.InstallDirectory;

            var userId = Guid.NewGuid();

            await PlaySessionService.StartSession(game.Id, userId);

            process.Start();

            await process.WaitForExitAsync();

            await PlaySessionService.EndSession(game.Id, userId);
        }
    }
}
