using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
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

namespace LANCommander.Launcher.Services
{
    public class LibraryService : BaseService
    {
        private readonly SDK.Client Client;
        private readonly DownloadService DownloadService;
        private readonly GameService GameService;
        private readonly SaveService SaveService;
        private readonly PlaySessionService PlaySessionService;
        private readonly RedistributableService RedistributableService;
        private readonly ImportService ImportService;
        private readonly MessageBusService MessageBusService;

        public Dictionary<Guid, Process> RunningProcesses = new Dictionary<Guid, Process>();

        public ObservableCollection<LibraryItem> LibraryItems { get; set; } = new ObservableCollection<LibraryItem>();

        public delegate Task OnLibraryChangedHandler(IEnumerable<LibraryItem> items);
        public event OnLibraryChangedHandler OnLibraryChanged;

        public delegate Task OnPreLibraryItemsFilteredHandler(IEnumerable<LibraryItem> items);
        public event OnPreLibraryItemsFilteredHandler OnPreLibraryItemsFiltered;

        public delegate Task OnLibraryItemsFilteredHandler(IEnumerable<LibraryItem> items);
        public event OnLibraryItemsFilteredHandler OnLibraryItemsFiltered;

        public Func<LibraryItem, string[]> GroupSelector { get; set; } = _ => new string[] { };

        public LibraryService(
            SDK.Client client,
            DownloadService downloadService,
            GameService gameService,
            SaveService saveService,
            PlaySessionService playSessionService,
            RedistributableService redistributableService,
            ImportService importService,
            MessageBusService messageBusService) : base()
        {
            Client = client;
            DownloadService = downloadService;
            GameService = gameService;
            SaveService = saveService;
            PlaySessionService = playSessionService;
            RedistributableService = redistributableService;
            ImportService = importService;
            MessageBusService = messageBusService;

            DownloadService.OnInstallComplete += DownloadService_OnInstallComplete;
            ImportService.OnImportComplete += ImportService_OnImportComplete;
        }

        private async Task ImportService_OnImportComplete()
        {
            await RefreshLibraryItemsAsync();

            LibraryChanged();
        }

        private async Task DownloadService_OnInstallComplete(Game game)
        {
            if (OnLibraryChanged != null)
                await OnLibraryChanged.Invoke(LibraryItems);
        }

        public async Task<IEnumerable<LibraryItem>> RefreshLibraryItemsAsync()
        {
            LibraryItems = new ObservableCollection<LibraryItem>(await GetLibraryItemsAsync());

            return LibraryItems;
        }

        public IEnumerable<LibraryItem> GetLibraryItems<T>()
        {
            return LibraryItems.Where(i => i.DataItem is T).DistinctBy(i => i.Key);
        }

        public async Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync()
        {
            var settings = SettingService.GetSettings();
            var items = new List<LibraryItem>();

            var games = await GameService.Get();

            switch (settings.Filter.GroupBy)
            {
                case Models.Enums.GroupBy.None:
                    GroupSelector = (g) => new string[] { };
                    break;
                case Models.Enums.GroupBy.Collection:
                    GroupSelector = (g) => (g.DataItem as Game).Collections.Select(c => c.Name).ToArray();
                    break;
                case Models.Enums.GroupBy.Genre:
                    GroupSelector = (g) => (g.DataItem as Game).Genres.Select(ge => ge.Name).ToArray();
                    break;
                case Models.Enums.GroupBy.Platform:
                    GroupSelector = (g) => (g.DataItem as Game).Platforms.Select(p => p.Name).ToArray();
                    break;
            }

            items.AddRange(games.Select(g => new LibraryItem(g, GroupSelector)).OrderByTitle(g => !String.IsNullOrWhiteSpace(g.SortName) ? g.SortName : g.Name));

            return await FilterLibraryItems(items);
        }

