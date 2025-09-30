using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public class LibraryFilter
    {
        private bool Initialized = false;

        public LibraryFilterModel DataSource { get; set; } = new LibraryFilterModel();
        public LibraryFilterModel SelectedOptions { get; set; } = new LibraryFilterModel();

        public delegate Task OnChangedHandler();
        public event OnChangedHandler OnChanged;

        public Func<ListItem, string[]> GroupSelector { get; set; } = _ => new string[] { };

        public void Populate(IEnumerable<Game> games)
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

            if (!Initialized)
                LoadSelectedOptions();

            Initialized = true;
        }

        public IEnumerable<ListItem> ApplyFilter(IEnumerable<ListItem> items)
        {
            if (!String.IsNullOrWhiteSpace(SelectedOptions.Title))
                items = items.Where(i => i.Name?.IndexOf(SelectedOptions.Title, StringComparison.OrdinalIgnoreCase) >= 0 || i.SortName?.IndexOf(SelectedOptions.Title, StringComparison.OrdinalIgnoreCase) >= 0);

            if (SelectedOptions.Engines != null && SelectedOptions.Engines.Any())
                items = items.Where(i => SelectedOptions.Engines.Any(e => e.Id == (i.DataItem as Game)?.Engine?.Id));

            if (SelectedOptions.Genres != null && SelectedOptions.Genres.Any())
                items = items.Where(i => SelectedOptions.Genres.Any(fg => (i.DataItem as Game).Genres.Any(g => fg.Id == g.Id)));

            if (SelectedOptions.Tags != null && SelectedOptions.Tags.Any())
                items = items.Where(i => SelectedOptions.Tags.Any(ft => (i.DataItem as Game).Tags.Any(t => ft.Id == t.Id)));

            if (SelectedOptions.Developers != null && SelectedOptions.Developers.Any())
                items = items.Where(i => SelectedOptions.Developers.Any(fc => (i.DataItem as Game).Developers.Any(c => fc.Id == c.Id)));

            if (SelectedOptions.Publishers != null && SelectedOptions.Publishers.Any())
                items = items.Where(i => SelectedOptions.Publishers.Any(fc => (i.DataItem as Game).Publishers.Any(c => fc.Id == c.Id)));

            if (SelectedOptions.MinPlayers != null)
                items = items.Where(i => (i.DataItem as Game).MultiplayerModes.Any(mm => mm.MinPlayers <= SelectedOptions.MinPlayers && mm.MaxPlayers >= SelectedOptions.MinPlayers));

            if (SelectedOptions.MaxPlayers != null)
                items = items.Where(i => (i.DataItem as Game).MultiplayerModes.Any(mm => mm.MaxPlayers <= SelectedOptions.MaxPlayers));

            if (SelectedOptions.Installed)
                items = items.Where(i => (i.DataItem as Game).Installed);

            items = items.Where(i => (i.DataItem as Game).Type.ValueIsIn(Data.Enums.GameType.MainGame, Data.Enums.GameType.StandaloneExpansion, Data.Enums.GameType.StandaloneMod));

            switch (SelectedOptions.SortBy)
            {
                case Enums.SortBy.Title:
                    items = items.OrderByTitle(i => i.Name ?? i.SortName, SelectedOptions.SortDirection);
                    break;

                case Enums.SortBy.DateAdded:
                    items = items.OrderBy(i =>
                        (i.DataItem as Game)?.CreatedOn ?? DateTime.MinValue,
                        SelectedOptions.SortDirection);
                    break;

                case Enums.SortBy.DateReleased:
                    items = items.OrderBy(i =>
                        (i.DataItem as Game)?.ReleasedOn ?? DateTime.MinValue,
                        SelectedOptions.SortDirection);
                    break;

                case Enums.SortBy.RecentActivity:
                    items = items.OrderBy(i =>
                        (i.DataItem as Game)?.PlaySessions?
                            .Where(ps => ps?.End != null)
                            .OrderByDescending(ps => ps.End ?? DateTime.MinValue)
                            .FirstOrDefault()?.End ?? DateTime.MinValue,
                        SelectedOptions.SortDirection);
                    break;
                
                case Enums.SortBy.MostPlayed:
                    items = items.OrderBy(i =>
                        (i.DataItem as Game)?.PlaySessions?.Sum(
                            ps => ps.End.Value.Subtract(ps.Start.Value).TotalSeconds), SelectedOptions.SortDirection);
                    break;
            }

            switch (SelectedOptions.GroupBy)
            {
                case Enums.GroupBy.None:
                    GroupSelector = (g) => Array.Empty<string>();
                    break;

                case Enums.GroupBy.Collection:
                    GroupSelector = (g) =>
                        (g.DataItem as Game)?.Collections?
                            .Where(c => c?.Name != null)
                            .Select(c => c.Name)
                            .ToArray() ?? Array.Empty<string>();
                    break;

                case Enums.GroupBy.Genre:
                    GroupSelector = (g) =>
                        (g.DataItem as Game)?.Genres?
                            .Where(ge => ge?.Name != null)
                            .Select(ge => ge.Name)
                            .ToArray() ?? Array.Empty<string>();
                    break;

                case Enums.GroupBy.Platform:
                    GroupSelector = (g) =>
                        (g.DataItem as Game)?.Platforms?
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

            SaveSelectedOptions();
        }

        public async Task ResetFilter()
        {
            SelectedOptions = new LibraryFilterModel();

            if (OnChanged != null)
                await OnChanged();

            SaveSelectedOptions();
        }

        void LoadSelectedOptions()
        {
            /*var settingsProvider = SettingService.GetSettings();

            SelectedOptions.Title = settings.Filter.Title;
            SelectedOptions.GroupBy = settings.Filter.GroupBy;
            SelectedOptions.SortBy = settings.Filter.SortBy;
            SelectedOptions.SortDirection = settings.Filter.SortDirection;
            SelectedOptions.Engines = settings.Filter.Engines != null ? DataSource.Engines?.Where(e => settings.Filter.Engines.Contains(e.Name)).ToList() : null;
            SelectedOptions.Genres = settings.Filter.Genres != null ? DataSource.Genres?.Where(e => settings.Filter.Genres.Contains(e.Name)).ToList() : null;
            SelectedOptions.Tags = settings.Filter.Tags != null ? DataSource.Tags?.Where(e => settings.Filter.Tags.Contains(e.Name)).ToList() : null;
            SelectedOptions.Platforms = settings.Filter.Platforms != null ? DataSource.Platforms?.Where(e => settings.Filter.Platforms.Contains(e.Name)).ToList() : null;
            SelectedOptions.Publishers = settings.Filter.Publishers != null ? DataSource.Publishers?.Where(e => settings.Filter.Publishers.Contains(e.Name)).ToList() : null;
            SelectedOptions.Developers = settings.Filter.Developers != null ? DataSource.Developers?.Where(e => settings.Filter.Developers.Contains(e.Name)).ToList() : null;
            SelectedOptions.MinPlayers = settings.Filter.MinPlayers;
            SelectedOptions.MaxPlayers = settings.Filter.MaxPlayers;
            SelectedOptions.Installed = settings.Filter.Installed;*/
        }

        void SaveSelectedOptions()
        {
            /*var settings = SettingService.GetSettings();

            settings.Filter = new FilterSettings()
            {
                Title = SelectedOptions.Title,
                GroupBy = SelectedOptions.GroupBy,
                SortBy = SelectedOptions.SortBy,
                SortDirection = SelectedOptions.SortDirection,
                Engines = SelectedOptions.Engines?.Select(e => e.Name),
                Genres = SelectedOptions.Genres?.Select(g => g.Name),
                Tags = SelectedOptions.Tags?.Select(t => t.Name),
                Platforms = SelectedOptions.Platforms?.Select(p => p.Name),
                Developers = SelectedOptions.Developers?.Select(c => c.Name),
                Publishers = SelectedOptions.Publishers?.Select(c => c.Name),
                MinPlayers = SelectedOptions.MinPlayers,
                MaxPlayers = SelectedOptions.MaxPlayers,
                Installed = SelectedOptions.Installed
            };

            SettingService.SaveSettings(settings);*/
        }
    }
}
