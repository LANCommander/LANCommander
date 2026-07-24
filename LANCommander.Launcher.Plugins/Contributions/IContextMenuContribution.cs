using Avalonia.Controls;

namespace LANCommander.Launcher.Plugins.Contributions;

/// <summary>
/// Contributes items to a game's context menu. Implementations are resolved from DI and their items
/// appended to the consolidated game menu shown on covers and list rows.
/// </summary>
public interface IContextMenuContribution
{
    /// <summary>Relative position among contributed items; lower values appear first.</summary>
    int Order { get; }

    /// <summary>Builds the menu items shown for the given game (typically <see cref="MenuItem"/>s).</summary>
    IEnumerable<Control> BuildMenuItems(Guid gameId);
}
