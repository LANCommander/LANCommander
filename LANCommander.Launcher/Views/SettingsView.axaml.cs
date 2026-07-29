using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LANCommander.Launcher.Plugins.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        AppendPluginSections();
    }

    /// <summary>
    /// Appends any plugin settings sections beneath the built-in sections, styled to
    /// match the surrounding cards so extensions look native.
    /// </summary>
    private void AppendPluginSections()
    {
        var extensions = App.Services?
            .GetServices<ISettingsPageExtension>()
            .OrderBy(c => c.Order)
            .ToList();

        if (extensions == null || extensions.Count == 0)
            return;

        foreach (var extension in extensions)
        {
            Control content;

            try
            {
                content = extension.BuildContent();
            }
            catch
            {
                // A misbehaving plugin must not break the settings page.
                continue;
            }

            var header = new TextBlock
            {
                Text = extension.Title,
                FontWeight = FontWeight.SemiBold,
                FontSize = 16,
            };

            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(header);
            stack.Children.Add(content);

            var card = new Border
            {
                Padding = new Avalonia.Thickness(16),
                CornerRadius = new Avalonia.CornerRadius(8),
                Child = stack,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            if (this.TryFindResource("SystemControlBackgroundChromeMediumLowBrush", out var brush) && brush is IBrush background)
                card.Background = background;

            SectionsPanel.Children.Add(card);
        }
    }
}
