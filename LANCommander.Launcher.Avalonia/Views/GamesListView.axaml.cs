using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System.Linq;
using LANCommander.Launcher.Avalonia.ViewModels;
using LANCommander.Launcher.Settings.Enums;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class GamesListView : UserControl
{
    // Exposed so DataTemplates can bind to them via $parent[local:GamesListView]
    public static readonly StyledProperty<double> CoverWidthProperty =
        AvaloniaProperty.Register<GamesListView, double>(nameof(CoverWidth), defaultValue: 140.0);

    public static readonly StyledProperty<double> CoverHeightProperty =
        AvaloniaProperty.Register<GamesListView, double>(nameof(CoverHeight), defaultValue: 210.0);

    public double CoverWidth
    {
        get => GetValue(CoverWidthProperty);
        private set => SetValue(CoverWidthProperty, value);
    }

    public double CoverHeight
    {
        get => GetValue(CoverHeightProperty);
        private set => SetValue(CoverHeightProperty, value);
    }

    public GamesListView()
    {
        InitializeComponent();
    }

    private void GridViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GamesCollectionViewModel vm)
            vm.SelectedViewType = GameViewType.Grid;
    }

    private void ListViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GamesCollectionViewModel vm)
            vm.SelectedViewType = GameViewType.List;
    }

    private void HorizontalViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GamesCollectionViewModel vm)
            vm.SelectedViewType = GameViewType.Horizontal;
    }

    private void OnGameTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is GamesCollectionViewModel vm && vm.SelectedGame is not null)
            vm.ViewGameDetailsCommand.Execute(vm.SelectedGame);
    }

    private void FlatGridScrollViewer_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateFlatGridLayout(e.NewSize.Width);
    }

    private void UpdateFlatGridLayout(double availableWidth)
    {
        if (FlatGridItemsRepeater?.Layout is not UniformGridLayout layout) return;

        const int    minCols    = 4;
        const int    maxCols    = 6;
        const double coverRatio = 9.0 / 6.0;  // 6:9 portrait
        const double colSpacing = 12.0;
        const double hPadding   = 32.0;        // 16px left + 16px right from ItemsRepeater margin

        double usable = Math.Max(availableWidth - hPadding, minCols * (140.0 + colSpacing));

        // How many columns fit given minimum cover width (140) plus column spacing
        int cols = (int)Math.Clamp(
            Math.Floor((usable + colSpacing) / (140.0 + colSpacing)), minCols, maxCols);

        // Cover width is the exact column width after distributing spacing
        double coverW = Math.Floor((usable - colSpacing * (cols - 1)) / cols);
        double coverH = Math.Floor(coverW * coverRatio);

        CoverWidth  = coverW;
        CoverHeight = coverH;

        layout.MinItemWidth  = coverW;
        layout.MinItemHeight = coverH;
    }

    private void IndexLetter_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GameGroupViewModel group })
            ScrollToGroup(group.Name);
    }

    private void ScrollToGroup(string name)
    {
        if (DataContext is not GamesCollectionViewModel vm || GroupedItemsControl is null) return;

        var group = vm.GroupedGames.FirstOrDefault(g => g.Name == name);
        if (group != null)
            GroupedItemsControl.ScrollIntoView(group);
    }
}
