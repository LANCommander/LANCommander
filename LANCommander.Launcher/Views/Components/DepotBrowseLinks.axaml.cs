using Avalonia;
using Avalonia.Controls;

namespace LANCommander.Launcher.Views.Components;

/// <summary>
/// The Depot rail's BROWSE rows, shared by the Depot home and the browse page. Bind the DataContext to
/// the DepotViewModel; <see cref="Selected"/> ("All", "NewReleases", "PlayTogether", "Backlog" or null)
/// highlights the row for the view being shown.
/// </summary>
public partial class DepotBrowseLinks : UserControl
{
    public static readonly StyledProperty<string?> SelectedProperty =
        AvaloniaProperty.Register<DepotBrowseLinks, string?>(nameof(Selected));

    public string? Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    public DepotBrowseLinks()
    {
        InitializeComponent();
    }
}
