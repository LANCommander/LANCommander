using Avalonia;
using Avalonia.Controls;

namespace LANCommander.Launcher.Views.Components;

/// <summary>
/// Left rail shared by the Library and Depot pages. <see cref="ActivePage"/> ("Library" or "Depot")
/// lights the matching nav entry; <see cref="RailContent"/> is the page's own section, shown between
/// the nav and the status bar.
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
