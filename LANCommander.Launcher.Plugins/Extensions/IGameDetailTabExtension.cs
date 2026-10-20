using Avalonia.Controls;

namespace LANCommander.Launcher.Plugins.Extensions;

/// <summary>
/// Adds an additional tab to a game's detail view. Implementations are resolved from DI and
/// appended, ordered by <see cref="Order"/>, after the built-in tabs.
/// </summary>
public interface IGameDetailTabExtension
{
    /// <summary>Header shown on the tab.</summary>
    string Header { get; }

    /// <summary>Relative position among extension tabs; lower values appear first.</summary>
    int Order { get; }

    /// <summary>Builds the control rendered inside the tab for the given game.</summary>
    Control BuildContent(Guid gameId);
}
