#if DEBUG
using LANCommander.Launcher.ViewModels.Packaging;

namespace LANCommander.Launcher.ViewModels;

public partial class ShellViewModel
{
    /// <summary>
    /// Builds the child view models the way <see cref="InitializeAsync"/> does, but connects to
    /// nothing, imports nothing and loads nothing, so a fixture can fill each page itself. See
    /// <see cref="Fixtures.FixtureContext"/>.
    /// </summary>
    internal void InitializeForFixture()
    {
        DepotViewModel           = new DepotViewModel(_serviceProvider);
        DepotBrowseViewModel     = new DepotBrowseViewModel(_serviceProvider);
        DepotGameDetailViewModel = new DepotGameDetailViewModel(_serviceProvider);
        GamesListViewModel       = new GamesListViewModel(_serviceProvider);
        LibraryViewModel         = new LibraryViewModel(_serviceProvider);
        GameDetailViewModel      = new GameDetailViewModel(_serviceProvider);
        DownloadQueue            = new DownloadQueueViewModel(_serviceProvider);
        SettingsViewModel        = new SettingsViewModel(_serviceProvider);
        PackagingWizardViewModel = new PackagingWizardViewModel(_serviceProvider);
        Chat                     = new ChatWindowViewModel(_serviceProvider);
    }
}
#endif
