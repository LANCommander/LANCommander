using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class GamesGridView : UserControl
{
    public static readonly StyledProperty<double> CoverWidthProperty =
        AvaloniaProperty.Register<GamesGridView, double>(nameof(CoverWidth), defaultValue: 140.0);

    public static readonly StyledProperty<double> CoverHeightProperty =
        AvaloniaProperty.Register<GamesGridView, double>(nameof(CoverHeight), defaultValue: 210.0);

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

    public GamesGridView()
    {
        InitializeComponent();
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
        const double coverRatio = 9.0 / 6.0;
        const double colSpacing = 12.0;
        const double hPadding   = 64.0;

        double usable = Math.Max(availableWidth - hPadding, minCols * (140.0 + colSpacing));

        int cols = (int)Math.Clamp(
            Math.Floor((usable + colSpacing) / (140.0 + colSpacing)), minCols, maxCols);

        double coverW = Math.Floor((usable - colSpacing * (cols - 1)) / cols);
        double coverH = Math.Floor(coverW * coverRatio);

        CoverWidth  = coverW;
        CoverHeight = coverH;

        layout.MinItemWidth  = coverW;
        layout.MinItemHeight = coverH;
    }
}
