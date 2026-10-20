#if DEBUG
namespace LANCommander.Launcher.ViewModels.Packaging;

public partial class MetadataStepViewModel
{
    /// <summary>
    /// Selects a provider for a visual fixture without the change handler, which would ask the
    /// server for the provider's sub-providers.
    /// </summary>
    internal void SelectProviderForFixture(string provider)
    {
        _selectedProvider = provider;

        OnPropertyChanged(nameof(SelectedProvider));
    }
}
#endif
