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

        public void Populate(IEnumerable<SDK.Models.DepotGame> games)
        {
            var multiplayerModes = games.Where(g => g.MultiplayerModes != null).SelectMany(g => g.MultiplayerModes);

            DataSource.Engines = games
                .Select(i => i.Engine)
                .Where(e => e != null)
                .DistinctBy(e => e.Id)
                .OrderBy(e => e.Name)
                .ToList();

            DataSource.Genres = games
                .SelectMany(i => i.Genres)
                .Where(g => g != null)
                .DistinctBy(g => g.Id)
                .OrderBy(g => g.Name)
                .ToList();

            DataSource.Tags = games
                .SelectMany(i => i.Tags)
                .Where(t => t != null)
                .DistinctBy(t => t.Id)
                .OrderBy(t => t.Name)
                .ToList();

            DataSource.Platforms = games
                .SelectMany(i => i.Platforms)
                .Where(p => p != null)
                .DistinctBy(p => p.Id)
                .OrderBy(p => p.Name)
                .ToList();

            DataSource.Developers = games
                .SelectMany(i => i.Developers)
                .Where(c => c != null)
                .DistinctBy(c => c.Id)
                .OrderBy(c => c.Name)
            .ToList();

            DataSource.Publishers = games
                .SelectMany(i => i.Publishers)
                .Where(c => c != null)
                .DistinctBy(c => c.Id)
                .OrderBy(c => c.Name)
                .ToList();

            if (games.Any(li => li.Singleplayer))
                DataSource.MinPlayers = 1;
            else if (multiplayerModes.Any())
                DataSource.MinPlayers = multiplayerModes.Where(i => i != null).Min(i => i.MinPlayers);

            if (multiplayerModes.Any())
                DataSource.MaxPlayers = multiplayerModes.Max(i => i.MaxPlayers);

            Initialized = true;
        }

        public IEnumerable<ListItem> ApplyFilter(IEnumerable<ListItem> items)
        {
            if (!String.IsNullOrWhiteSpace(SelectedOptions.Title))
                items = items.Where(i => i.Name?.IndexOf(SelectedOptions.Title, StringComparison.OrdinalIgnoreCase) >= 0 || i.SortName?.IndexOf(SelectedOptions.Title, StringComparison.OrdinalIgnoreCase) >= 0);

            if (SelectedOptions.Engines != null && SelectedOptions.Engines.Any())
                items = items.Where(i => SelectedOptions.Engines.Any(e => e.Id == (i.DataItem as DepotGame)?.Engine?.Id));

            if (SelectedOptions.Genres != null && SelectedOptions.Genres.Any())
                items = items.Where(i => SelectedOptions.Genres.Any(fg => (i.DataItem as DepotGame).Genres.Any(g => fg.Id == g.Id)));

            if (SelectedOptions.Tags != null && SelectedOptions.Tags.Any())
                items = items.Where(i => SelectedOptions.Tags.Any(ft => (i.DataItem as DepotGame).Tags.Any(t => ft.Id == t.Id)));

            if (SelectedOptions.Developers != null && SelectedOptions.Developers.Any())
                items = items.Where(i => SelectedOptions.Developers.Any(fc => (i.DataItem as DepotGame).Developers.Any(c => fc.Id == c.Id)));

            if (SelectedOptions.Publishers != null && SelectedOptions.Publishers.Any())
                items = items.Where(i => SelectedOptions.Publishers.Any(fc => (i.DataItem as DepotGame).Publishers.Any(c => fc.Id == c.Id)));

            if (SelectedOptions.MinPlayers != null)
                items = items.Where(i => (i.DataItem as DepotGame).MultiplayerModes.Any(mm => mm.MinPlayers <= SelectedOptions.MinPlayers && mm.MaxPlayers >= SelectedOptions.MinPlayers));

            if (SelectedOptions.MaxPlayers != null)
                items = items.Where(i => (i.DataItem as DepotGame).MultiplayerModes.Any(mm => mm.MaxPlayers <= SelectedOptions.MaxPlayers));

            items = items.Where(i => (i.DataItem as DepotGame).Type.IsIn(SDK.Enums.GameType.MainGame, SDK.Enums.GameType.StandaloneExpansion, SDK.Enums.GameType.StandaloneMod));

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
                        (g.DataItem as DepotGame)?.Collections?
                            .Where(c => c?.Name != null)
                            .Select(c => c.Name)
                            .ToArray() ?? Array.Empty<string>();
                    break;

                case Enums.GroupBy.Genre:
                    GroupSelector = (g) =>
                        (g.DataItem as DepotGame)?.Genres?
                            .Where(ge => ge?.Name != null)
                            .Select(ge => ge.Name)
                            .ToArray() ?? Array.Empty<string>();
                    break;

                case Enums.GroupBy.Platform:
                    GroupSelector = (g) =>
                        (g.DataItem as DepotGame)?.Platforms?
                            .Where(p => p?.Name != null)
                            .Select(p => p.Name)
                            .ToArray() ?? Array.Empty<string>();
                    break;
            }

            foreach (var item in items)
            {
                item.Groups = GroupSelector.Invoke(item);
            }

            return items;
        }

        public async Task UpdateFilter()
        {
            if (OnChanged != null)
                await OnChanged();
        }

        public async Task ResetFilter()
        {
            SelectedOptions = new DepotFilterModel();

            if (OnChanged != null)
                await OnChanged();
        }
    }
}
