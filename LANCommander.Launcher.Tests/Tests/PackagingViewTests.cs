using System;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LANCommander.Launcher.Views.Packaging;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Guards the wiring between the packaging views and their XAML.
/// </summary>
/// <remarks>
/// The regression these exist for: a hand-written
/// <c>private void InitializeComponent() =&gt; AvaloniaXamlLoader.Load(this)</c> hides the one
/// the XAML compiler generates. The visual tree still loads, so nothing throws during a plain
/// construction — but the generated <c>x:Name</c> backing fields are never assigned. Code-behind
/// that touches a named control then dereferences null, the view fails to construct inside the
/// data template, and the user is shown a blank page with nothing in the log.
/// </remarks>
public class PackagingViewTests
{
    public static TheoryData<Type> PackagingViewTypes
    {
        get
        {
            var data = new TheoryData<Type>();

            data.Add(typeof(PackagingWizardView));
            data.Add(typeof(MonitorStepView));
            data.Add(typeof(InstallDirectoryStepView));
            data.Add(typeof(FileSelectionStepView));
            data.Add(typeof(RegistrySelectionStepView));
            data.Add(typeof(MetadataStepView));
            data.Add(typeof(ActionStepView));
            data.Add(typeof(OutputStepView));

            return data;
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(PackagingViewTypes))]
    public void ViewConstructsWithoutThrowing(Type viewType)
    {
        var view = Activator.CreateInstance(viewType);

        Assert.NotNull(view);
        Assert.IsAssignableFrom<UserControl>(view);
    }

    [AvaloniaTheory]
    [MemberData(nameof(PackagingViewTypes))]
    public void ViewDeclaresNoHandWrittenInitializeComponent(Type viewType)
    {
        // Declared on the type itself rather than inherited or generated: a parameterless
        // InitializeComponent written by hand is what shadows the generated one.
        var declared = viewType.GetMethods(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "InitializeComponent" && m.GetParameters().Length == 0)
            .ToList();

        Assert.True(
            declared.Count == 0,
            $"{viewType.Name} declares its own parameterless InitializeComponent, which hides the " +
            "generated one and leaves every x:Name field null.");
    }

    [AvaloniaFact]
    public void WizardResolvesItsNamedStepHost()
    {
        // The specific null dereference that produced the blank view.
        var view = new PackagingWizardView();

        var field = typeof(PackagingWizardView).GetField(
            "StepHost", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.NotNull(field);
        Assert.NotNull(field!.GetValue(view));
    }

    [AvaloniaFact]
    public void WizardStepHostIsAContentControl()
    {
        var view = new PackagingWizardView();

        Assert.NotNull(view.FindControl<ContentControl>("StepHost"));
    }
}
