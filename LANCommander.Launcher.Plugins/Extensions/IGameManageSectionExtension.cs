using Avalonia.Controls;

namespace LANCommander.Launcher.Plugins.Extensions;

/// <summary>
/// Adds an additional section to a game's "Manage" dialog. Implementations are resolved from DI
/// and appended, ordered by <see cref="Order"/>, after the built-in sections (Options, Modify,
/// Versions) in the left-hand navigation.
/// </summary>
public interface IGameManageSectionExtension
{
    /// <summary>Label shown for the section in the dialog's navigation menu.</summary>
    string Title { get; }

    /// <summary>Phosphor icon name shown next to the section in the navigation menu.</summary>
    string IconValue { get; }

    /// <summary>Relative position among extension sections; lower values appear first.</summary>
    int Order { get; }

    /// <summary>Builds the control rendered in the section's content pane for the given game.</summary>
    Control BuildContent(Guid gameId);
}
