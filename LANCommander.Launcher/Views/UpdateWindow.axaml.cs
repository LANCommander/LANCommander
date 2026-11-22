using Avalonia.Controls;
using Avalonia.Input;

namespace LANCommander.Launcher.Views;

public partial class UpdateWindow : Window
{
    public UpdateWindow()
    {
        InitializeComponent();

        ViewBackground.Apply(BackgroundImage);

        // No title bar, so the whole window drags
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
    }
}
