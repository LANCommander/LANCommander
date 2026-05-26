using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class LibraryRowView : UserControl
{
    public static readonly StyledProperty<double> SmallCoverWidthProperty =
        AvaloniaProperty.Register<LibraryRowView, double>(nameof(SmallCoverWidth), defaultValue: 105.0);

    public static readonly StyledProperty<double> SmallCoverHeightProperty =
        AvaloniaProperty.Register<LibraryRowView, double>(nameof(SmallCoverHeight), defaultValue: 158.0);

    public double SmallCoverWidth
    {
        get => GetValue(SmallCoverWidthProperty);
        private set => SetValue(SmallCoverWidthProperty, value);
    }

    public double SmallCoverHeight
    {
        get => GetValue(SmallCoverHeightProperty);
        private set => SetValue(SmallCoverHeightProperty, value);
    }

    public LibraryRowView()
    {
        InitializeComponent();
        KeyDown += OnViewKeyDown;
    }

    private void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Left) return;

        // If focus is somewhere in the right panel and Left was not handled,
        // move focus back to the ListBox's selected item
        var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focusedElement == null) return;

        if (RightPanelScrollViewer.IsVisualAncestorOf(focusedElement))
        {
            GameListBox.Focus(NavigationMethod.Directional);
            // Ensure the selected item gets focus
            if (GameListBox.SelectedIndex >= 0)
            {
                var container = GameListBox.ContainerFromIndex(GameListBox.SelectedIndex);
                (container as InputElement)?.Focus(NavigationMethod.Directional);
            }
            e.Handled = true;
        }
    }

    private void RightPanelScrollViewer_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateGridLayout(e.NewSize.Width);
    }

    private void UpdateGridLayout(double availableWidth)
    {
        if (LibraryGridItemsRepeater?.Layout is not UniformGridLayout layout) return;

        const int    minCols    = 4;
        const int    maxCols    = 7;
        const double coverRatio = 9.0 / 6.0;
        const double colSpacing = 12.0;
        const double hPadding   = 84.0; // outer StackPanel margin (10+10) + inner StackPanel margin (32+32)
        const double minWidth   = 105.0;

        double usable = Math.Max(availableWidth - hPadding, minCols * (minWidth + colSpacing));

        int cols = (int)Math.Clamp(
            Math.Floor((usable + colSpacing) / (minWidth + colSpacing)), minCols, maxCols);

        double coverW = Math.Floor((usable - colSpacing * (cols - 1)) / cols);
        double coverH = Math.Floor(coverW * coverRatio);

        SmallCoverWidth  = coverW;
        SmallCoverHeight = coverH;

        layout.MinItemWidth  = coverW;
        layout.MinItemHeight = coverH;
    }

    private void GameList_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm && vm.SelectedGame != null)
        {
            vm.ViewGameDetailsCommand.Execute(vm.SelectedGame);
        }
    }

    private void GameList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;

        if (e.Key == Key.Return)
        {
            if (DataContext is LibraryViewModel vm && vm.SelectedGame != null)
            {
                vm.ViewGameDetailsCommand.Execute(vm.SelectedGame);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Right)
        {
            // Move focus from left list to right panel — skip containers/inputs
            var target = RightPanelScrollViewer.GetVisualDescendants()
                .OfType<InputElement>()
                .FirstOrDefault(el => el.Focusable && el.IsEffectivelyVisible
                    && el is not ItemsControl && el is not ScrollViewer && el is not TextBox);

            if (target != null)
            {
                target.Focus(NavigationMethod.Directional);
                e.Handled = true;
            }
        }
    }
}
