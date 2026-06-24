using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LANCommander.Launcher.Controls;

namespace LANCommander.Launcher.Views.Components;

public partial class CoverButton : UserControl
{
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<CoverButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<CoverButton, object?>(nameof(CommandParameter));

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private ContentPresenter? _itemContainer;
    private bool _isInsideCarousel;
    private bool _resolved;

    public CoverButton()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Finds the ContentPresenter container, but only when inside a CarouselControl.
    /// Caches the result so the visual tree walk only happens once.
    /// </summary>
    private ContentPresenter? GetItemContainer()
    {
        if (_resolved)
            return _itemContainer;

        _resolved = true;
        _isInsideCarousel = this.FindAncestorOfType<CarouselControl>() != null;

        if (!_isInsideCarousel)
            return null;

        var current = this.GetVisualParent();
        while (current != null)
        {
            if (current is ContentPresenter cp && cp.GetVisualParent() is Panel)
            {
                _itemContainer = cp;
                return cp;
            }
            current = current.GetVisualParent();
        }
        return null;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        SetHighlighted(true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHighlighted(false);
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        SetHighlighted(true);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        SetHighlighted(false);
    }

    private void SetHighlighted(bool highlighted)
    {
        ZIndex = highlighted ? 100 : 0;

        var container = GetItemContainer();
        if (container != null)
            container.ZIndex = highlighted ? 1 : 0;

        if (CoverControl != null)
            CoverControl.IsPlayingAnimation = highlighted;
    }
}
