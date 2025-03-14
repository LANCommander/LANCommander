using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public class DepotFilter
    {
        private bool Initialized = false;

        public DepotFilterModel DataSource { get; set; } = new DepotFilterModel();
        public DepotFilterModel SelectedOptions { get; set; } = new DepotFilterModel();

        public delegate Task OnChangedHandler();
        public event OnChangedHandler OnChanged;

        public Func<ListItem, string[]> GroupSelector { get; set; } = _ => new string[] { };

        public void Populate(DepotResults results)
        {
            if (results.Games == null)
                results.Games = new List<DepotGame>();
            
            DataSource.Collections = results.Collections?.OrderBy(c => c.Name).ToList() ?? new List<SDK.Models.Collection>();
            DataSource.Engines = results.Engines?.OrderBy(e => e.Name).ToList() ?? new List<SDK.Models.Engine>();
            DataSource.Genres = results.Genres?.OrderBy(g => g.Name).ToList() ?? new List<SDK.Models.Genre>();
            DataSource.Tags = results.Tags?.OrderBy(t => t.Name).ToList() ?? new List<SDK.Models.Tag>();
            DataSource.Platforms = results.Platforms?.OrderBy(p => p.Name).ToList() ?? new List<SDK.Models.Platform>();
            DataSource.Developers = results.Companies?.OrderBy(c => c.Name).ToList() ?? new List<SDK.Models.Company>();
            DataSource.Publishers = results.Companies?.OrderBy(c => c.Name).ToList() ?? new List<SDK.Models.Company>();

            if (results.Games.Any())
            {
                var multiplayerModes = results.Games
                    .Where(g => g.MultiplayerModes != null)
                    .SelectMany(g => g.MultiplayerModes);

                if (results.Games.Any(li => li.Singleplayer))
                    DataSource.MinPlayers = 1;
                else if (multiplayerModes.Any())
                    DataSource.MinPlayers = multiplayerModes.Where(i => i != null).Min(i => i.MinPlayers);

                if (multiplayerModes.Any())
                    DataSource.MaxPlayers = multiplayerModes.Max(i => i.MaxPlayers);
            }

            Initialized = true;
        }

        public IEnumerable<ListItem> ApplyFilter(IEnumerable<ListItem> items)
        {
            if (!String.IsNullOrWhiteSpace(SelectedOptions.Title))
                items = items.Where(i => i.Name?.IndexOf(SelectedOptions.Title, StringComparison.OrdinalIgnoreCase) >= 0 || i.SortName?.IndexOf(SelectedOptions.Title, StringComparison.OrdinalIgnoreCase) >= 0);

            if (SelectedOptions.Engines != null && SelectedOptions.Engines.Any())
                items = items.Where(i => SelectedOptions.Engines.Any(e => (i.DataItem as DepotGame).EngineId == e.Id));

            if (SelectedOptions.Collections != null && SelectedOptions.Collections.Any())
                items = items.Where(i => SelectedOptions.Collections.Any(fc => (i.DataItem as DepotGame).Collections.Any(c => c.Id == fc.Id)));

            if (SelectedOptions.Genres != null && SelectedOptions.Genres.Any())
                items = items.Where(i => SelectedOptions.Genres.Any(fg => (i.DataItem as DepotGame).Genres.Any(g => g.Id == fg.Id)));

            if (SelectedOptions.Tags != null && SelectedOptions.Tags.Any())
                items = items.Where(i => SelectedOptions.Tags.Any(ft => (i.DataItem as DepotGame).Tags.Any(t => t.Id == ft.Id)));

            if (SelectedOptions.Developers != null && SelectedOptions.Developers.Any())
                items = items.Where(i => SelectedOptions.Developers.Any(fc => (i.DataItem as DepotGame).Developers.Any(c => c.Id == fc.Id)));

            if (SelectedOptions.Publishers != null && SelectedOptions.Publishers.Any())
                items = items.Where(i => SelectedOptions.Publishers.Any(fc => (i.DataItem as DepotGame).Publishers.Any(c => c.Id == fc.Id)));

            if (SelectedOptions.Platforms != null && SelectedOptions.Platforms.Any())
                items = items.Where(i => SelectedOptions.Platforms.Any(fp => (i.DataItem as DepotGame).Platforms.Any(p => p.Id == fp.Id)));

            if (SelectedOptions.MinPlayers != null)
                items = items.Where(i => (i.DataItem as DepotGame).MultiplayerModes.Any(mm => mm.MinPlayers <= SelectedOptions.MinPlayers && mm.MaxPlayers >= SelectedOptions.MinPlayers));

            if (SelectedOptions.MaxPlayers != null)
                items = items.Where(i => (i.DataItem as DepotGame).MultiplayerModes.Any(mm => mm.MaxPlayers <= SelectedOptions.MaxPlayers));

            items = items.Where(i => (i.DataItem as DepotGame).Type.ValueIsIn(SDK.Enums.GameType.MainGame, SDK.Enums.GameType.StandaloneExpansion, SDK.Enums.GameType.StandaloneMod));

            switch (SelectedOptions.SortBy)
            {
                case Enums.SortBy.Title:
                    items = items.OrderByTitle(i => i.Name ?? i.SortName, SelectedOptions.SortDirection);
                    break;

                case Enums.SortBy.DateAdded:
                    items = items.OrderBy(i =>
                        (i.DataItem as DepotGame)?.CreatedOn ?? DateTime.MinValue,
                        SelectedOptions.SortDirection);
                    break;

                case Enums.SortBy.DateReleased:
                    items = items.OrderBy(i =>
                        (i.DataItem as DepotGame)?.ReleasedOn ?? DateTime.MinValue,
                        SelectedOptions.SortDirection);
                    break;
            }

            switch (SelectedOptions.GroupBy)
            {
                case Enums.GroupBy.None:
                    GroupSelector = (g) => Array.Empty<string>();
                    break;

                case Enums.GroupBy.Collection:
                    GroupSelector = (g) =>
                        DataSource.Collections
                            .Where(c => (g.DataItem as DepotGame).Collections.Any(gc => gc.Id == c.Id))
                            .Select(c => c.Name)
                            .ToArray() ?? Array.Empty<string>();
                    break;

                case Enums.GroupBy.Genre:
                    GroupSelector = (g) =>
                        DataSource.Genres
                            .Where(ge => (g.DataItem as DepotGame).Genres.Any(gge => gge.Id == ge.Id))
                            .Select(g => g.Name)
                            .ToArray() ?? Array.Empty<string>();
                    break;

                case Enums.GroupBy.Platform:
                    GroupSelector = (g) =>
                        DataSource.Platforms
                            .Where(p => (g.DataItem as DepotGame).Platforms.Any(gp => gp.Id == p.Id))
                            .Select(g => g.Name)
                            .ToArray() ?? Array.Empty<string>();
                    break;
            }

            foreach (var item in items)
            {
                item.Groups = GroupSelector.Invoke(item);
            }

            return items;
        }

        public async Task UpdateFilterAsync()
        {
            if (OnChanged != null)
                await OnChanged();
        }

        public async Task ResetFilterAsync()
        {
            SelectedOptions = new DepotFilterModel();

            if (OnChanged != null)
                await OnChanged();
        }
    }
}
