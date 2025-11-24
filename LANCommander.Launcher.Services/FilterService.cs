using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class FilterService(
        ILogger<FilterService> logger,
        LibraryService libraryService,
        SettingsProvider<Settings.Settings> settingsProvider) : BaseService(logger)
    {
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

        public void Populate(IEnumerable<Game> games)
        {
            logger.LogDebug("Populating filter with metadata from {Count} game(s)", games.Count());
            
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
            logger.LogDebug("Filtering {Count} library item(s)", items.Count());
            
            using (var op = Logger.BeginDebugOperation("Filtering library items"))
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
            logger.LogDebug("Applying filter");
            
            await libraryService.FilterChanged();

            await SaveSettingsAsync();
        }

        public async Task ResetFilter()
        {
            logger.LogDebug("Resetting filter");
            
            Filter = new LibraryFilterModel();

            await libraryService.FilterChanged();

            await SaveSettingsAsync();
        }

        void LoadSettings()
        {
            logger.LogDebug("Loading filter settings");
            
            Filter.Title = settingsProvider.CurrentValue.Filter.Title;
            Filter.GroupBy = settingsProvider.CurrentValue.Filter.GroupBy;
            Filter.Engines = settingsProvider.CurrentValue.Filter.Engines != null ? Engines?.Where(e => settingsProvider.CurrentValue.Filter.Engines.Contains(e.Name)).ToList() : null;
            Filter.Genres = settingsProvider.CurrentValue.Filter.Genres != null ? Genres?.Where(e => settingsProvider.CurrentValue.Filter.Genres.Contains(e.Name)).ToList() : null;
            Filter.Tags = settingsProvider.CurrentValue.Filter.Tags != null ? Tags?.Where(e => settingsProvider.CurrentValue.Filter.Tags.Contains(e.Name)).ToList() : null;
            Filter.Platforms = settingsProvider.CurrentValue.Filter.Platforms != null ? Platforms?.Where(e => settingsProvider.CurrentValue.Filter.Platforms.Contains(e.Name)).ToList() : null;
            Filter.Publishers = settingsProvider.CurrentValue.Filter.Publishers != null ? Publishers?.Where(e => settingsProvider.CurrentValue.Filter.Publishers.Contains(e.Name)).ToList() : null;
            Filter.Developers = settingsProvider.CurrentValue.Filter.Developers != null ? Developers?.Where(e => settingsProvider.CurrentValue.Filter.Developers.Contains(e.Name)).ToList() : null;
            Filter.MinPlayers = settingsProvider.CurrentValue.Filter.MinPlayers;
            Filter.MaxPlayers = settingsProvider.CurrentValue.Filter.MaxPlayers;
            Filter.Installed = settingsProvider.CurrentValue.Filter.Installed;
        }

        async Task SaveSettingsAsync()
        {
            logger.LogDebug("Saving filter settings");
            
            settingsProvider.Update(s =>
            {
                s.Filter.Title = Filter.Title;
                s.Filter.GroupBy = Filter.GroupBy;
                s.Filter.Engines = Filter.Engines?.Select(e => e.Name);
                s.Filter.Genres = Filter.Genres?.Select(g => g.Name);
                s.Filter.Tags = Filter.Tags?.Select(t => t.Name);
                s.Filter.Platforms = Filter.Platforms?.Select(p => p.Name);
                s.Filter.Developers = Filter.Developers?.Select(c => c.Name);
                s.Filter.Publishers = Filter.Publishers?.Select(c => c.Name);
                s.Filter.MinPlayers = Filter.MinPlayers;
                s.Filter.MaxPlayers = Filter.MaxPlayers;
                s.Filter.Installed = Filter.Installed;
            });
        }
    }
}
