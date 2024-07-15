using AntDesign;
using LANCommander.Client.Data;
using LANCommander.Client.Data.Models;
using LANCommander.Client.Extensions;
using LANCommander.Client.Models;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Microsoft.EntityFrameworkCore;
using Photino.NET;
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
        private readonly SaveService SaveService;
        private readonly PlaySessionService PlaySessionService;
        private readonly RedistributableService RedistributableService;
        private readonly ImportService ImportService;
        private readonly MessageBusService MessageBusService;
        private readonly PhotinoWindow Window;

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
            CollectionService collectionService,
            GameService gameService,
            SaveService saveService,
            PlaySessionService playSessionService,
            PhotinoWindow window,
            RedistributableService redistributableService,
            ImportService importService,
            MessageBusService messageBusService) : base()
        {
            Client = client;
            DownloadService = downloadService;
            CollectionService = collectionService;
            GameService = gameService;
            SaveService = saveService;
            PlaySessionService = playSessionService;
            Window = window;
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
                case Enums.GroupBy.None:
                    GroupSelector = (g) => new string[] { };
                    break;
                case Enums.GroupBy.Collection:
                    GroupSelector = (g) => (g.DataItem as Game).Collections.Select(c => c.Name).ToArray();
                    break;
                case Enums.GroupBy.Genre:
                    GroupSelector = (g) => (g.DataItem as Game).Genres.Select(ge => ge.Name).ToArray();
                    break;
                case Enums.GroupBy.Platform:
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
            // Assume for now it's a game
            var game = await GameService.Get(libraryItem.Key);

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

        public async Task Stop(Game game)
        {
            await Task.Run(() =>
            {
                if (RunningProcesses.ContainsKey(game.Id))
                {
                    var process = RunningProcesses[game.Id];

                    process.CloseMainWindow();

                    RunningProcesses.Remove(game.Id);
                }
            });
        }

        public bool IsRunning(Game game)
        {
            return IsRunning(game.Id);
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

            var monitor = Window.Monitors.First(m => m.MonitorArea.X == 0 && m.MonitorArea.Y == 0);

            Client.Actions.AddVariable("DisplayWidth", monitor.MonitorArea.Width.ToString());
            Client.Actions.AddVariable("DisplayHeight", monitor.MonitorArea.Height.ToString());
            // Client.Actions.AddVariable("DisplayRefreshRate", ((int)DeviceDisplay.Current.MainDisplayInfo.RefreshRate).ToString());

            process.StartInfo.Arguments = Client.Actions.ExpandVariables(action.Arguments, game.InstallDirectory, skipSlashes: true);
            process.StartInfo.FileName = Client.Actions.ExpandVariables(action.Path, game.InstallDirectory);
            process.StartInfo.WorkingDirectory = Client.Actions.ExpandVariables(action.WorkingDirectory, game.InstallDirectory);
            process.StartInfo.UseShellExecute = true;

            if (String.IsNullOrWhiteSpace(action.WorkingDirectory))
                process.StartInfo.WorkingDirectory = game.InstallDirectory;

            var userId = Guid.NewGuid();

            #region Run Scripts
            var manifests = GetGameManifests(game);

            foreach (var manifest in manifests)
            {
                var currentGamePlayerAlias = SDK.GameService.GetPlayerAlias(game.InstallDirectory, manifest.Id);
                var currentGameKey = SDK.GameService.GetCurrentKey(game.InstallDirectory, manifest.Id);

                #region Check Game's Player Name
                if (currentGamePlayerAlias != settings.Profile.Alias)
                    RunNameChangeScript(game, manifest.Id, currentGamePlayerAlias, settings.Profile.Alias);
                #endregion

                #region Check Key Allocation
                if (!settings.Authentication.OfflineMode && Client.IsConnected())
                {
                    var allocatedKey = await Client.Games.GetAllocatedKeyAsync(manifest.Id);

                    if (currentGameKey != allocatedKey)
                        RunKeyChangeScript(game, manifest.Id, allocatedKey);
                }
                #endregion

                #region Download Latest Saves
                if (!settings.Authentication.OfflineMode && Client.IsConnected())
                {
                    try
                    {
                        var latestSave = await Client.Saves.GetLatestAsync(manifest.Id);
                        var latestSession = await PlaySessionService.GetLatestSession(manifest.Id, userId);

                        if (latestSave != null && (latestSave.CreatedOn > latestSession.End))
                        {
                            await SaveService.DownloadLatestAsync(manifest.Id, game.InstallDirectory);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error(ex, "Could not download save");
                    }
                }
                #endregion

                #region Run Before Start Script
                RunBeforeStartScript(game, manifest.Id);
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
                RunAfterStopScript(game, manifest.Id);
            }
        }

        private IEnumerable<GameManifest> GetGameManifests(Game game)
        {
            var manifests = new List<GameManifest>();
            var mainManifest = ManifestHelper.Read(game.InstallDirectory, game.Id);

            manifests.Add(mainManifest);

            if (mainManifest.DependentGames != null)
            {
                foreach (var dependentGameId in mainManifest.DependentGames)
                {
                    try
                    {
                        var dependentGameManifest = ManifestHelper.Read(game.InstallDirectory, dependentGameId);

                        if (dependentGameManifest.Type == SDK.Enums.GameType.Expansion || dependentGameManifest.Type == SDK.Enums.GameType.Mod)
                            manifests.Add(dependentGameManifest);
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error(ex, $"Could not load manifest from dependent game {dependentGameId}");
                    }
                }
            }

            return manifests;
        }

        private void RunBeforeStartScript(Game game, Guid gameId)
        {

            try
            {
                var settings = SettingService.GetSettings();
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.BeforeStart);

                if (File.Exists(path))
                {
                    var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);
                    script.AddVariable("PlayerAlias", settings.Profile.Alias);

                    script.UseFile(path);

                    script.Execute();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run an Before Start script");
            }
        }

        private void RunAfterStopScript(Game game, Guid gameId)
        {
            try
            {
                var settings = SettingService.GetSettings();
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.AfterStop);

                if (File.Exists(path))
                {
                    var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);
                    script.AddVariable("PlayerAlias", settings.Profile.Alias);

                    script.UseFile(path);

                    script.Execute();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run an After Stop script");
            }
        }

        private void RunNameChangeScript(Game game, Guid gameId, string oldName, string newName)
        {
            try
            {
                var settings = SettingService.GetSettings();
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.NameChange);

                if (File.Exists(path))
                {
                    var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);
                    script.AddVariable("OldPlayerAlias", oldName);
                    script.AddVariable("NewPlayerAlias", newName);

                    script.UseFile(path);

                    SDK.GameService.UpdatePlayerAlias(game.InstallDirectory, gameId, newName);

                    script.Execute();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run a Name Change script");
            }
        }

        private void RunKeyChangeScript(Game game, Guid gameId, string newKey = "")
        {
            try
            {
                var settings = SettingService.GetSettings();
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.NameChange);

                if (File.Exists(path))
                {
                    var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);
                    script.AddVariable("AllocatedKey", newKey);

                    script.UseFile(path);

                    SDK.GameService.UpdateCurrentKey(game.InstallDirectory, gameId, newKey);

                    script.Execute();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run a Name Change script");
            }
        }
    }
}
