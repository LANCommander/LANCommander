using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Views;

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
        AddHandler(KeyDownEvent, OnViewKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    // ── Section navigation helpers ──────────────────────────────────────

    /// <summary>
    /// Returns the ordered list of visible right-panel sections
    /// (carousels then grid) for Up/Down navigation.
    /// </summary>
    private List<Control> GetVisibleSections()
    {
        var sections = new List<Control>();
        if (RecentlyPlayedCarousel.IsVisible) sections.Add(RecentlyPlayedCarousel);
        if (CollectionsCarousel.IsVisible) sections.Add(CollectionsCarousel);
        sections.Add(LibraryGridItemsRepeater); // always visible
        return sections;
    }

    /// <summary>
    /// Determines which right-panel section currently contains focus.
    /// </summary>
    private Control? GetFocusedSection(Visual focused)
    {
        if (RecentlyPlayedCarousel.IsVisible && RecentlyPlayedCarousel.IsVisualAncestorOf(focused))
            return RecentlyPlayedCarousel;
        if (CollectionsCarousel.IsVisible && CollectionsCarousel.IsVisualAncestorOf(focused))
            return CollectionsCarousel;
        if (LibraryGridItemsRepeater.IsVisualAncestorOf(focused))
            return LibraryGridItemsRepeater;
        return null;
    }

    /// <summary>
    /// Focuses the first interactive item inside a section control.
    /// Scrolls the right panel to the top when targeting the first section.
    /// </summary>
    private void FocusSectionFirstItem(Control section)
    {
        var target = section.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(el => el.Focusable && el.IsEffectivelyVisible
                && el is not ItemsControl && el is not ScrollViewer && el is not TextBox);

        if (target != null)
        {
            target.Focus(NavigationMethod.Directional);

            var sections = GetVisibleSections();
            if (sections.Count > 0 && sections[0] == section)
                RightPanelScrollViewer.Offset = new Vector(RightPanelScrollViewer.Offset.X, 0);
            else
                (target as Control)?.BringIntoView();
        }
    }

    // ── Key handling ────────────────────────────────────────────────────

    private void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focusedElement == null) return;

        var focusInList = GameListBox == focusedElement || GameListBox.IsVisualAncestorOf(focusedElement);

        // Right from sidebar: must intercept even when the ListBox's internal
        // directional navigation has already marked the event as handled.
        if (e.Key == Key.Right && focusInList)
        {
            var sections = GetVisibleSections();
            if (sections.Count > 0)
            {
                FocusSectionFirstItem(sections[0]);
                e.Handled = true;
            }
            return;
        }

        if (e.Handled) return;

        switch (e.Key)
        {
            case Key.Left:
                // If focus is in the right panel and Left was not consumed by a
                // child (carousel at index 0, or grid leftmost column), move
                // focus back to the sidebar list.
                if (RightPanelScrollViewer.IsVisualAncestorOf(focusedElement))
                {
                    FocusListSelectedItem();
                    e.Handled = true;
                }
                break;

            case Key.Down:
                // Navigate from current section to the next section below.
                // For the grid, DirectionalNavigation handles internal Down;
                // this only fires when the event bubbles (bottom row or carousel).
                if (RightPanelScrollViewer.IsVisualAncestorOf(focusedElement))
                {
                    var sections = GetVisibleSections();
                    var current = GetFocusedSection(focusedElement);
                    if (current != null)
                    {
                        var idx = sections.IndexOf(current);
                        if (idx >= 0 && idx < sections.Count - 1)
                        {
                            FocusSectionFirstItem(sections[idx + 1]);
                            e.Handled = true;
                        }
                    }
                }
                break;

            case Key.Up:
                // Navigate from current section to the previous section above.
                // For the grid, DirectionalNavigation handles internal Up;
                // this only fires when the event bubbles (top row or carousel).
                if (RightPanelScrollViewer.IsVisualAncestorOf(focusedElement))
                {
                    var sections = GetVisibleSections();
                    var current = GetFocusedSection(focusedElement);
                    if (current != null)
                    {
                        var idx = sections.IndexOf(current);
                        if (idx > 0)
                        {
                            FocusSectionFirstItem(sections[idx - 1]);
                            e.Handled = true;
                        }
                    }
                }
                break;
        }
    }

    private void FocusListSelectedItem()
    {
        // Ensure there's a valid selection
        if (GameListBox.SelectedIndex < 0 && GameListBox.ItemCount > 0)
            GameListBox.SelectedIndex = 0;

        if (GameListBox.SelectedIndex >= 0)
        {
            // Scroll the selected item into view so its container is realized
            GameListBox.ScrollIntoView(GameListBox.SelectedIndex);

            var container = GameListBox.ContainerFromIndex(GameListBox.SelectedIndex);
            if (container is InputElement input)
            {
                input.Focus(NavigationMethod.Directional);
                return;
            }
        }

        // Fallback: focus the ListBox itself
        GameListBox.Focus(NavigationMethod.Directional);
    }

    // ── Layout ──────────────────────────────────────────────────────────

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

    // ── List interactions ───────────────────────────────────────────────

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
    }
}