        async Task<IEnumerable<LibraryItem>> FilterLibraryItems(IEnumerable<LibraryItem> items)
        {
            var settings = SettingService.GetSettings();

            if (OnPreLibraryItemsFiltered != null)
                await OnPreLibraryItemsFiltered.Invoke(items);

            if (!String.IsNullOrWhiteSpace(settings.Filter.Title))
                items = items.Where(i => i.Name?.IndexOf(settings.Filter.Title, StringComparison.OrdinalIgnoreCase) >= 0 || i.SortName?.IndexOf(settings.Filter.Title, StringComparison.OrdinalIgnoreCase) >= 0);

            if (settings.Filter.Engines != null && settings.Filter.Engines.Any())
                items = items.Where(i => settings.Filter.Engines.Any(e => e == (i.DataItem as Game)?.Engine?.Name));

            if (settings.Filter.Genres != null && settings.Filter.Genres.Any())
                items = items.Where(i => settings.Filter.Genres.Any(fg => (i.DataItem as Game).Genres.Any(g => fg == g.Name)));

            if (settings.Filter.Tags != null && settings.Filter.Tags.Any())
                items = items.Where(i => settings.Filter.Tags.Any(ft => (i.DataItem as Game).Tags.Any(t => ft == t.Name)));

            if (settings.Filter.Developers != null && settings.Filter.Developers.Any())
                items = items.Where(i => settings.Filter.Developers.Any(fc => (i.DataItem as Game).Developers.Any(c => fc == c.Name)));

            if (settings.Filter.Publishers != null && settings.Filter.Publishers.Any())
                items = items.Where(i => settings.Filter.Publishers.Any(fc => (i.DataItem as Game).Publishers.Any(c => fc == c.Name)));

            if (settings.Filter.MinPlayers != null)
                items = items.Where(i => (i.DataItem as Game).MultiplayerModes.Any(mm => mm.MinPlayers <= settings.Filter.MinPlayers && mm.MaxPlayers >= settings.Filter.MinPlayers));

            if (settings.Filter.MaxPlayers != null)
                items = items.Where(i => (i.DataItem as Game).MultiplayerModes.Any(mm => mm.MaxPlayers <= settings.Filter.MaxPlayers));

            if (settings.Filter.Installed)
                items = items.Where(i => (i.DataItem as Game).Installed);

            if (OnLibraryItemsFiltered != null)
                await OnLibraryItemsFiltered.Invoke(items);

            return items;
        }

        public LibraryItem GetLibraryItem(Guid key)
        {
            var item = LibraryItems.FirstOrDefault(i => i.Key == key);

            if (item == null)
                item = LibraryItems.FirstOrDefault(i => i.Key == key);

            return item;
        }

        public async Task<LibraryItem> GetLibraryItemAsync(LibraryItem libraryItem)
        {
            return await GetLibraryItemAsync(libraryItem.Key);
        }

        public async Task<LibraryItem> GetLibraryItemAsync(Guid key)
        {
            var game = await GameService.Get(key);

            return new LibraryItem(game, GroupSelector);
        }

        public async Task Install(LibraryItem libraryItem)
        {
            var game = libraryItem.DataItem as Game;

            await DownloadService.Add(game);
        }

        public async Task LibraryChanged()
        {
            if (OnLibraryChanged != null)
                await OnLibraryChanged.Invoke(LibraryItems);
        }

        public async Task FilterChanged()
        {
            LibraryItems = new ObservableCollection<LibraryItem>(await GetLibraryItemsAsync());
        }

        public async Task Stop(LibraryItem libraryItem)
        {
            await Task.Run(() =>
            {
                if (RunningProcesses.ContainsKey(libraryItem.Key))
                {
                    var process = RunningProcesses[libraryItem.Key];

                    process.CloseMainWindow();

                    RunningProcesses.Remove(libraryItem.Key);
                }
            });
        }

        public bool IsRunning(LibraryItem libraryItem)
        {
            if (libraryItem != null)
                return IsRunning(libraryItem.Key);
            return false;
        }

