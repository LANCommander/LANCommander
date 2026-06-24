using Avalonia;
using Avalonia.Markup.Xaml;

namespace LANCommander.Launcher.Tests;

public partial class TestApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
