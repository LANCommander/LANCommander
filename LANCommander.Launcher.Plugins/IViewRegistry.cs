using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace LANCommander.Launcher.Plugins;

/// <summary>
/// Maps view model types to the Avalonia controls that render them. Seeded at startup with the
/// launcher's built-in mappings and extended at runtime by plugins that add navigable views.
/// Consumers apply <see cref="AsDataTemplate"/> to a <see cref="ContentControl"/> so content is
/// resolved by view model type, replacing the previously hard-coded inline XAML data templates.
/// </summary>
public interface IViewRegistry
{
    /// <summary>Register a control factory for the given view model type.</summary>
    void Register(Type viewModelType, Func<Control> factory);

    /// <summary>Register a control factory for <typeparamref name="TViewModel"/>.</summary>
    void Register<TViewModel>(Func<Control> factory);

    /// <summary>
    /// Build an <see cref="IDataTemplate"/> backed by the current registrations. Matching prefers
    /// the most-derived registered type, preserving the launcher's existing rule that
    /// DepotGameDetailViewModel resolves before its GameDetailViewModel base.
    /// </summary>
    IDataTemplate AsDataTemplate();
}
