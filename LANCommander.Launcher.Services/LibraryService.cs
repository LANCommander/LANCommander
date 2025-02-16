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
    public class LibraryService : BaseDatabaseService<Library>
    {
        private readonly AuthenticationService AuthenticationService;
        private readonly InstallService InstallService;
        private readonly GameService GameService;
        private readonly UserService UserService;

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
            DatabaseContext databaseContext,
            SDK.Client client,
            ILogger<LibraryService> logger,
            AuthenticationService authenticationService,
            InstallService installService,
            GameService gameService,
            UserService userService) : base(databaseContext, client, logger)
        {
            AuthenticationService = authenticationService;
            InstallService = installService;
            GameService = gameService;
            UserService = userService;

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
                .FirstOrDefaultAsync(g => g.Id == key);

            return new ListItem(game);
        }

        public async Task AddToLibraryAsync(Game game)
        {
            var library = await GetByUserAsync(AuthenticationService.GetUserId());

            if (!library.Games.Any(g => g.Id == game.Id))
            {
                library.Games.Add(game);

                await UpdateAsync(library);
            }

            if (!Items.Any(i => i.Key == game.Id))
            {
                Items.Add(new ListItem(game));

                await LibraryChanged();
            
                if (OnItemsFiltered != null)
                    await OnItemsFiltered.Invoke(Filter.ApplyFilter(Items));
            }
        }

        public async Task AddToLibraryAsync(Guid id)
        {
            var localGame = await GameService.GetAsync(id);

            await AddToLibraryAsync(localGame);

            await Client.Library.AddToLibrary(id);
        }

        public async Task RemoveFromLibraryAsync(Guid id)
        {
            var localGame = await GameService.GetAsync(id);
            var library = await GetByUserAsync(AuthenticationService.GetUserId());

            library.Games.Remove(localGame);
            
            await UpdateAsync(library);
            
            await Client.Library.RemoveFromLibrary(id);
            
            var itemToRemove = Items.FirstOrDefault(i => i.Key == id);
            
            if (itemToRemove != null)
                Items.Remove(itemToRemove);

            await LibraryChanged();
            
            if (OnItemsFiltered != null)
                await OnItemsFiltered.Invoke(Filter.ApplyFilter(Items));
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
