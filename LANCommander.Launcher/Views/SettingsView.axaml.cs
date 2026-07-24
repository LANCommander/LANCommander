using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LANCommander.Launcher.Plugins.Contributions;
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
    /// Appends any plugin-contributed settings sections beneath the built-in sections, styled to
    /// match the surrounding cards so contributions look native.
    /// </summary>
    private void AppendPluginSections()
    {
        var contributions = App.Services?
            .GetServices<ISettingsPageContribution>()
            .OrderBy(c => c.Order)
            .ToList();

        if (contributions == null || contributions.Count == 0)
            return;

        foreach (var contribution in contributions)
        {
            Control content;

            try
            {
                content = contribution.BuildContent();
            }
            catch
            {
                // A misbehaving plugin must not break the settings page.
                continue;
            }

            var header = new TextBlock
            {
                Text = contribution.Title,
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
