using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LANCommander.Launcher.Plugins;
using LANCommander.Launcher.Views.Packaging;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Renders the wizard shell with a real view registry and asserts a step actually resolves to
/// its view.
/// </summary>
/// <remarks>
/// Constructing the views is not enough on its own: the blank page came from the registry's
/// data template being attached to a null named control, which only happens when a registry is
/// present. This exercises that path.
/// </remarks>
public class PackagingWizardRenderTests
{
    /// <summary>
    /// Minimal stand-in for a wizard step, so the render can be exercised without building the
    /// real view models and their service dependencies.
    /// </summary>
    private sealed class FakeStep : ViewModels.ViewModelBase
    {
    }

    private sealed class FakeStepView : UserControl
    {
        public FakeStepView() => Content = new TextBlock { Text = "step-rendered" };
    }

    [AvaloniaFact]
    public void StepHostAcceptsARegistryDataTemplate()
    {
        // Exactly what PackagingWizardView's constructor does. Before the fix this threw a
        // NullReferenceException, the view failed to construct, and the user saw a blank page.
        var view = new PackagingWizardView();

        var stepHost = view.FindControl<ContentControl>("StepHost");

        Assert.NotNull(stepHost);

        var registry = new ViewRegistry();

        registry.Register<FakeStep>(() => new FakeStepView());

        var exception = Record.Exception(() => stepHost!.DataTemplates.Add(registry.AsDataTemplate()));

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void StepHostRendersTheViewRegisteredForItsContent()
    {
        var view = new PackagingWizardView();
        var stepHost = view.FindControl<ContentControl>("StepHost")!;

        var registry = new ViewRegistry();

        registry.Register<FakeStep>(() => new FakeStepView());

        stepHost.DataTemplates.Add(registry.AsDataTemplate());
        stepHost.Content = new FakeStep();

        var window = new Window { Width = 900, Height = 700, Content = view };

        window.Show();

        Dispatcher.UIThread.RunJobs();

        // The step's view must be somewhere under the host, not just assigned as content.
        var rendered = stepHost.GetVisualDescendants().OfType<TextBlock>()
            .Any(t => t.Text == "step-rendered");

        Assert.True(rendered, "The wizard's step host did not render the view registered for its content.");
    }
}
