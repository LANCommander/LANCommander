using Avalonia.Controls;

namespace LANCommander.Launcher.Plugins.Extensions;

/// <summary>
/// Adds an additional section to the launcher's settings page. Implementations are resolved
/// from DI and appended, ordered by <see cref="Order"/>, beneath the built-in settings sections.
/// </summary>
public interface ISettingsPageExtension
{
    /// <summary>Heading shown for the section.</summary>
    string Title { get; }

    /// <summary>Relative position among extension sections; lower values appear first.</summary>
    int Order { get; }

    /// <summary>Builds the control rendered inside the section.</summary>
    Control BuildContent();
}
