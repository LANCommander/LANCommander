using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Models.Enums;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Logging;
using Steamworks.Ugc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class FilterService : BaseService
    {
        private readonly LibraryService LibraryService;
        private readonly GameService GameService;

        public LibraryFilterModel Filter { get; set; }

        public ICollection<Engine> Engines { get; private set; }
        public ICollection<Genre> Genres { get; private set; }
        public ICollection<Tag> Tags { get; private set; }
        public ICollection<Platform> Platforms { get; private set; }
        public ICollection<Company> Developers { get; private set; }
        public ICollection<Company> Publishers { get; private set; }
        public int MinPlayers { get; private set; }
        public int MaxPlayers { get; private set; }

        public delegate Task OnFilterChangedHandler();
        public event OnFilterChangedHandler OnFilterChanged;

        public FilterService(
            SDK.Client client,
            ILogger<FilterService> logger,
            LibraryService libraryService,
            GameService gameService) : base(client, logger)
        {
            GameService = gameService;
            LibraryService = libraryService;
        }

        public void Populate(IEnumerable<Game> games)
        {
            var multiplayerModes = games.Where(g => g.MultiplayerModes != null).SelectMany(g => g.MultiplayerModes);

            Engines = games
                .Select(i => i.Engine)
                .Where(e => e != null)
                .DistinctBy(e => e.Id)
                .OrderBy(e => e.Name)
                .ToList();

            Genres = games
                .SelectMany(i => i.Genres)
                .Where(g => g != null)
                .DistinctBy(g => g.Id)
                .OrderBy(g => g.Name)
                .ToList();

            Tags = games
                .SelectMany(i => i.Tags)
                .Where(t => t != null)
                .DistinctBy(t => t.Id)
                .OrderBy(t => t.Name)
                .ToList();

            Platforms = games
                .SelectMany(i => i.Platforms)
                .Where(p => p != null)
                .DistinctBy(p => p.Id)
                .OrderBy(p => p.Name)
                .ToList();

            Developers = games
                .SelectMany(i => i.Developers)
                .Where(c => c != null)
                .DistinctBy(c => c.Id)
                .OrderBy(c => c.Name)
                .ToList();

            Publishers = games
                .SelectMany(i => i.Publishers)
                .Where(c => c != null)
                .DistinctBy(c => c.Id)
                .OrderBy(c => c.Name)
                .ToList();

            if (games.Any(li => li.Singleplayer))
                MinPlayers = 1;
            else if (multiplayerModes.Any())
                MinPlayers = multiplayerModes.Where(i => i != null).Min(i => i.MinPlayers);

            if (multiplayerModes.Any())
                MaxPlayers = multiplayerModes.Max(i => i.MaxPlayers);

            LoadSettings();
        }

        public IEnumerable<ListItem> FilterLibraryItems(IEnumerable<ListItem> items)
        {
            using (var op = Logger.BeginOperation(LogLevel.Trace, "Filtering library items"))
            {
                if (!String.IsNullOrWhiteSpace(Filter.Title))
                    items = items.Where(i => i.Name?.IndexOf(Filter.Title, StringComparison.OrdinalIgnoreCase) >= 0 || i.SortName?.IndexOf(Filter.Title, StringComparison.OrdinalIgnoreCase) >= 0);

                if (Filter.Engines != null && Filter.Engines.Any())
                    items = items.Where(i => Filter.Engines.Any(e => e.Id == (i.DataItem as Game)?.Engine?.Id));

                if (Filter.Genres != null && Filter.Genres.Any())
                    items = items.Where(i => Filter.Genres.Any(fg => (i.DataItem as Game).Genres.Any(g => fg.Id == g.Id)));

                if (Filter.Tags != null && Filter.Tags.Any())
                    items = items.Where(i => Filter.Tags.Any(ft => (i.DataItem as Game).Tags.Any(t => ft.Id == t.Id)));

                if (Filter.Developers != null && Filter.Developers.Any())
                    items = items.Where(i => Filter.Developers.Any(fc => (i.DataItem as Game).Developers.Any(c => fc.Id == c.Id)));

                if (Filter.Publishers != null && Filter.Publishers.Any())
                    items = items.Where(i => Filter.Publishers.Any(fc => (i.DataItem as Game).Publishers.Any(c => fc.Id == c.Id)));

                if (Filter.MinPlayers != null)
                    items = items.Where(i => (i.DataItem as Game).MultiplayerModes.Any(mm => mm.MinPlayers <= Filter.MinPlayers && mm.MaxPlayers >= Filter.MinPlayers));

                if (Filter.MaxPlayers != null)
                    items = items.Where(i => (i.DataItem as Game).MultiplayerModes.Any(mm => mm.MaxPlayers <= Filter.MaxPlayers));

                if (Filter.Installed)
                    items = items.Where(i => (i.DataItem as Game).Installed);

                items = items.Where(i => (i.DataItem as Game).Type.ValueIsIn(Data.Enums.GameType.MainGame, Data.Enums.GameType.StandaloneExpansion, Data.Enums.GameType.StandaloneMod));

                op.Complete();
            }

            return items;
        }

        public async Task ApplyFilter()
        {
            await LibraryService.FilterChanged();

            SaveSettings();
        }

        public async Task ResetFilter()
        {
            Filter = new LibraryFilterModel();

            await LibraryService.FilterChanged();

            SaveSettings();
        }

        void LoadSettings()
        {
            var settings = SettingService.GetSettings();

            Filter.Title = settings.Filter.Title;
            Filter.GroupBy = settings.Filter.GroupBy;
            Filter.Engines = settings.Filter.Engines != null ? Engines?.Where(e => settings.Filter.Engines.Contains(e.Name)).ToList() : null;
            Filter.Genres = settings.Filter.Genres != null ? Genres?.Where(e => settings.Filter.Genres.Contains(e.Name)).ToList() : null;
            Filter.Tags = settings.Filter.Tags != null ? Tags?.Where(e => settings.Filter.Tags.Contains(e.Name)).ToList() : null;
            Filter.Platforms = settings.Filter.Platforms != null ? Platforms?.Where(e => settings.Filter.Platforms.Contains(e.Name)).ToList() : null;
            Filter.Publishers = settings.Filter.Publishers != null ? Publishers?.Where(e => settings.Filter.Publishers.Contains(e.Name)).ToList() : null;
            Filter.Developers = settings.Filter.Developers != null ? Developers?.Where(e => settings.Filter.Developers.Contains(e.Name)).ToList() : null;
            Filter.MinPlayers = settings.Filter.MinPlayers;
            Filter.MaxPlayers = settings.Filter.MaxPlayers;
            Filter.Installed = settings.Filter.Installed;
        }

        void SaveSettings()
        {
            var settings = SettingService.GetSettings();

            settings.Filter = new FilterSettings()
            {
                Title = Filter.Title,
                GroupBy = Filter.GroupBy,
                Engines = Filter.Engines?.Select(e => e.Name),
                Genres = Filter.Genres?.Select(g => g.Name),
                Tags = Filter.Tags?.Select(t => t.Name),
                Platforms = Filter.Platforms?.Select(p => p.Name),
                Developers = Filter.Developers?.Select(c => c.Name),
                Publishers = Filter.Publishers?.Select(c => c.Name),
                MinPlayers = Filter.MinPlayers,
                MaxPlayers = Filter.MaxPlayers,
                Installed = Filter.Installed
            };

            SettingService.SaveSettings(settings);
        }
    }
}
