using Avalonia;
using Avalonia.Controls;

namespace LANCommander.Launcher.Views.Components;

/// <summary>
/// Left rail of the Library and Depot pages, hosted by the shell so it stays beside the game pages
/// opened from them. <see cref="ActivePage"/> ("Library" or "Depot") lights the matching nav entry;
/// <see cref="RailContent"/> is the page's own section, shown below the nav.
/// </summary>
public partial class NavRail : UserControl
{
    public static readonly StyledProperty<string?> ActivePageProperty =
        AvaloniaProperty.Register<NavRail, string?>(nameof(ActivePage));

    public static readonly StyledProperty<object?> RailContentProperty =
        AvaloniaProperty.Register<NavRail, object?>(nameof(RailContent));

    public string? ActivePage
    {
        get => GetValue(ActivePageProperty);
        set => SetValue(ActivePageProperty, value);
    }

    public object? RailContent
    {
        get => GetValue(RailContentProperty);
        set => SetValue(RailContentProperty, value);
    }

    public NavRail()
    {
        InitializeComponent();
    }
}
