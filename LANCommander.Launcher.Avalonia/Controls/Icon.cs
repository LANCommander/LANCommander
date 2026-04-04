using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LANCommander.Launcher.Avalonia.Controls;

/// <summary>
/// Icon variant (weight) corresponding to the Phosphor icon families.
/// Bold icons are stroke-based; all others are fill-based.
/// </summary>
public enum IconVariant
{
    Bold,
    DuoTone,
    Fill,
    Light,
    Regular,
    Thin,
}

/// <summary>
/// Displays a Phosphor icon by variant and name.
/// Resolves the StreamGeometry resource keyed as "{Type}_{Value}"
/// (e.g. "Regular_ArrowClockwise") from the application resource dictionaries.
///
/// Usage:
///   &lt;controls:Icon Type="Regular" Value="ArrowClockwise" Width="16" Height="16" /&gt;
///
/// Color sets the fill colour via Foreground (and stroke colour for Bold icons).
/// Bold icons additionally require Stroke to be set on the control via a style,
/// or you can set StrokeThickness directly (recommended value: 16, matching
/// the 256-unit viewBox stroke width used by Phosphor's bold weight).
/// IsVisible is inherited from Visual and toggles visibility.
/// </summary>
public class Icon : PathIcon
{
    public static readonly StyledProperty<IconVariant> TypeProperty =
        AvaloniaProperty.Register<Icon, IconVariant>(nameof(Type), IconVariant.Regular);

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<Icon, string?>(nameof(Value));

    public static readonly StyledProperty<Color?> ColorProperty =
        AvaloniaProperty.Register<Icon, Color?>(nameof(Color));

    public IconVariant Type
    {
        get => GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Color? Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    static Icon()
    {
        TypeProperty.Changed.AddClassHandler<Icon>((c, _) => c.UpdateGeometry());
        ValueProperty.Changed.AddClassHandler<Icon>((c, _) => c.UpdateGeometry());
        ColorProperty.Changed.AddClassHandler<Icon>((c, _) => c.UpdateColor());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateGeometry();
    }

    private void UpdateGeometry()
    {
        if (Value is null) return;

        var key = $"{Type}_{Value}";

        if (Application.Current is { } app &&
            app.TryGetResource(key, null, out var resource) &&
            resource is StreamGeometry geometry)
            Data = geometry;
    }

    private void UpdateColor()
    {
        if (Color is { } color)
            Foreground = new SolidColorBrush(color);
    }
}
