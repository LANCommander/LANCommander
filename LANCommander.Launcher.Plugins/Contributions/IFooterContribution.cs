using Avalonia.Controls;

namespace LANCommander.Launcher.Plugins.Contributions;

/// <summary>
/// Contributes a control to the launcher shell's footer. Implementations are resolved from DI and
/// rendered, ordered by <see cref="Order"/>, alongside the built-in footer items.
/// </summary>
public interface IFooterContribution
{
    /// <summary>Relative position among contributed items; lower values appear first.</summary>
    int Order { get; }

    /// <summary>Builds the control rendered in the footer.</summary>
    Control BuildContent();
}
