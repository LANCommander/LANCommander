using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Steamworks.Ugc;
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
        private readonly InstallService InstallService;
        private readonly GameService GameService;
        private readonly SaveService SaveService;
        private readonly PlaySessionService PlaySessionService;
        private readonly RedistributableService RedistributableService;
        private readonly ImportService ImportService;
        private readonly MessageBusService MessageBusService;

        public Dictionary<Guid, Process> RunningProcesses = new Dictionary<Guid, Process>();

        public ObservableCollection<ListItem> Items { get; set; } = new ObservableCollection<ListItem>();

        public delegate Task OnLibraryChangedHandler(IEnumerable<ListItem> items);
        public event OnLibraryChangedHandler OnLibraryChanged;

        public delegate Task OnPreLibraryItemsFilteredHandler(IEnumerable<ListItem> items);
        public event OnPreLibraryItemsFilteredHandler OnPreLibraryItemsFiltered;

        public delegate Task OnItemsFilteredHandler(IEnumerable<ListItem> items);
        public event OnItemsFilteredHandler OnItemsFiltered;

        public LibraryFilter Filter { get; set; } = new LibraryFilter();

        public LibraryService(
            SDK.Client client,
            ILogger<LibraryService> logger,
            InstallService installService,
            GameService gameService,
            SaveService saveService,
            PlaySessionService playSessionService,
            RedistributableService redistributableService,
            ImportService importService,
            MessageBusService messageBusService) : base(client, logger)
        {
            InstallService = installService;
            GameService = gameService;
            SaveService = saveService;
            PlaySessionService = playSessionService;
            RedistributableService = redistributableService;
            ImportService = importService;
            MessageBusService = messageBusService;

            InstallService.OnInstallComplete += InstallService_OnInstallComplete;
            ImportService.OnImportComplete += ImportService_OnImportComplete;
            Filter.OnChanged += Filter_OnChanged;
        }

        private async Task Filter_OnChanged()
        {
            if (OnItemsFiltered != null)
                await OnItemsFiltered.Invoke(Filter.ApplyFilter(Items));
        }

        private async Task ImportService_OnImportComplete()
        {
            await RefreshItemsAsync();

            LibraryChanged();
        }

        private async Task InstallService_OnInstallComplete(Game game)
        {
            if (OnLibraryChanged != null)
                await OnLibraryChanged.Invoke(Items);
        }

        public async Task<IEnumerable<ListItem>> RefreshItemsAsync()
        {
            Items = new ObservableCollection<ListItem>(await GetItemsAsync());

            if (OnItemsFiltered != null)
                await OnItemsFiltered.Invoke(Filter.ApplyFilter(Items));

            return Items;
        }

        public IEnumerable<ListItem> GetItems<T>()
        {
            return Items.Where(i => i.DataItem is T).DistinctBy(i => i.Key);
        }

        public async Task<IEnumerable<ListItem>> GetItemsAsync()
        {
            Items.Clear();

            using (var op = Logger.BeginOperation(LogLevel.Trace, "Loading library items from local database"))
            {
                var games = await GameService.Get(x => true).AsNoTracking().ToListAsync();

                Filter.Populate(games);

                foreach (var item in games.Select(g => new ListItem(g)).OrderByTitle(g => !String.IsNullOrWhiteSpace(g.SortName) ? g.SortName : g.Name))
                {
                    Items.Add(item);
                }

                op.Complete();
            }

            return Items;
        }

        public IEnumerable<ListItem> GetFilteredItems()
        {
            return Filter.ApplyFilter(Items);
        }

        public ListItem GetItem(Guid key)
        {
            var item = Items.FirstOrDefault(i => i.Key == key);

            if (item == null)
                item = Items.FirstOrDefault(i => i.Key == key);

            return item;
        }

        public async Task<ListItem> GetItemAsync(ListItem libraryItem)
        {
            return await GetItemAsync(libraryItem.Key);
        }

        public async Task<ListItem> GetItemAsync(Guid key)
        {
            var game = await GameService.Get(key);

            return new ListItem(game);
        }

        public async Task AddToLibraryAsync(Guid id)
        {
            await Client.Library.AddToLibrary(id);

            await ImportService.ImportGameAsync(id);

            await LibraryChanged();
        }

        public async Task RemoveFromLibraryAsync(Guid id)
        {
            await Client.Library.RemoveFromLibrary(id);

            var localGame = await GameService.Get(id);

            await GameService.Delete(localGame);

            await LibraryChanged();
        }

        public async Task LibraryChanged()
        {
            if (OnLibraryChanged != null)
                await OnLibraryChanged.Invoke(Items);
        }

        public async Task FilterChanged()
        {
            Items = new ObservableCollection<ListItem>(await GetItemsAsync());
        }
    }
}
