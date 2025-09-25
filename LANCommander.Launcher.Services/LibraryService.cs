using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace LANCommander.Launcher.Services
{
    public class LibraryService : BaseDatabaseService<Library>
    {
        private readonly AuthenticationService AuthenticationService;
        private readonly InstallService InstallService;
        private readonly GameService GameService;
        private readonly SDK.Client Client;

        public Dictionary<Guid, Process> RunningProcesses = new Dictionary<Guid, Process>();

        public ObservableCollection<ListItem> Items { get; set; } = new ObservableCollection<ListItem>();

        public delegate Task OnLibraryChangedHandler(IEnumerable<ListItem> items);
        public event OnLibraryChangedHandler? OnLibraryChanged;

        public delegate Task OnLibraryItemsUpdatedHandler(IEnumerable<ListItem> itemsUpdatedOrAdded, IEnumerable<ListItem> itemsRemoved);
        public event OnLibraryItemsUpdatedHandler? OnLibraryItemsUpdated;

        public delegate Task OnPreLibraryItemsFilteredHandler(IEnumerable<ListItem> items);
        public event OnPreLibraryItemsFilteredHandler OnPreLibraryItemsFiltered;

        public delegate Task OnItemsFilteredHandler(IEnumerable<ListItem> items);
        public event OnItemsFilteredHandler OnItemsFiltered;

        public LibraryFilter Filter { get; set; } = new LibraryFilter();

        public LibraryService(
            DatabaseContext databaseContext,
            SDK.Client client,
            ILogger<LibraryService> logger,
            AuthenticationService authenticationService,
            InstallService installService,
            GameService gameService,
            UserService userService) : base(databaseContext, logger)
        {
            AuthenticationService = authenticationService;
            InstallService = installService;
            GameService = gameService;
            Client = client;

            InstallService.OnInstallComplete += InstallService_OnInstallComplete;
            Filter.OnChanged += Filter_OnChanged;
        }

        private async Task Filter_OnChanged()
        {
            if (OnItemsFiltered != null)
                await OnItemsFiltered.Invoke(Filter.ApplyFilter(Items));
        }

        private async Task InstallService_OnInstallComplete(Game game)
        {
            await LibraryChanged();
        }

        public async Task<IEnumerable<ListItem>> RefreshItemsAsync(bool forcePersistent = false)
        {
            if (forcePersistent)
            {
                // clearing pending changes in tracker to force load data from database
                Context.ChangeTracker.Clear();
            }

            Items = new ObservableCollection<ListItem>(await GetItemsAsync());

            await LibraryChanged();

            if (OnItemsFiltered != null)
                await OnItemsFiltered.Invoke(Filter.ApplyFilter(Items));

            return Items;
        }

        public IEnumerable<ListItem> GetItems<T>()
        {
            return Items.Where(i => i.DataItem is T).DistinctBy(i => i.Key);
        }

        public async Task<Library> GetByUserAsync(Guid userId)
        {
            var user = await Context
                .Users
                .Include(u => u.Library)
                .ThenInclude(l => l.Games)
                .FirstOrDefaultAsync(u => u.Id == userId);

            try
            {
                if (user == null)
                {
                    user = new User
                    {
                        Id = userId,
                    };
                
                    user = Context.Users.Add(user).Entity;
                    
                    await Context.SaveChangesAsync();
                }

                if (user.Library == null)
                {
                    user.Library = new Library();
                
                    user = Context.Users.Update(user).Entity;
                
                    await Context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                
            }


            return user.Library;
        }

        private static bool IsInstalled(ListItem item)
        {
            return item != null && (item.State == ListItemState.Installed || item.State == ListItemState.UpdateAvailable);
        }

        public bool IsInstalled(Guid itemId)
        {
            return Items.Any(i => i.Key == itemId && IsInstalled(i));
        }

        public async Task<bool> IsInstalledAsync(Guid itemId)
        {
            var items = await GetItemsAsync();
            return items.Any(i => IsInstalled(i));
        }

        public bool IsInLibrary(Guid itemId)
        {
            return Items.Any(i => i.Key == itemId);
        }

        public async Task<IEnumerable<ListItem>> GetItemsAsync()
        {
            Items.Clear();

            using (var op = Logger.BeginOperation(LogLevel.Trace, "Loading library items from local database"))
            {
                var games = await Context
                    .Games
                    .AsSplitQuery()
                    .Where(g => g.Libraries.Any(l => l.UserId == AuthenticationService.GetUserId()))
                    .Include(g => g.Media)
                    .Include(g => g.Platforms)
                    .Include(g => g.Collections)
                    .Include(g => g.Genres)
                    .Include(g => g.Engine)
                    .Include(g => g.Publishers)
                    .Include(g => g.Developers)
                    .Include(g => g.Tags)
                    .Include(g => g.MultiplayerModes)
                    .Include(g => g.DependentGames)
                    .ToListAsync();

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
            var game = await Context.Games
                .AsSplitQuery()
                .Include(g => g.Collections)
                .Include(g => g.Developers)
                .Include(g => g.Genres)
                .Include(g => g.Publishers)
                .Include(g => g.Tags)
                .Include(g => g.PlaySessions)
                .Include(g => g.Engine)
                .Include(g => g.Platforms)
                .Include(g => g.Media)
                .Include(g => g.MultiplayerModes)
                .Include(g => g.DependentGames)
                .FirstOrDefaultAsync(g => g.Id == key);

            return new ListItem(game);
        }

        public async Task AddToLibraryAsync(Game game)
        {
            var library = await GetByUserAsync(AuthenticationService.GetUserId());

            if (library.Games.Where(g => g != null).All(g => g.Id != game.Id))
            {
                library.Games.Add(game);

                await UpdateAsync(library);
            }

            if (Items.All(i => i.Key != game.Id))
            {
                var item = new ListItem(game);
                Items.Add(item);

                await LibraryItemsUpdated(itemsUpdatedOrAdded: [item]);
            }
        }

        public async Task AddToLibraryAsync(Guid id)
        {
            var localGame = await GameService.GetAsync(id);

            await AddToLibraryAsync(localGame);

            await Client.Library.AddToLibrary(id);
        }

        public Task RemoveFromLibraryAsync(Guid id)
        {
            return RemoveFromLibraryAsync(id, []);
        }

        public async Task RemoveFromLibraryAsync(Guid id, params Guid[] addonIds)
        {
            var localGame = await GameService.GetAsync(id);
            var library = await GetByUserAsync(AuthenticationService.GetUserId());

            // update library items

            addonIds ??= [];
            foreach (var addonId in addonIds)
            {
                var localAddon = await GameService.GetAsync(addonId);
                if (localAddon != null)
                {
                    library.Games.Remove(localAddon);
                }
            }

            library.Games.Remove(localGame);
            
            await UpdateAsync(library);

            await Client.Library.RemoveFromLibrary(id, addonIds);


            // handle removing item from libray list locally
            
            var itemToRemove = Items.FirstOrDefault(i => i.Key == id);
            
            if (itemToRemove != null)
                Items.Remove(itemToRemove);

            var addonsToRemove = Items.Where(i => addonIds.Contains(i.Key));
            Items.RemoveRange(addonsToRemove);

            var itemsRemoved = addonsToRemove.Concat([itemToRemove!]) ?? [];
            await LibraryItemsUpdated(itemsRemoved: itemsRemoved);
        }

        public async Task LibraryChanged(IEnumerable<ListItem>? items = null)
        {
            if (OnLibraryChanged != null)
                await OnLibraryChanged.Invoke(items ?? Items);
        }

        public async Task LibraryItemsUpdated(IEnumerable<ListItem>? itemsUpdatedOrAdded = null, IEnumerable<ListItem>? itemsRemoved = null)
        {
            if (OnLibraryItemsUpdated != null)
            {
                itemsUpdatedOrAdded = itemsUpdatedOrAdded?.ToArray() ?? [];
                itemsRemoved = itemsRemoved?.ToArray() ?? [];
                await OnLibraryItemsUpdated(itemsUpdatedOrAdded: itemsUpdatedOrAdded, itemsRemoved: itemsRemoved);
            }
        }

        public async Task FilterChanged()
        {
            Items = new ObservableCollection<ListItem>(await GetItemsAsync());
        }
    }
}
