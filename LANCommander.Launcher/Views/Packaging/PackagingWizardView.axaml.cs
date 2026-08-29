using Avalonia.Controls;
using LANCommander.Launcher.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Views.Packaging;

public partial class PackagingWizardView : UserControl
{
    public PackagingWizardView()
    {
        InitializeComponent();

        // Reuse the app-wide registry so each step view model resolves to its own view, the
        // same mechanism the shell uses for its pages.
        var registry = App.Services?.GetService<IViewRegistry>();

        if (registry != null)
            StepHost.DataTemplates.Add(registry.AsDataTemplate());
    }
}
