using Avalonia.Controls;
using Avalonia.Layout;
using LANCommander.Launcher.Plugins.Extensions;

namespace LANCommander.SamplePlugin;

/// <summary>
/// Adds a settings section built entirely in code (no XAML / avares assets), which is the
/// recommended authoring path for plugin views since it avoids Avalonia asset resolution across the
/// plugin's AssemblyLoadContext.
/// </summary>
public sealed class SampleSettingsExtension : ISettingsPageExtension
{
    public string Title => "Sample Plugin";

    public int Order => 0;

    public Control BuildContent()
    {
        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = "This settings section was added by the sample plugin.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Opacity = 0.7,
        });

        panel.Children.Add(new CheckBox
        {
            Content = "Enable sample feature",
            HorizontalAlignment = HorizontalAlignment.Left,
        });

        return panel;
    }
}
