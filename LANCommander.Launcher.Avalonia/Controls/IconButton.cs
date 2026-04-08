using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace LANCommander.Launcher.Avalonia.Controls;

public enum IconPosition
{
    Left,
    Right,
}

public class IconButton : Button
{
    public static readonly StyledProperty<IconVariant> IconTypeProperty =
        AvaloniaProperty.Register<IconButton, IconVariant>(nameof(IconType), IconVariant.Regular);

    public static readonly StyledProperty<string?> IconValueProperty =
        AvaloniaProperty.Register<IconButton, string?>(nameof(IconValue));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<IconButton, double>(nameof(Size), 16);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<IconButton, string?>(nameof(Text));

    public static readonly StyledProperty<IconPosition> IconPlacementProperty =
        AvaloniaProperty.Register<IconButton, IconPosition>(nameof(IconPlacement), IconPosition.Left);

    public IconVariant IconType
    {
        get => GetValue(IconTypeProperty);
        set => SetValue(IconTypeProperty, value);
    }

    public string? IconValue
    {
        get => GetValue(IconValueProperty);
        set => SetValue(IconValueProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IconPosition IconPlacement
    {
        get => GetValue(IconPlacementProperty);
        set => SetValue(IconPlacementProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateContent();
    }

    static IconButton()
    {
        VerticalAlignmentProperty.OverrideDefaultValue<IconButton>(global::Avalonia.Layout.VerticalAlignment.Center);

        IconTypeProperty.Changed.AddClassHandler<IconButton>((b, _) => b.UpdateContent());
        IconValueProperty.Changed.AddClassHandler<IconButton>((b, _) => b.UpdateContent());
        SizeProperty.Changed.AddClassHandler<IconButton>((b, _) => b.UpdateContent());
        TextProperty.Changed.AddClassHandler<IconButton>((b, _) => b.UpdateContent());
        IconPlacementProperty.Changed.AddClassHandler<IconButton>((b, _) => b.UpdateContent());
    }

    private void UpdateContent()
    {
        var icon = new Icon
        {
            Type = IconType,
            Value = IconValue,
            Width = Size,
            Height = Size,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };

        if (Text is null)
        {
            Content = icon;
            return;
        }

        var textBlock = new TextBlock
        {
            Text = Text,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            FontSize = Size,
        };

        var panel = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = (Size / 2) + 2,
        };

        if (IconPlacement == IconPosition.Right)
        {
            panel.Children.Add(textBlock);
            panel.Children.Add(icon);
        }
        else
        {
            panel.Children.Add(icon);
            panel.Children.Add(textBlock);
        }

        Content = panel;
    }
}
