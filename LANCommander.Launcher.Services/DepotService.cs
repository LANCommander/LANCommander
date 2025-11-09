using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Services
{
    public class DepotService : BaseService
    {
        public ObservableCollection<ListItem> Items { get; set; } = new ObservableCollection<ListItem>();
        public DepotFilter Filter { get; set; } = new DepotFilter();

        public delegate Task OnItemsLoadedHandler(IEnumerable<ListItem> items);
        public event OnItemsLoadedHandler OnItemsLoaded;
        
        public delegate Task OnItemsFilteredHandler(IEnumerable<ListItem> items);
        public event OnItemsFilteredHandler OnItemsFiltered;

        private DepotClient _depotClient;

        public DepotService(
            DepotClient depotClient,
            ILogger<DepotService> logger) : base(logger)
        {
            _depotClient = depotClient;
            Filter.OnChanged += Filter_OnChanged;
        }

        private async Task Filter_OnChanged()
        {
            if (OnItemsFiltered != null)
                await OnItemsFiltered.Invoke(Filter.ApplyFilter(Items));
        }

        public async Task<IEnumerable<ListItem>> RefreshItemsAsync()
        {
            using (var op = Logger.BeginDebugOperation( "Refreshing items from depot"))
            {
                Items = new ObservableCollection<ListItem>(await GetItemsAsync());

                if (OnItemsLoaded != null)
                    await OnItemsLoaded.Invoke(Items);
                if (OnItemsFiltered != null)
                    await OnItemsFiltered.Invoke(Filter.ApplyFilter(Items));

                op.Complete();
                
                return Items;
            }
        }

        public int GetItemCount()
        {
            return Items.Count;
        }

        public async Task<IEnumerable<ListItem>> GetItemsAsync()
        {
            Items.Clear();

            using (var op = Logger.BeginDebugOperation("Loading depot items from host"))
            {
                var results = await _depotClient.GetAsync();

                Filter.Populate(results);

                foreach (var item in results.Games.Select(g => new ListItem(g)).OrderByTitle(g => !String.IsNullOrWhiteSpace(g.SortName) ? g.SortName : g.Name))
                {
                    Items.Add(item);
                }

                op.Complete();
            }

            return Items;
        }

        public async Task ResetFilterAsync()
        {
            await Filter.ResetFilterAsync();
        }

        public async Task UpdateFilterAsync()
        {
            await Filter.UpdateFilterAsync();
        }
        
        public IEnumerable<ListItem> GetFilteredItems()
        {
            return Filter.ApplyFilter(Items);
        }

        public async Task FilterChanged()
        {
            Items = new ObservableCollection<ListItem>(await GetItemsAsync());
        }
    }
}
