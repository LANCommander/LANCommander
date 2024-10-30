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

        public ObservableCollection<LibraryItem> LibraryItems { get; set; } = new ObservableCollection<LibraryItem>();

        public delegate Task OnLibraryChangedHandler(IEnumerable<LibraryItem> items);
        public event OnLibraryChangedHandler OnLibraryChanged;

        public delegate Task OnPreLibraryItemsFilteredHandler(IEnumerable<LibraryItem> items);
        public event OnPreLibraryItemsFilteredHandler OnPreLibraryItemsFiltered;

        public delegate Task OnLibraryItemsFilteredHandler(IEnumerable<LibraryItem> items);
        public event OnLibraryItemsFilteredHandler OnLibraryItemsFiltered;

        public Filter Filter { get; set; } = new Filter();

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
            if (OnLibraryItemsFiltered != null)
                await OnLibraryItemsFiltered.Invoke(Filter.ApplyFilter(LibraryItems));
        }

        private async Task ImportService_OnImportComplete()
        {
            await RefreshLibraryItemsAsync();

            LibraryChanged();
        }

        private async Task InstallService_OnInstallComplete(Game game)
        {
            if (OnLibraryChanged != null)
                await OnLibraryChanged.Invoke(LibraryItems);
        }

        public async Task<IEnumerable<LibraryItem>> RefreshLibraryItemsAsync()
        {
            LibraryItems = new ObservableCollection<LibraryItem>(await GetLibraryItemsAsync());

            if (OnLibraryItemsFiltered != null)
                await OnLibraryItemsFiltered.Invoke(Filter.ApplyFilter(LibraryItems));

            return LibraryItems;
        }

        public IEnumerable<LibraryItem> GetLibraryItems<T>()
        {
            return LibraryItems.Where(i => i.DataItem is T).DistinctBy(i => i.Key);
        }

        public async Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync()
        {
            LibraryItems.Clear();

            using (var op = Logger.BeginOperation(LogLevel.Trace, "Loading library items from local database"))
            {
                var games = await GameService.Get(x => true).AsNoTracking().ToListAsync();

                Filter.Populate(games);

                foreach (var item in games.Select(g => new LibraryItem(g)).OrderByTitle(g => !String.IsNullOrWhiteSpace(g.SortName) ? g.SortName : g.Name))
                {
                    LibraryItems.Add(item);
                }

                op.Complete();
            }

            return LibraryItems;
        }

        public IEnumerable<LibraryItem> GetFilteredItems()
        {
            return Filter.ApplyFilter(LibraryItems);
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

            return new LibraryItem(game);
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
    }
}
