#if DEBUG
using System.Collections.Generic;
using System.Linq;
using LANCommander.Launcher.ViewModels.Components;

namespace LANCommander.Launcher.ViewModels;

public abstract partial class GamesCollectionViewModel
{
    /// <summary>
    /// Replaces the games with canned ones and runs them through the same filter, sort and group
    /// pipeline a load does, so fixtures exercise the real grouping and facet lists.
    /// </summary>
    internal void SeedFixture(IEnumerable<GameItemViewModel> games)
    {
        _allGames = games.ToList();

        PopulateGenres();
        PopulateCollections();
        PopulateTags();
        PopulateDevelopers();
        PopulatePublishers();

        ApplyFilters();
    }
}
#endif
