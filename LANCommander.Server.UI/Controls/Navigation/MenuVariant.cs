namespace LANCommander.Server.UI.Controls;

/// <summary>How a vertical <see cref="Menu"/> is drawn.</summary>
public enum MenuVariant
{
    /// <summary>Compact items, e.g. inside a dropdown.</summary>
    Default,

    /// <summary>The application's main navigation: 36px items with icons and a selection rail.</summary>
    Sidebar,

    /// <summary>The sections of an edit page: 32px text items under optional <see cref="MenuGroup"/> kickers.</summary>
    Section,
}