        public bool IsRunning(Guid id)
        {
            if (!RunningProcesses.ContainsKey(id))
                return false;

            var process = RunningProcesses[id];

            try
            {
                if (process.HasExited)
                {
                    RunningProcesses.Remove(id);
                    return false;
                }
                else
                    return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task Run(Game game, SDK.Models.Action action)
        {
            var process = new Process();
            var settings = SettingService.GetSettings();

            var screen = DisplayHelper.GetScreen();

            Client.Actions.AddVariable("DisplayWidth", screen.Width.ToString());
            Client.Actions.AddVariable("DisplayHeight", screen.Height.ToString());
            Client.Actions.AddVariable("DisplayRefreshRate", screen.RefreshRate.ToString());
            Client.Actions.AddVariable("DisplayBitDepth", screen.BitsPerPixel.ToString());

            process.StartInfo.Arguments = Client.Actions.ExpandVariables(action.Arguments, game.InstallDirectory, skipSlashes: true);
            process.StartInfo.FileName = Client.Actions.ExpandVariables(action.Path, game.InstallDirectory);
            process.StartInfo.WorkingDirectory = Client.Actions.ExpandVariables(action.WorkingDirectory, game.InstallDirectory);
            process.StartInfo.UseShellExecute = true;

            if (String.IsNullOrWhiteSpace(action.WorkingDirectory))
                process.StartInfo.WorkingDirectory = game.InstallDirectory;

            var userId = Guid.NewGuid();

            #region Run Scripts
            var manifests = GameService.GetGameManifests(game);

            foreach (var manifest in manifests)
            {
                var currentGamePlayerAlias = SDK.GameService.GetPlayerAlias(game.InstallDirectory, manifest.Id);
                var currentGameKey = SDK.GameService.GetCurrentKey(game.InstallDirectory, manifest.Id);

                #region Check Game's Player Name
                if (currentGamePlayerAlias != settings.Profile.Alias)
                    await Client.Scripts.RunNameChangeScriptAsync(game.InstallDirectory, game.Id, settings.Profile.Alias);
                #endregion

                #region Check Key Allocation
                if (!settings.Authentication.OfflineMode && Client.IsConnected())
                {
                    var newKey = Client.Games.GetAllocatedKey(game.Id);

                    if (currentGameKey != newKey)
                        await Client.Scripts.RunKeyChangeScriptAsync(game.InstallDirectory, game.Id, newKey);
                }
                #endregion

                #region Download Latest Saves
                if (!settings.Authentication.OfflineMode && Client.IsConnected())
                {
                    try
                    {
                        var latestSave = await Client.Saves.GetLatestAsync(manifest.Id);
                        var latestSession = await PlaySessionService.GetLatestSession(manifest.Id, userId);

                        if (latestSave != null && latestSession != null && latestSave.CreatedOn > latestSession.End && latestSave.CreatedOn > game.InstalledOn)
                        {
                            await SaveService.DownloadLatestAsync(game.InstallDirectory, manifest.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error(ex, "Could not download save");
                    }
                }
                #endregion

                #region Run Before Start Script
                await Client.Scripts.RunBeforeStartScriptAsync(game.InstallDirectory, game.Id);
                #endregion
            }
            #endregion

            try
            {
                process.Start();

                await PlaySessionService.StartSession(game.Id, userId);

                RunningProcesses.Add(game.Id, process);

                MessageBusService.GameStarted(game);

                await process.WaitForExitAsync();
            }
            catch (Exception ex) { }

            MessageBusService.GameStopped(game);

            RunningProcesses.Remove(game.Id);

            await PlaySessionService.EndSession(game.Id, userId);

            foreach (var manifest in manifests)
            {
                #region Upload Saves
                try
                {
                    await SaveService.UploadAsync(game.InstallDirectory, manifest.Id);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Could not upload save");
                }
                #endregion

                await Client.Scripts.RunAfterStopScriptAsync(game.InstallDirectory, game.Id);
            }
        }
    }
}
